namespace Ustaz.Application.Common.Interfaces;

/// <summary>
/// Who currently has a live connection open.
///
/// Used to keep email as an *offline* channel: someone reading a conversation in the app does
/// not need the same message in their inbox thirty seconds later. Presence is best-effort by
/// nature — a stale "online" only costs a missed email, never a missed notification, because
/// the in-app row is written either way.
/// </summary>
public interface IUserPresenceTracker
{
    bool IsOnline(Guid userId);

    void Join(Guid userId, string connectionId);

    void Leave(Guid userId, string connectionId);
}
