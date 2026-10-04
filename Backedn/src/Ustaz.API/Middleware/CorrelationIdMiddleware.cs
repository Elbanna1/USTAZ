using Serilog.Context;
using Ustaz.Application.Common.Interfaces;

namespace Ustaz.API.Middleware;

/// <summary>
/// Gives every request a correlation id and puts the caller's identity into the log scope.
///
/// A support question is always "what happened to this user, in this workspace, on this
/// request" — so those three facts are attached to every log line the request produces. The
/// id is echoed back so a client can quote it. Nothing sensitive is added: no tokens, no
/// headers, no bodies.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var supplied)
            && !string.IsNullOrWhiteSpace(supplied)
                // A client-supplied id is only ever used as a label, never trusted as input.
                ? supplied.ToString()[..Math.Min(supplied.ToString().Length, 64)]
                : context.TraceIdentifier;

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("UserId", currentUser.UserId))
        using (LogContext.PushProperty("TenantId", currentUser.TenantId))
        {
            await next(context);
        }
    }
}
