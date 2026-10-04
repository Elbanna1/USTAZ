using Ustaz.Application.Common.Interfaces;

namespace Ustaz.Application.Tests.Infrastructure;

/// <summary>
/// Stands in for the SignalR publisher and remembers what was pushed, so tests can assert
/// that live delivery happened after the row was persisted — never instead of it.
/// </summary>
public sealed class RecordingRealtimeNotifier : IRealtimeNotifier
{
    public List<RealtimeNotification> Published { get; } = [];

    public List<(Guid UserId, int Count)> UnreadCounts { get; } = [];

    /// <summary>Set to make the transport fail, proving delivery is best-effort.</summary>
    public bool ThrowOnPublish { get; set; }

    public Task PublishAsync(RealtimeNotification notification, CancellationToken cancellationToken = default)
    {
        if (ThrowOnPublish)
        {
            throw new InvalidOperationException("The live transport is down.");
        }

        Published.Add(notification);
        return Task.CompletedTask;
    }

    public Task PublishUnreadCountAsync(Guid userId, int unreadCount, CancellationToken cancellationToken = default)
    {
        UnreadCounts.Add((userId, unreadCount));
        return Task.CompletedTask;
    }
}
