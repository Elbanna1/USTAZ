using Microsoft.AspNetCore.SignalR;
using Ustaz.API.Hubs;
using Ustaz.Application.Common.Interfaces;

namespace Ustaz.API.Services;

/// <summary>
/// Pushes persisted notifications to connected clients over SignalR.
///
/// Delivery is best-effort by design: the notification is already in the database before this
/// runs, so an offline client simply picks it up from the notification API on next load.
/// </summary>
public sealed class SignalRRealtimeNotifier(IHubContext<NotificationsHub> hub) : IRealtimeNotifier
{
    public Task PublishAsync(RealtimeNotification notification, CancellationToken cancellationToken = default) =>
        hub.Clients
            .Group(NotificationsHub.UserGroup(notification.UserId))
            .SendAsync("notification", notification, cancellationToken);

    public Task PublishUnreadCountAsync(Guid userId, int unreadCount, CancellationToken cancellationToken = default) =>
        hub.Clients
            .Group(NotificationsHub.UserGroup(userId))
            .SendAsync("unreadCount", unreadCount, cancellationToken);
}
