using Ustaz.Application.Common.Interfaces;

namespace Ustaz.Application.Tests.Infrastructure;

/// <summary>Lets a test say who is sitting in front of the app right now.</summary>
public sealed class StubPresenceTracker : IUserPresenceTracker
{
    private readonly HashSet<Guid> _online = [];

    public bool IsOnline(Guid userId) => _online.Contains(userId);

    public void Join(Guid userId, string connectionId) => _online.Add(userId);

    public void Leave(Guid userId, string connectionId) => _online.Remove(userId);

    public void SetOnline(Guid userId) => _online.Add(userId);
}
