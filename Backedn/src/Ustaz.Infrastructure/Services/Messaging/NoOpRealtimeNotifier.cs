using Ustaz.Application.Common.Interfaces;

namespace Ustaz.Infrastructure.Services.Messaging;

/// <summary>
/// The default live transport: none. The API replaces this with the SignalR publisher when
/// real-time delivery is enabled, so the backend runs identically with the feature switched
/// off — notifications are still persisted and still returned by the notification API.
/// </summary>
public sealed class NoOpRealtimeNotifier : IRealtimeNotifier
{
    public Task PublishAsync(RealtimeNotification notification, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task PublishUnreadCountAsync(Guid userId, int unreadCount, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
