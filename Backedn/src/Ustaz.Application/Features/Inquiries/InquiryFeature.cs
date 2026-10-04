using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Events;
using Ustaz.Domain.Exceptions;
using Ustaz.Domain.ValueObjects;

namespace Ustaz.Application.Features.Inquiries;

public sealed record InquiryDto
{
    public Guid Id { get; init; }

    public Guid? StudentId { get; init; }

    public Guid TeacherId { get; init; }

    public string TeacherName { get; init; } = string.Empty;

    public Guid? GroupId { get; init; }

    public string? GroupName { get; init; }

    public Guid? CourseId { get; init; }

    public string? CourseTitle { get; init; }

    public string Name { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string Message { get; init; } = string.Empty;

    public InquiryStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public string? TeacherNote { get; init; }

    public string? WhatsAppLink { get; init; }

    public DateTime? RespondedAt { get; init; }

    public DateTime CreatedAt { get; init; }
}

/// <summary>Sends a lead to a teacher. Guests may use this too, which is why authentication is optional.</summary>
public sealed record CreateInquiryCommand : IRequest<InquiryDto>
{
    public Guid TeacherId { get; init; }

    public Guid? GroupId { get; init; }

    public Guid? CourseId { get; init; }

    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    public required string Message { get; init; }
}

public sealed class CreateInquiryCommandValidator : AbstractValidator<CreateInquiryCommand>
{
    public CreateInquiryCommandValidator()
    {
        RuleFor(command => command.TeacherId).NotEmpty();
        RuleFor(command => command.Name).PersonName();
        RuleFor(command => command.PhoneNumber).EgyptianPhone();
        RuleFor(command => command.Email).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.Email));

        RuleFor(command => command.Message)
            .NotEmpty().WithMessage("A message is required.")
            .MinimumLength(10).WithMessage("Please write at least 10 characters so the teacher can help you.")
            .MaximumLength(DomainRules.DescriptionMaxLength);
    }
}

public sealed class CreateInquiryCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications) : IRequestHandler<CreateInquiryCommand, InquiryDto>
{
    public async Task<InquiryDto> Handle(CreateInquiryCommand command, CancellationToken cancellationToken)
    {
        var teacher = await context.TeacherProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == command.TeacherId)
            .Select(profile => new { profile.Id, profile.UserId, profile.TenantId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Teacher", command.TeacherId);

        if (command.GroupId.HasValue
            && !await context.Groups.AnyAsync(
                group => group.Id == command.GroupId.Value && group.TeacherId == teacher.Id, cancellationToken))
        {
            throw new NotFoundException("Group", command.GroupId.Value);
        }

        if (command.CourseId.HasValue
            && !await context.Courses.AnyAsync(
                course => course.Id == command.CourseId.Value && course.TeacherId == teacher.Id, cancellationToken))
        {
            throw new NotFoundException("Course", command.CourseId.Value);
        }

        var inquiry = new Inquiry
        {
            Id = Guid.NewGuid(),
            StudentId = currentUser.StudentId,
            TenantId = teacher.TenantId,
            TeacherId = teacher.Id,
            GroupId = command.GroupId,
            CourseId = command.CourseId,
            Name = command.Name.Trim(),
            PhoneNumber = EgyptianPhoneNumber.Create(command.PhoneNumber).Value,
            Email = command.Email?.Trim().ToLowerInvariant(),
            Message = command.Message.Trim(),
            Status = InquiryStatus.New,
            CreatedAt = dateTimeProvider.UtcNow
        };

        inquiry.RaiseDomainEvent(new InquiryCreatedEvent(inquiry.Id, teacher.Id, inquiry.Name));

        await context.Inquiries.AddAsync(inquiry, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await notifications.SendAsync(
            teacher.UserId,
            new NotificationRequest
            {
                Event = NotificationEvents.InquiryReceived,
                ReferenceId = inquiry.Id,
                Values = new Dictionary<string, string>
                {
                    ["sender"] = inquiry.Name,
                    ["inquiryId"] = inquiry.Id.ToString()
                }
            },
            cancellationToken);

        return await InquiryProjection.LoadAsync(context, inquiry.Id, cancellationToken);
    }
}

internal static class InquiryProjection
{
    public static System.Linq.Expressions.Expression<Func<Inquiry, InquiryDto>> Projection =>
        inquiry => new InquiryDto
        {
            Id = inquiry.Id,
            StudentId = inquiry.StudentId,
            TeacherId = inquiry.TeacherId,
            TeacherName = inquiry.Teacher.User.DisplayName,
            GroupId = inquiry.GroupId,
            GroupName = inquiry.Group != null ? inquiry.Group.Name : null,
            CourseId = inquiry.CourseId,
            CourseTitle = inquiry.Course != null ? inquiry.Course.Title : null,
            Name = inquiry.Name,
            PhoneNumber = inquiry.PhoneNumber,
            Email = inquiry.Email,
            Message = inquiry.Message,
            Status = inquiry.Status,
            TeacherNote = inquiry.TeacherNote,
            RespondedAt = inquiry.RespondedAt,
            CreatedAt = inquiry.CreatedAt
        };

    public static Task<InquiryDto> LoadAsync(
        IApplicationDbContext context,
        Guid inquiryId,
        CancellationToken cancellationToken) =>
        context.Inquiries
            .AsNoTracking()
            .Where(inquiry => inquiry.Id == inquiryId)
            .Select(Projection)
            .FirstAsync(cancellationToken);
}

/// <summary>Inquiries addressed to the calling teacher.</summary>
/// <summary>
/// Inquiries for one teacher's inbox, or — when a platform administrator asks — for the whole
/// platform. Two callers, one projection: the admin screen showed nothing at all while this
/// handler insisted on a teacher profile the administrator does not have.
/// </summary>
public sealed record GetTeacherInquiriesQuery : PagedRequest, IRequest<PaginatedList<InquiryDto>>
{
    public InquiryStatus? Status { get; init; }

    public Guid? GroupId { get; init; }

    /// <summary>
    /// Narrows a platform-wide listing to one teacher. Ignored for a teacher caller, who is
    /// always pinned to their own inbox regardless of what the query string says.
    /// </summary>
    public Guid? TeacherId { get; init; }
}

public sealed class GetTeacherInquiriesQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IWhatsAppLinkService whatsAppLinkService) : IRequestHandler<GetTeacherInquiriesQuery, PaginatedList<InquiryDto>>
{
    public async Task<PaginatedList<InquiryDto>> Handle(
        GetTeacherInquiriesQuery query,
        CancellationToken cancellationToken)
    {
        // A teacher is pinned to their own inbox; an administrator sees the platform and may
        // filter down to one teacher. Anyone else is refused by RequireTeacherId as before.
        var scopedTeacherId = currentUser.IsAdmin && currentUser.TeacherId is null
            ? query.TeacherId
            : currentUser.RequireTeacherId();

        var search = query.Search?.Trim();

        var page = await context.Inquiries
            .AsNoTracking()
            .WhereIf(scopedTeacherId.HasValue, inquiry => inquiry.TeacherId == scopedTeacherId!.Value)
            .WhereIf(query.Status.HasValue, inquiry => inquiry.Status == query.Status!.Value)
            .WhereIf(query.GroupId.HasValue, inquiry => inquiry.GroupId == query.GroupId!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(search), inquiry =>
                inquiry.Name.Contains(search!) || inquiry.PhoneNumber.Contains(search!))
            .OrderByDescending(inquiry => inquiry.CreatedAt)
            .Select(InquiryProjection.Projection)
            .ToPaginatedListAsync(query, cancellationToken);

        // The teacher gets a ready-to-click WhatsApp link back to the person who wrote in.
        return page.Map(inquiry => inquiry with
        {
            WhatsAppLink = whatsAppLinkService.BuildChatLink(
                inquiry.PhoneNumber,
                "السلام عليكم " + inquiry.Name + "، بخصوص استفسارك على منصة أستاذ.")
        });
    }
}

public sealed record GetInquiryByIdQuery(Guid InquiryId) : IRequest<InquiryDto>;

public sealed class GetInquiryByIdQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IWhatsAppLinkService whatsAppLinkService) : IRequestHandler<GetInquiryByIdQuery, InquiryDto>
{
    public async Task<InquiryDto> Handle(GetInquiryByIdQuery query, CancellationToken cancellationToken)
    {
        var inquiry = await context.Inquiries
            .AsNoTracking()
            .Where(entity => entity.Id == query.InquiryId)
            .Select(InquiryProjection.Projection)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Inquiry", query.InquiryId);

        var isTeacherOwner = currentUser.TeacherId is not null && currentUser.TeacherId == inquiry.TeacherId;
        var isStudentOwner = currentUser.StudentId is not null && currentUser.StudentId == inquiry.StudentId;

        if (!isTeacherOwner && !isStudentOwner && !currentUser.IsAdmin)
        {
            throw new ForbiddenAccessException("You can only view your own inquiries.");
        }

        return inquiry with
        {
            WhatsAppLink = isTeacherOwner || currentUser.IsAdmin
                ? whatsAppLinkService.BuildChatLink(inquiry.PhoneNumber)
                : null
        };
    }
}

/// <summary>Inquiries the calling student has sent.</summary>
public sealed record GetStudentInquiriesQuery : PagedRequest, IRequest<PaginatedList<InquiryDto>>;

public sealed class GetStudentInquiriesQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetStudentInquiriesQuery, PaginatedList<InquiryDto>>
{
    public Task<PaginatedList<InquiryDto>> Handle(GetStudentInquiriesQuery query, CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        return context.Inquiries
            .AsNoTracking()
            .Where(inquiry => inquiry.StudentId == studentId)
            .OrderByDescending(inquiry => inquiry.CreatedAt)
            .Select(InquiryProjection.Projection)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

public sealed record UpdateInquiryStatusCommand : IRequest<InquiryDto>
{
    public Guid InquiryId { get; init; }

    public InquiryStatus Status { get; init; }

    public string? Note { get; init; }
}

public sealed class UpdateInquiryStatusCommandValidator : AbstractValidator<UpdateInquiryStatusCommand>
{
    public UpdateInquiryStatusCommandValidator()
    {
        RuleFor(command => command.InquiryId).NotEmpty();
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.Note).MaximumLength(DomainRules.ShortTextMaxLength);
    }
}

public sealed class UpdateInquiryStatusCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications) : IRequestHandler<UpdateInquiryStatusCommand, InquiryDto>
{
    public async Task<InquiryDto> Handle(UpdateInquiryStatusCommand command, CancellationToken cancellationToken)
    {
        var inquiry = await context.Inquiries
            .FirstOrDefaultAsync(entity => entity.Id == command.InquiryId, cancellationToken)
            ?? throw new NotFoundException("Inquiry", command.InquiryId);

        Common.Security.OwnershipGuard.EnsureTeacherOwns(currentUser, inquiry.TeacherId);

        inquiry.ChangeStatus(command.Status, dateTimeProvider.UtcNow, command.Note?.Trim());

        await context.SaveChangesAsync(cancellationToken);

        // The teacher marking an inquiry as contacted is the student's answer. An anonymous
        // inquiry only ever had an email address, and is replied to by email instead.
        if (inquiry.Status == Domain.Enums.InquiryStatus.Contacted && inquiry.StudentId.HasValue)
        {
            var studentUserId = await NotificationAudience.StudentUserIdAsync(
                context, inquiry.StudentId.Value, cancellationToken);

            if (studentUserId != Guid.Empty)
            {
                await notifications.SendAsync(
                    studentUserId,
                    new NotificationRequest
                    {
                        Event = NotificationEvents.InquiryAnswered,
                        ReferenceId = inquiry.Id,
                        DeduplicationKey = "inquiry-answered:" + inquiry.Id
                    },
                    cancellationToken);
            }
        }

        return await InquiryProjection.LoadAsync(context, inquiry.Id, cancellationToken);
    }
}
