using Ustaz.Application.Common.Notifications;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Common.Interfaces;

/// <summary>Creates in-app notifications without every handler touching the DbSet directly.</summary>
public interface INotificationDispatcher
{
    Task NotifyAsync(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        string? actionUrl = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a categorised notification, honouring the recipient's per-category channel
    /// preferences (in-app, email, WhatsApp).
    /// </summary>
    Task NotifyAsync(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        NotificationCategory category,
        Guid? tenantId = null,
        string? actionUrl = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises one catalog notification for a single recipient. This is the path new code
    /// uses: the wording, category, priority and deep link all come from the catalog, so a
    /// handler never hard-codes a sentence.
    /// </summary>
    /// <returns>True when a notification was written; false when it was suppressed by the
    /// user's preferences or by deduplication.</returns>
    Task<bool> SendAsync(
        Guid userId,
        NotificationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Raises the same catalog notification for several recipients.</summary>
    Task<int> SendManyAsync(
        IEnumerable<Guid> userIds,
        NotificationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Fans a categorised notification out to several recipients.</summary>
    Task NotifyManyAsync(
        IEnumerable<Guid> userIds,
        string title,
        string message,
        NotificationType type,
        NotificationCategory category,
        Guid? tenantId = null,
        string? actionUrl = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default);
}
