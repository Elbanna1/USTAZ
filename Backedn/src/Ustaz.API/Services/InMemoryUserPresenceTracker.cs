using System.Collections.Concurrent;
using Ustaz.Application.Common.Interfaces;

namespace Ustaz.API.Services;

/// <summary>
/// Tracks live SignalR connections per user, in this process.
///
/// A user may have several tabs open, so connections are counted rather than flagged: the
/// last one to disconnect is what takes them offline. This is per-instance state by design —
/// behind a load balancer a user may look offline to one node, and the only consequence is an
/// extra email, which is the harmless direction to be wrong in.
/// </summary>
public sealed class InMemoryUserPresenceTracker : IUserPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _connections = new();

    public bool IsOnline(Guid userId) =>
        _connections.TryGetValue(userId, out var connections) && !connections.IsEmpty;

    public void Join(Guid userId, string connectionId) =>
        _connections
            .GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>())
            .TryAdd(connectionId, 0);

    public void Leave(Guid userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var connections))
        {
            return;
        }

        connections.TryRemove(connectionId, out _);

        if (connections.IsEmpty)
        {
            _connections.TryRemove(userId, out _);
        }
    }
}
