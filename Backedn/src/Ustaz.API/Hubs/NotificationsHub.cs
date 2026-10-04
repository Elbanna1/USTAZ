using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Ustaz.Application.Common.Interfaces;

namespace Ustaz.API.Hubs;

/// <summary>
/// Live notification and message delivery.
///
/// The hub carries no authorization decisions of its own: a connection is only ever placed in
/// a group named after the caller's own user id, taken from the validated JWT, so a client
/// cannot subscribe to anyone else's stream by asking. Tenant isolation follows from that —
/// a notification row already belongs to exactly one user.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public const string Path = "/hubs/notifications";

    public static string UserGroup(Guid userId) => "user:" + userId;

    public override async Task OnConnectedAsync()
    {
        var currentUser = Context.GetHttpContext()?.RequestServices.GetService<ICurrentUserService>();
        var userId = currentUser?.UserId;

        if (userId is null)
        {
            // An authenticated principal without our own user claim is not something we can
            // route to; drop the connection rather than leave it silently dark.
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));

        Context.GetHttpContext()?.RequestServices
            .GetService<IUserPresenceTracker>()?
            .Join(userId.Value, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var currentUser = Context.GetHttpContext()?.RequestServices.GetService<ICurrentUserService>();
        var userId = currentUser?.UserId;

        if (userId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId.Value));

            Context.GetHttpContext()?.RequestServices
                .GetService<IUserPresenceTracker>()?
                .Leave(userId.Value, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
