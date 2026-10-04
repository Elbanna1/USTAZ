using System.Linq.Expressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Common.Models;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Messaging;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Messaging;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------
public sealed record ConversationDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public ConversationType Type { get; init; }

    public string TypeName => Type.ToString();

    public string? Subject { get; init; }

    public Guid? GroupId { get; init; }

    public string? GroupName { get; init; }

    public DateTime? LastMessageAt { get; init; }

    public string? LastMessagePreview { get; init; }

    public int UnreadCount { get; init; }

    public IReadOnlyList<ConversationParticipantDto> Participants { get; init; } = [];

    public DateTime CreatedAt { get; init; }
}

public sealed record ConversationParticipantDto
{
    public Guid UserId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? ProfileImageUrl { get; init; }

    public DateTime? LastReadAt { get; init; }
}

public sealed record MessageDto
{
    public Guid Id { get; init; }

    public Guid ConversationId { get; init; }

    public Guid SenderUserId { get; init; }

    public string SenderName { get; init; } = string.Empty;

    public string? SenderImageUrl { get; init; }

    public string Body { get; init; } = string.Empty;

    public DateTime SentAt { get; init; }

    public DateTime? EditedAt { get; init; }

    public bool IsMine { get; init; }

    public IReadOnlyList<MessageAttachmentDto> Attachments { get; init; } = [];
}

public sealed record MessageAttachmentDto
{
    public Guid Id { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string FileUrl { get; init; } = string.Empty;

    public MaterialFileType FileType { get; init; }
}

/// <summary>
/// Participation is the only key to a conversation. Every read and write passes through
/// here, so knowing a conversation id is worth nothing on its own.
/// </summary>
internal static class MessagingGuard
{
    public static async Task<Conversation> LoadParticipatingAsync(
        IApplicationDbContext context,
        Guid conversationId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var conversation = await context.Conversations
            .Include(entity => entity.Participants)
            .FirstOrDefaultAsync(entity => entity.Id == conversationId, cancellationToken)
            ?? throw new NotFoundException("Conversation", conversationId);

        if (!isAdmin && !conversation.HasParticipant(userId))
        {
            // 404 rather than 403: a non-participant learns nothing about the thread.
            throw new NotFoundException("Conversation", conversationId);
        }

        return conversation;
    }
}

internal static class MessagingProjections
{
    public static Expression<Func<Message, MessageDto>> ToMessageDto(Guid currentUserId) =>
        message => new MessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderUserId = message.SenderUserId,
            SenderName = message.Sender.DisplayName,
            SenderImageUrl = message.Sender.ProfileImageUrl,
            Body = message.IsDeleted ? string.Empty : message.Body,
            SentAt = message.SentAt,
            EditedAt = message.EditedAt,
            IsMine = message.SenderUserId == currentUserId,
            Attachments = message.Attachments
                .Select(attachment => new MessageAttachmentDto
                {
                    Id = attachment.Id,
                    FileName = attachment.FileName,
                    FileUrl = attachment.FileUrl,
                    FileType = attachment.FileType
                })
                .ToList()
        };
}

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------
public sealed record GetConversationsQuery : PagedRequest, IRequest<PaginatedList<ConversationDto>>
{
    public bool? UnreadOnly { get; init; }
}

public sealed class GetConversationsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetConversationsQuery, PaginatedList<ConversationDto>>
{
    public Task<PaginatedList<ConversationDto>> Handle(
        GetConversationsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        return context.Conversations
            .AsNoTracking()
            .Where(conversation => conversation.Participants.Any(participant =>
                participant.UserId == userId && participant.LeftAt == null))
            .WhereIf(query.UnreadOnly == true, conversation => conversation.Participants.Any(participant =>
                participant.UserId == userId && participant.UnreadCount > 0))
            .OrderByDescending(conversation => conversation.LastMessageAt ?? conversation.CreatedAt)
            .Select(conversation => new ConversationDto
            {
                Id = conversation.Id,
                TenantId = conversation.TenantId,
                Type = conversation.Type,
                Subject = conversation.Subject,
                GroupId = conversation.GroupId,
                GroupName = conversation.Group != null ? conversation.Group.Name : null,
                LastMessageAt = conversation.LastMessageAt,
                LastMessagePreview = conversation.LastMessagePreview,
                CreatedAt = conversation.CreatedAt,
                UnreadCount = conversation.Participants
                    .Where(participant => participant.UserId == userId)
                    .Select(participant => participant.UnreadCount)
                    .FirstOrDefault(),
                Participants = conversation.Participants
                    .Where(participant => participant.LeftAt == null)
                    .Select(participant => new ConversationParticipantDto
                    {
                        UserId = participant.UserId,
                        DisplayName = participant.User.DisplayName,
                        ProfileImageUrl = participant.User.ProfileImageUrl,
                        LastReadAt = participant.LastReadAt
                    })
                    .ToList()
            })
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

public sealed record GetConversationQuery : PagedRequest, IRequest<PaginatedList<MessageDto>>
{
    public Guid ConversationId { get; init; }
}

public sealed class GetConversationQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetConversationQuery, PaginatedList<MessageDto>>
{
    public async Task<PaginatedList<MessageDto>> Handle(
        GetConversationQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        await MessagingGuard.LoadParticipatingAsync(
            context, query.ConversationId, userId, currentUser.IsAdmin, cancellationToken);

        return await context.Messages
            .AsNoTracking()
            .Where(message => message.ConversationId == query.ConversationId)
            .OrderByDescending(message => message.SentAt)
            .Select(MessagingProjections.ToMessageDto(userId))
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------
public sealed record StartConversationCommand : IRequest<ConversationDto>
{
    public Guid TenantId { get; init; }

    public ConversationType Type { get; init; } = ConversationType.StudentTeacher;

    public string? Subject { get; init; }

    public Guid? GroupId { get; init; }

    /// <summary>Other participants. The caller is always added automatically.</summary>
    public IReadOnlyList<Guid> ParticipantUserIds { get; init; } = [];

    public string? FirstMessage { get; init; }
}

public sealed class StartConversationCommandValidator : AbstractValidator<StartConversationCommand>
{
    public StartConversationCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Subject).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.FirstMessage).MaximumLength(DomainRules.DescriptionMaxLength);

        RuleFor(command => command.ParticipantUserIds)
            .NotEmpty().WithMessage("A conversation needs at least one other participant.");
    }
}

public sealed class StartConversationCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    IMediator mediator,
    INotificationDispatcher notifications) : IRequestHandler<StartConversationCommand, ConversationDto>
{
    public async Task<ConversationDto> Handle(StartConversationCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var utcNow = dateTimeProvider.UtcNow;

        await planLimits.EnsureFeatureAsync(command.TenantId, PlanFeatures.Messaging, cancellationToken);

        // Everyone in the thread must have a genuine relationship with the workspace.
        var participantIds = command.ParticipantUserIds
            .Append(userId)
            .Distinct()
            .ToList();

        foreach (var participantId in participantIds)
        {
            if (!await IsRelatedToTenantAsync(participantId, command.TenantId, cancellationToken))
            {
                throw new ForbiddenAccessException(
                    "Every participant must belong to this workspace as a member, student or guardian.");
            }
        }

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            TenantId = command.TenantId,
            Type = command.Type,
            Subject = command.Subject?.Trim(),
            GroupId = command.GroupId,
            CreatedByUserId = userId,
            CreatedAt = utcNow
        };

        foreach (var participantId in participantIds)
        {
            conversation.Participants.Add(new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                TenantId = command.TenantId,
                UserId = participantId,
                JoinedAt = utcNow,
                CreatedAt = utcNow
            });
        }

        await context.Conversations.AddAsync(conversation, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(command.FirstMessage))
        {
            await mediator.Send(
                new SendMessageCommand { ConversationId = conversation.Id, Body = command.FirstMessage },
                cancellationToken);
        }
        else
        {
            // With no opening message there is nothing for the message notification to
            // carry, so the invitation itself is the news.
            var starterName = await context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.DisplayName)
                .FirstOrDefaultAsync(cancellationToken) ?? "مستخدم";

            await notifications.SendManyAsync(
                participantIds.Where(id => id != userId),
                new NotificationRequest
                {
                    Event = NotificationEvents.ConversationStarted,
                    TenantId = conversation.TenantId,
                    ReferenceId = conversation.Id,
                    DeduplicationKey = "conversation-started:" + conversation.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["sender"] = starterName,
                        ["conversationId"] = conversation.Id.ToString()
                    }
                },
                cancellationToken);
        }

        return await context.Conversations
            .AsNoTracking()
            .Where(entity => entity.Id == conversation.Id)
            .Select(entity => new ConversationDto
            {
                Id = entity.Id,
                TenantId = entity.TenantId,
                Type = entity.Type,
                Subject = entity.Subject,
                GroupId = entity.GroupId,
                LastMessageAt = entity.LastMessageAt,
                LastMessagePreview = entity.LastMessagePreview,
                CreatedAt = entity.CreatedAt,
                Participants = entity.Participants
                    .Select(participant => new ConversationParticipantDto
                    {
                        UserId = participant.UserId,
                        DisplayName = participant.User.DisplayName,
                        ProfileImageUrl = participant.User.ProfileImageUrl,
                        LastReadAt = participant.LastReadAt
                    })
                    .ToList()
            })
            .FirstAsync(cancellationToken);
    }

    /// <summary>A user belongs to a workspace as a member, an enrolled student, or a guardian of one.</summary>
    private async Task<bool> IsRelatedToTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var isMember = await context.TenantMembers.AnyAsync(
            member => member.TenantId == tenantId
                && member.UserId == userId
                && member.Status == TenantMemberStatus.Active,
            cancellationToken);

        if (isMember)
        {
            return true;
        }

        var isStudent = await context.TenantStudents.AnyAsync(
            link => link.TenantId == tenantId && link.Student.UserId == userId && link.IsActive,
            cancellationToken);

        if (isStudent)
        {
            return true;
        }

        return await context.ParentStudentLinks.AnyAsync(
            link => link.Parent.UserId == userId
                && link.IsConfirmed
                && context.TenantStudents.Any(student =>
                    student.TenantId == tenantId && student.StudentId == link.StudentId && student.IsActive),
            cancellationToken);
    }
}

public sealed record SendMessageCommand : IRequest<MessageDto>
{
    public Guid ConversationId { get; init; }

    public required string Body { get; init; }
}

public sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(command => command.ConversationId).NotEmpty();
        RuleFor(command => command.Body)
            .NotEmpty().WithMessage("A message cannot be empty.")
            .MaximumLength(DomainRules.DescriptionMaxLength);
    }
}

public sealed class SendMessageCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications) : IRequestHandler<SendMessageCommand, MessageDto>
{
    public async Task<MessageDto> Handle(SendMessageCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var conversation = await MessagingGuard.LoadParticipatingAsync(
            context, command.ConversationId, userId, isAdmin: false, cancellationToken);

        var utcNow = dateTimeProvider.UtcNow;
        var body = command.Body.Trim();

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TenantId = conversation.TenantId,
            SenderUserId = userId,
            Body = body,
            SentAt = utcNow,
            CreatedAt = utcNow
        };

        conversation.LastMessageAt = utcNow;
        conversation.LastMessagePreview = body.Length > 200 ? body[..200] : body;
        conversation.UpdatedAt = utcNow;

        foreach (var participant in conversation.Participants.Where(entry => !entry.HasLeft))
        {
            if (participant.UserId == userId)
            {
                participant.LastReadAt = utcNow;
                participant.UnreadCount = 0;
            }
            else
            {
                participant.UnreadCount++;
            }

            participant.UpdatedAt = utcNow;
        }

        await context.Messages.AddAsync(message, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Someone whose unread count is still zero has the thread open in front of them;
        // pushing a "new message" card at them would be noise.
        var recipients = conversation.Participants
            .Where(entry => !entry.HasLeft && entry.UserId != userId && entry.UnreadCount <= 1)
            .Select(entry => entry.UserId)
            .ToList();

        var senderName = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "مستخدم";

        await notifications.SendManyAsync(
            recipients,
            new NotificationRequest
            {
                Event = NotificationEvents.MessageReceived,
                TenantId = conversation.TenantId,
                ReferenceId = message.Id,
                Values = new Dictionary<string, string>
                {
                    ["sender"] = senderName,
                    ["preview"] = conversation.LastMessagePreview ?? "وصلتك رسالة جديدة.",
                    ["conversationId"] = conversation.Id.ToString()
                }
            },
            cancellationToken);

        return await context.Messages
            .AsNoTracking()
            .Where(entity => entity.Id == message.Id)
            .Select(MessagingProjections.ToMessageDto(userId))
            .FirstAsync(cancellationToken);
    }
}

public sealed record MarkMessageReadCommand(Guid MessageId) : IRequest<Unit>;

public sealed class MarkMessageReadCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<MarkMessageReadCommand, Unit>
{
    public async Task<Unit> Handle(MarkMessageReadCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var message = await context.Messages
            .AsNoTracking()
            .Where(entity => entity.Id == command.MessageId)
            .Select(entity => new { entity.ConversationId, entity.SentAt })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Message", command.MessageId);

        var conversation = await MessagingGuard.LoadParticipatingAsync(
            context, message.ConversationId, userId, isAdmin: false, cancellationToken);

        var participant = conversation.Participants.First(entry => entry.UserId == userId);

        participant.LastReadAt = message.SentAt > (participant.LastReadAt ?? DateTime.MinValue)
            ? message.SentAt
            : participant.LastReadAt;
        participant.UnreadCount = 0;
        participant.UpdatedAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

/// <summary>Unread totals for the message badge in the client shell.</summary>
public sealed record UnreadMessagesDto
{
    /// <summary>Unread messages across every conversation the caller still participates in.</summary>
    public int TotalUnread { get; init; }

    /// <summary>How many conversations carry at least one unread message.</summary>
    public int ConversationsWithUnread { get; init; }
}

public sealed record GetUnreadMessagesCountQuery : IRequest<UnreadMessagesDto>;

public sealed class GetUnreadMessagesCountQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetUnreadMessagesCountQuery, UnreadMessagesDto>
{
    public async Task<UnreadMessagesDto> Handle(
        GetUnreadMessagesCountQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        // Only live memberships count: a participant who left keeps their row for history.
        var counts = await context.ConversationParticipants
            .AsNoTracking()
            .Where(participant => participant.UserId == userId && participant.LeftAt == null)
            .Select(participant => participant.UnreadCount)
            .ToListAsync(cancellationToken);

        return new UnreadMessagesDto
        {
            TotalUnread = counts.Sum(),
            ConversationsWithUnread = counts.Count(count => count > 0)
        };
    }
}
