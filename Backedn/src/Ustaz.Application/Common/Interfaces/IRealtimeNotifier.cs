using Ustaz.Domain.Enums;

namespace Ustaz.Application.Common.Interfaces;

/// <summary>One notification, shaped for a live push to a connected client.</summary>
public sealed record RealtimeNotification
{
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public Guid? TenantId { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    public string? TitleEn { get; init; }

    public string? MessageEn { get; init; }

    public required NotificationType Type { get; init; }

    public required NotificationCategory Category { get; init; }

    public required NotificationPriority Priority { get; init; }

    public string? Event { get; init; }

    public string? ActionUrl { get; init; }

    public Guid? ReferenceId { get; init; }

    public string? RelatedEntityType { get; init; }

    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Pushes an already-persisted notification to whatever live transport is configured.
///
/// The database row is written first and is the only source of truth; a push that fails, or
/// a client that is offline, costs nothing because the bell still shows the notification on
/// the next fetch. Implementations must therefore never throw into the caller.
/// </summary>
public interface IRealtimeNotifier
{
    Task PublishAsync(RealtimeNotification notification, CancellationToken cancellationToken = default);

    /// <summary>Pushes the recipient's new unread total, so badges stay in step.</summary>
    Task PublishUnreadCountAsync(Guid userId, int unreadCount, CancellationToken cancellationToken = default);
}
