using Ustaz.API.Middleware;
using Ustaz.Application.Common.Interfaces;

namespace Ustaz.API.Services;

/// <summary>Reads the correlation id the middleware stamped onto the response.</summary>
public sealed class HttpCorrelationContext(IHttpContextAccessor accessor) : ICorrelationContext
{
    public string? CorrelationId
    {
        get
        {
            var context = accessor.HttpContext;

            if (context is null)
            {
                return null;
            }

            var header = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();

            return string.IsNullOrWhiteSpace(header) ? context.TraceIdentifier : header;
        }
    }
}
