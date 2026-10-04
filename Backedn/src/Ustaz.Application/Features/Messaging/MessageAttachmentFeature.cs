using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Messaging;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Messaging;

/// <summary>A file handed back for download, with the bytes still streaming.</summary>
public sealed record AttachmentDownloadDto
{
    public required Stream Content { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }
}

// ---------------------------------------------------------------------------
// Sending a file
// ---------------------------------------------------------------------------

/// <summary>
/// Posts a file into a conversation. The file goes to private storage, never to a public
/// URL, and the message that carries it follows exactly the same participation rules as a
/// text message.
/// </summary>
public sealed record SendMessageAttachmentCommand : IRequest<MessageDto>
{
    public Guid ConversationId { get; init; }

    public required FileUploadRequest File { get; init; }

    /// <summary>Optional caption sent with the file.</summary>
    public string? Body { get; init; }
}

public sealed class SendMessageAttachmentCommandValidator : AbstractValidator<SendMessageAttachmentCommand>
{
    public SendMessageAttachmentCommandValidator()
    {
        RuleFor(command => command.ConversationId).NotEmpty();
        RuleFor(command => command.Body).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.File).ValidDocumentUpload();
    }
}

public sealed class SendMessageAttachmentCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IFileStorage fileStorage,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications) : IRequestHandler<SendMessageAttachmentCommand, MessageDto>
{
    public async Task<MessageDto> Handle(
        SendMessageAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        // Non-participants get the same 404 they get for reading the thread.
        var conversation = await MessagingGuard.LoadParticipatingAsync(
            context, command.ConversationId, userId, isAdmin: false, cancellationToken);

        await planLimits.EnsureFeatureAsync(conversation.TenantId, PlanFeatures.Messaging, cancellationToken);

        var utcNow = dateTimeProvider.UtcNow;
        var caption = command.Body?.Trim();

        // Store the file first: a failed upload must not leave an empty message behind.
        var stored = await fileStorage.SaveAsync(
            command.File,
            new FileStorageScope
            {
                Area = FileStorageArea.Messages,
                TenantId = conversation.TenantId,
                OwnerId = conversation.Id
            },
            cancellationToken);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            TenantId = conversation.TenantId,
            SenderUserId = userId,
            Body = string.IsNullOrWhiteSpace(caption) ? stored.FileName : caption,
            SentAt = utcNow,
            CreatedAt = utcNow
        };

        message.Attachments.Add(new MessageAttachment
        {
            Id = Guid.NewGuid(),
            MessageId = message.Id,
            TenantId = conversation.TenantId,
            FileName = stored.FileName,
            // The storage key is the handle, not a URL: reads go through the download
            // endpoint so every access is authorized.
            FileUrl = stored.StorageKey,
            FilePublicId = stored.StorageKey,
            FileType = ResolveFileType(stored.FileName),
            FileSizeInBytes = stored.SizeInBytes,
            CreatedAt = utcNow
        });

        var preview = "📎 " + stored.FileName;

        conversation.LastMessageAt = utcNow;
        conversation.LastMessagePreview = preview.Length > 200 ? preview[..200] : preview;
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

        try
        {
            await context.Messages.AddAsync(message, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // The row never landed, so the file it pointed at is an orphan: drop it.
            await fileStorage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }

        var senderName = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "مستخدم";

        var recipients = conversation.Participants
            .Where(entry => !entry.HasLeft && entry.UserId != userId && entry.UnreadCount <= 1)
            .Select(entry => entry.UserId)
            .ToList();

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
                    ["preview"] = preview,
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

    /// <summary>Classifies by extension only; the client's content type is not trusted.</summary>
    private static MaterialFileType ResolveFileType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" => MaterialFileType.Image,
            ".mp4" => MaterialFileType.Video,
            ".mp3" or ".m4a" => MaterialFileType.Audio,
            _ => MaterialFileType.Document
        };
}

// ---------------------------------------------------------------------------
// Reading a file
// ---------------------------------------------------------------------------

/// <summary>
/// Streams one attachment. Authorization is by conversation membership, re-resolved from the
/// database on every request, so holding an attachment id proves nothing on its own.
/// </summary>
public sealed record DownloadMessageAttachmentQuery(Guid AttachmentId) : IRequest<AttachmentDownloadDto>;

public sealed class DownloadMessageAttachmentQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IFileStorage fileStorage) : IRequestHandler<DownloadMessageAttachmentQuery, AttachmentDownloadDto>
{
    public async Task<AttachmentDownloadDto> Handle(
        DownloadMessageAttachmentQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var attachment = await context.MessageAttachments
            .AsNoTracking()
            .Where(entity => entity.Id == query.AttachmentId)
            .Select(entity => new
            {
                entity.Id,
                entity.FileName,
                entity.FileUrl,
                entity.TenantId,
                entity.Message.ConversationId
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("MessageAttachment", query.AttachmentId);

        var participates = await context.ConversationParticipants
            .AsNoTracking()
            .AnyAsync(
                participant => participant.ConversationId == attachment.ConversationId
                    && participant.UserId == userId
                    && participant.LeftAt == null,
                cancellationToken);

        if (!participates)
        {
            // Not a participant: the attachment is not merely forbidden, it is invisible.
            throw new NotFoundException("MessageAttachment", query.AttachmentId);
        }

        // Second gate: the key itself has to belong to the conversation's workspace, so a
        // key rewritten to point at another tenant's folder cannot be replayed here.
        if (!fileStorage.BelongsToTenant(attachment.FileUrl, attachment.TenantId))
        {
            throw new NotFoundException("MessageAttachment", query.AttachmentId);
        }

        var content = await fileStorage.OpenAsync(attachment.FileUrl, cancellationToken)
            ?? throw new NotFoundException("MessageAttachment", query.AttachmentId);

        return new AttachmentDownloadDto
        {
            Content = content,
            FileName = attachment.FileName,
            // Always a generic type: the browser must never be told to render an upload.
            ContentType = "application/octet-stream"
        };
    }
}
