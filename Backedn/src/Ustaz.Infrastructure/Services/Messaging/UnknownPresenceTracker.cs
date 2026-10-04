using Ustaz.Application.Common.Interfaces;

namespace Ustaz.Infrastructure.Services.Messaging;

/// <summary>
/// The fallback when real-time delivery is switched off: nobody is known to be online, so
/// offline-only emails are always sent. Failing towards "send it" is the safe direction.
/// </summary>
public sealed class UnknownPresenceTracker : IUserPresenceTracker
{
    public bool IsOnline(Guid userId) => false;

    public void Join(Guid userId, string connectionId)
    {
    }

    public void Leave(Guid userId, string connectionId)
    {
    }
}
