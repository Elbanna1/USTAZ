using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Security;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Notifications;

public sealed record NotificationDto
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    /// <summary>English rendering for accounts reading the app in English.</summary>
    public string? TitleEn { get; init; }

    public string? MessageEn { get; init; }

    public NotificationType Type { get; init; }

    public string TypeName => Type.ToString();

    public NotificationCategory Category { get; init; }

    public string CategoryName => Category.ToString();

    public NotificationPriority Priority { get; init; }

    public string PriorityName => Priority.ToString();

    /// <summary>Catalog key, so the client can render per-event UI.</summary>
    public string? Event { get; init; }

    public bool IsRead { get; init; }

    public string? ActionUrl { get; init; }

    public Guid? ReferenceId { get; init; }

    public string? RelatedEntityType { get; init; }

    public Guid? TenantId { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? ReadAt { get; init; }
}

internal static class NotificationProjections
{
    /// <summary>One projection for every read path, so the inbox shape never drifts.</summary>
    public static System.Linq.Expressions.Expression<Func<Domain.Entities.Notification, NotificationDto>> ToDto =>
        notification => new NotificationDto
        {
            Id = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            TitleEn = notification.TitleEn,
            MessageEn = notification.MessageEn,
            Type = notification.Type,
            Category = notification.Category,
            Priority = notification.Priority,
            Event = notification.Event,
            IsRead = notification.IsRead,
            ActionUrl = notification.ActionUrl,
            ReferenceId = notification.ReferenceId,
            RelatedEntityType = notification.RelatedEntityType,
            TenantId = notification.TenantId,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt
        };
}

/// <summary>The caller's notification inbox.</summary>
public sealed record GetNotificationsQuery : PagedRequest, IRequest<PaginatedList<NotificationDto>>
{
    public bool? UnreadOnly { get; init; }

    public NotificationType? Type { get; init; }

    public NotificationCategory? Category { get; init; }

    /// <summary>Only notifications at or above this priority, for a "important only" view.</summary>
    public NotificationPriority? MinimumPriority { get; init; }
}

public sealed class GetNotificationsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetNotificationsQuery, PaginatedList<NotificationDto>>
{
    public Task<PaginatedList<NotificationDto>> Handle(
        GetNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        return context.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .WhereIf(query.UnreadOnly == true, notification => !notification.IsRead)
            .WhereIf(query.Type.HasValue, notification => notification.Type == query.Type!.Value)
            .WhereIf(query.Category.HasValue, notification => notification.Category == query.Category!.Value)
            .WhereIf(
                query.MinimumPriority.HasValue,
                notification => notification.Priority >= query.MinimumPriority!.Value)
            // Newest first, with the id as a tiebreaker so paging never repeats or skips a
            // row when several notifications share a timestamp.
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Select(NotificationProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

public sealed record UnreadCountDto(int Count);

public sealed record GetUnreadNotificationCountQuery : IRequest<UnreadCountDto>;

public sealed class GetUnreadNotificationCountQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetUnreadNotificationCountQuery, UnreadCountDto>
{
    public async Task<UnreadCountDto> Handle(
        GetUnreadNotificationCountQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var count = await context.Notifications
            .AsNoTracking()
            .CountAsync(notification => notification.UserId == userId && !notification.IsRead, cancellationToken);

        return new UnreadCountDto(count);
    }
}

public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest<Unit>;

public sealed class MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator() => RuleFor(command => command.NotificationId).NotEmpty();
}

public sealed class MarkNotificationReadCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<MarkNotificationReadCommand, Unit>
{
    public async Task<Unit> Handle(MarkNotificationReadCommand command, CancellationToken cancellationToken)
    {
        var notification = await context.Notifications
            .FirstOrDefaultAsync(entity => entity.Id == command.NotificationId, cancellationToken)
            ?? throw new NotFoundException("Notification", command.NotificationId);

        OwnershipGuard.EnsureUserOwns(currentUser, notification.UserId);

        notification.MarkAsRead(dateTimeProvider.UtcNow);
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed record MarkAllNotificationsReadCommand : IRequest<Unit>;

public sealed class MarkAllNotificationsReadCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<MarkAllNotificationsReadCommand, Unit>
{
    public async Task<Unit> Handle(MarkAllNotificationsReadCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var utcNow = dateTimeProvider.UtcNow;

        var unread = await context.Notifications
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in unread)
        {
            notification.MarkAsRead(utcNow);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed record DeleteNotificationCommand(Guid NotificationId) : IRequest<Unit>;

public sealed class DeleteNotificationCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<DeleteNotificationCommand, Unit>
{
    public async Task<Unit> Handle(DeleteNotificationCommand command, CancellationToken cancellationToken)
    {
        var notification = await context.Notifications
            .FirstOrDefaultAsync(entity => entity.Id == command.NotificationId, cancellationToken)
            ?? throw new NotFoundException("Notification", command.NotificationId);

        OwnershipGuard.EnsureUserOwns(currentUser, notification.UserId);

        context.Notifications.Remove(notification);
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Clears the caller's already-read notifications. Unread ones are deliberately kept: a
/// "clear" gesture should never silently drop something the user has not seen.
/// </summary>
public sealed record ClearReadNotificationsCommand : IRequest<int>;

public sealed class ClearReadNotificationsCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<ClearReadNotificationsCommand, int>
{
    public async Task<int> Handle(ClearReadNotificationsCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var read = await context.Notifications
            .Where(notification => notification.UserId == userId && notification.IsRead)
            .ToListAsync(cancellationToken);

        if (read.Count == 0)
        {
            return 0;
        }

        context.Notifications.RemoveRange(read);
        await context.SaveChangesAsync(cancellationToken);

        return read.Count;
    }
}

/// <summary>One notification, for a deep link that opens straight onto it.</summary>
public sealed record GetNotificationByIdQuery(Guid NotificationId) : IRequest<NotificationDto>;

public sealed class GetNotificationByIdQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetNotificationByIdQuery, NotificationDto>
{
    public async Task<NotificationDto> Handle(
        GetNotificationByIdQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        // Scoping the lookup to the caller means a guessed id reads as "not found" rather
        // than leaking that someone else's notification exists.
        return await context.Notifications
            .AsNoTracking()
            .Where(notification => notification.Id == query.NotificationId && notification.UserId == userId)
            .Select(NotificationProjections.ToDto)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Notification", query.NotificationId);
    }
}
