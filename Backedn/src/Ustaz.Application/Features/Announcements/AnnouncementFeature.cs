using System.Linq.Expressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Announcements;

public sealed record AnnouncementDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string TenantName { get; init; } = string.Empty;

    public Guid? GroupId { get; init; }

    public string? GroupName { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public AnnouncementAudience Audience { get; init; }

    public string AudienceName => Audience.ToString();

    public bool IsPinned { get; init; }

    public bool IsPublished { get; init; }

    public DateTime? PublishedAt { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public string? ImageUrl { get; init; }

    public string? AttachmentUrl { get; init; }

    public DateTime CreatedAt { get; init; }
}

internal static class AnnouncementProjections
{
    public static Expression<Func<Announcement, AnnouncementDto>> ToDto =>
        announcement => new AnnouncementDto
        {
            Id = announcement.Id,
            TenantId = announcement.TenantId,
            TenantName = announcement.Tenant.Name,
            GroupId = announcement.GroupId,
            GroupName = announcement.Group != null ? announcement.Group.Name : null,
            Title = announcement.Title,
            Body = announcement.Body,
            Audience = announcement.Audience,
            IsPinned = announcement.IsPinned,
            IsPublished = announcement.IsPublished,
            PublishedAt = announcement.PublishedAt,
            ExpiresAt = announcement.ExpiresAt,
            ImageUrl = announcement.ImageUrl,
            AttachmentUrl = announcement.AttachmentUrl,
            CreatedAt = announcement.CreatedAt
        };
}

public sealed record CreateAnnouncementCommand : IRequest<AnnouncementDto>
{
    public required string Title { get; init; }

    public required string Body { get; init; }

    public AnnouncementAudience Audience { get; init; } = AnnouncementAudience.TenantWide;

    public Guid? GroupId { get; init; }

    public bool IsPinned { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public string? ImageUrl { get; init; }

    public string? AttachmentUrl { get; init; }
}

public sealed class CreateAnnouncementCommandValidator : AbstractValidator<CreateAnnouncementCommand>
{
    public CreateAnnouncementCommandValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Body).NotEmpty().MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.Audience).IsInEnum();
        RuleFor(command => command.ImageUrl).OptionalHttpUrl();
        RuleFor(command => command.AttachmentUrl).OptionalHttpUrl();

        RuleFor(command => command.GroupId)
            .NotNull()
            .When(command => command.Audience == AnnouncementAudience.Group)
            .WithMessage("A group announcement must name the group it targets.");
    }
}

public sealed class CreateAnnouncementCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    IUrlGuard urlGuard,
    IMediator mediator) : IRequestHandler<CreateAnnouncementCommand, AnnouncementDto>
{
    public async Task<AnnouncementDto> Handle(
        CreateAnnouncementCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AnnouncementsManage, cancellationToken: cancellationToken);

        await planLimits.EnsureFeatureAsync(access.TenantId, PlanFeatures.Announcements, cancellationToken);

        if (command.GroupId.HasValue)
        {
            var group = await context.Groups
                .AsNoTracking()
                .Where(entity => entity.Id == command.GroupId.Value)
                .Select(entity => new { entity.TenantId })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Group", command.GroupId.Value);

            tenantAuthorization.EnsureSameTenant(access, group.TenantId);
        }

        foreach (var url in new[] { command.ImageUrl, command.AttachmentUrl })
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                urlGuard.EnsureSafeHttpUrl(url, "Announcement URL");
            }
        }

        var announcement = new Announcement
        {
            Id = Guid.NewGuid(),
            TenantId = access.TenantId,
            CreatedByUserId = access.UserId,
            GroupId = command.GroupId,
            Title = command.Title.Trim(),
            Body = command.Body.Trim(),
            Audience = command.Audience,
            IsPinned = command.IsPinned,
            ExpiresAt = command.ExpiresAt.HasValue
                ? DateTime.SpecifyKind(command.ExpiresAt.Value, DateTimeKind.Utc)
                : null,
            ImageUrl = command.ImageUrl?.Trim(),
            AttachmentUrl = command.AttachmentUrl?.Trim(),
            CreatedAt = dateTimeProvider.UtcNow
        };

        await context.Announcements.AddAsync(announcement, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return await mediator.Send(new GetAnnouncementByIdQuery(announcement.Id), cancellationToken);
    }
}

public sealed record UpdateAnnouncementCommand : IRequest<AnnouncementDto>
{
    public Guid AnnouncementId { get; init; }

    public string? Title { get; init; }

    public string? Body { get; init; }

    public bool? IsPinned { get; init; }

    public DateTime? ExpiresAt { get; init; }
}

public sealed class UpdateAnnouncementCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IMediator mediator) : IRequestHandler<UpdateAnnouncementCommand, AnnouncementDto>
{
    public async Task<AnnouncementDto> Handle(
        UpdateAnnouncementCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AnnouncementsManage, cancellationToken: cancellationToken);

        var announcement = await LoadAsync(context, tenantAuthorization, access, command.AnnouncementId, cancellationToken);

        if (command.Title is not null)
        {
            announcement.Title = command.Title.Trim();
        }

        if (command.Body is not null)
        {
            announcement.Body = command.Body.Trim();
        }

        if (command.IsPinned.HasValue)
        {
            announcement.IsPinned = command.IsPinned.Value;
        }

        if (command.ExpiresAt.HasValue)
        {
            announcement.ExpiresAt = DateTime.SpecifyKind(command.ExpiresAt.Value, DateTimeKind.Utc);
        }

        announcement.UpdatedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return await mediator.Send(new GetAnnouncementByIdQuery(announcement.Id), cancellationToken);
    }

    internal static async Task<Announcement> LoadAsync(
        IApplicationDbContext context,
        ITenantAuthorizationService tenantAuthorization,
        TenantAccess access,
        Guid announcementId,
        CancellationToken cancellationToken)
    {
        var announcement = await context.Announcements
            .FirstOrDefaultAsync(entity => entity.Id == announcementId, cancellationToken)
            ?? throw new NotFoundException("Announcement", announcementId);

        tenantAuthorization.EnsureSameTenant(access, announcement.TenantId);

        return announcement;
    }
}

public sealed record PublishAnnouncementCommand(Guid AnnouncementId) : IRequest<Unit>;

public sealed class PublishAnnouncementCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications) : IRequestHandler<PublishAnnouncementCommand, Unit>
{
    public async Task<Unit> Handle(PublishAnnouncementCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AnnouncementsManage, cancellationToken: cancellationToken);

        var announcement = await UpdateAnnouncementCommandHandler.LoadAsync(
            context, tenantAuthorization, access, command.AnnouncementId, cancellationToken);

        announcement.Publish(dateTimeProvider.UtcNow);
        await context.SaveChangesAsync(cancellationToken);

        var recipients = await ResolveRecipientsAsync(announcement, cancellationToken);

        await notifications.SendManyAsync(
            recipients,
            new NotificationRequest
            {
                Event = NotificationEvents.AnnouncementPublished,
                TenantId = access.TenantId,
                ReferenceId = announcement.Id,
                // Publishing twice is a correction, not a second announcement.
                DeduplicationKey = "announcement:" + announcement.Id,
                Values = new Dictionary<string, string>
                {
                    ["title"] = announcement.Title,
                    ["announcementId"] = announcement.Id.ToString()
                }
            },
            cancellationToken);

        return Unit.Value;
    }

    /// <summary>Resolves the user ids the announcement's audience maps to.</summary>
    private async Task<IReadOnlyList<Guid>> ResolveRecipientsAsync(
        Announcement announcement,
        CancellationToken cancellationToken)
    {
        var studentUserIds = announcement.Audience switch
        {
            AnnouncementAudience.Group when announcement.GroupId.HasValue => await context.GroupEnrollments
                .AsNoTracking()
                .Where(enrollment => enrollment.GroupId == announcement.GroupId.Value
                    && enrollment.Status == EnrollmentStatus.Active)
                .Select(enrollment => enrollment.Student.UserId)
                .ToListAsync(cancellationToken),

            _ => await context.TenantStudents
                .AsNoTracking()
                .Where(link => link.TenantId == announcement.TenantId && link.IsActive)
                .Select(link => link.Student.UserId)
                .ToListAsync(cancellationToken)
        };

        if (announcement.Audience == AnnouncementAudience.Students)
        {
            return studentUserIds;
        }

        var studentIds = await context.TenantStudents
            .AsNoTracking()
            .Where(link => link.TenantId == announcement.TenantId && link.IsActive)
            .Select(link => link.StudentId)
            .ToListAsync(cancellationToken);

        var guardianUserIds = await context.ParentStudentLinks
            .AsNoTracking()
            .Where(link => studentIds.Contains(link.StudentId) && link.IsConfirmed)
            .Select(link => link.Parent.UserId)
            .ToListAsync(cancellationToken);

        return announcement.Audience == AnnouncementAudience.Parents
            ? guardianUserIds
            : [.. studentUserIds.Concat(guardianUserIds)];
    }
}

public sealed record DeleteAnnouncementCommand(Guid AnnouncementId) : IRequest<Unit>;

public sealed class DeleteAnnouncementCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<DeleteAnnouncementCommand, Unit>
{
    public async Task<Unit> Handle(DeleteAnnouncementCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AnnouncementsManage, cancellationToken: cancellationToken);

        var announcement = await UpdateAnnouncementCommandHandler.LoadAsync(
            context, tenantAuthorization, access, command.AnnouncementId, cancellationToken);

        context.Announcements.Remove(announcement);
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------
public sealed record GetAnnouncementByIdQuery(Guid AnnouncementId) : IRequest<AnnouncementDto>;

public sealed class GetAnnouncementByIdQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetAnnouncementByIdQuery, AnnouncementDto>
{
    public async Task<AnnouncementDto> Handle(
        GetAnnouncementByIdQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AnnouncementsView, cancellationToken: cancellationToken);

        var announcement = await context.Announcements
            .AsNoTracking()
            .Where(entity => entity.Id == query.AnnouncementId)
            .Select(AnnouncementProjections.ToDto)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Announcement", query.AnnouncementId);

        tenantAuthorization.EnsureSameTenant(access, announcement.TenantId);

        return announcement;
    }
}

public sealed record GetAnnouncementsQuery : PagedRequest, IRequest<PaginatedList<AnnouncementDto>>
{
    public Guid? GroupId { get; init; }

    public AnnouncementAudience? Audience { get; init; }

    public bool? IsPublished { get; init; }
}

public sealed class GetAnnouncementsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetAnnouncementsQuery, PaginatedList<AnnouncementDto>>
{
    public async Task<PaginatedList<AnnouncementDto>> Handle(
        GetAnnouncementsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AnnouncementsView, cancellationToken: cancellationToken);

        return await context.Announcements
            .AsNoTracking()
            .Where(announcement => announcement.TenantId == access.TenantId)
            .WhereIf(query.GroupId.HasValue, announcement => announcement.GroupId == query.GroupId!.Value)
            .WhereIf(query.Audience.HasValue, announcement => announcement.Audience == query.Audience!.Value)
            .WhereIf(query.IsPublished.HasValue, announcement => announcement.IsPublished == query.IsPublished!.Value)
            .OrderByDescending(announcement => announcement.IsPinned)
            .ThenByDescending(announcement => announcement.PublishedAt ?? announcement.CreatedAt)
            .Select(AnnouncementProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>Announcements the signed-in student can see, across the workspaces they study in.</summary>
public sealed record GetMyAnnouncementsQuery : PagedRequest, IRequest<PaginatedList<AnnouncementDto>>;

public sealed class GetMyAnnouncementsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyAnnouncementsQuery, PaginatedList<AnnouncementDto>>
{
    public Task<PaginatedList<AnnouncementDto>> Handle(
        GetMyAnnouncementsQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();
        var utcNow = dateTimeProvider.UtcNow;

        return context.Announcements
            .AsNoTracking()
            .Where(announcement => announcement.IsPublished
                && (announcement.ExpiresAt == null || announcement.ExpiresAt > utcNow)
                && announcement.Audience != AnnouncementAudience.Parents
                && context.TenantStudents.Any(link =>
                    link.TenantId == announcement.TenantId && link.StudentId == studentId && link.IsActive)
                && (announcement.GroupId == null
                    || context.GroupEnrollments.Any(enrollment =>
                        enrollment.GroupId == announcement.GroupId
                        && enrollment.StudentId == studentId
                        && enrollment.Status == EnrollmentStatus.Active)))
            .OrderByDescending(announcement => announcement.IsPinned)
            .ThenByDescending(announcement => announcement.PublishedAt)
            .Select(AnnouncementProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>Announcements visible to the signed-in parent, based on their children's workspaces.</summary>
public sealed record GetParentAnnouncementsQuery : PagedRequest, IRequest<PaginatedList<AnnouncementDto>>;

public sealed class GetParentAnnouncementsQueryHandler(
    IApplicationDbContext context,
    IParentAccessGuard parentAccess,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetParentAnnouncementsQuery, PaginatedList<AnnouncementDto>>
{
    public async Task<PaginatedList<AnnouncementDto>> Handle(
        GetParentAnnouncementsQuery query,
        CancellationToken cancellationToken)
    {
        var childIds = await parentAccess.LinkedStudentIdsAsync(cancellationToken);
        var utcNow = dateTimeProvider.UtcNow;

        if (childIds.Count == 0)
        {
            return PaginatedList<AnnouncementDto>.Empty(query.Page, query.PageSize);
        }

        return await context.Announcements
            .AsNoTracking()
            .Where(announcement => announcement.IsPublished
                && (announcement.ExpiresAt == null || announcement.ExpiresAt > utcNow)
                && announcement.Audience != AnnouncementAudience.Students
                && context.TenantStudents.Any(link =>
                    link.TenantId == announcement.TenantId
                    && childIds.Contains(link.StudentId)
                    && link.IsActive))
            .OrderByDescending(announcement => announcement.IsPinned)
            .ThenByDescending(announcement => announcement.PublishedAt)
            .Select(AnnouncementProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}
