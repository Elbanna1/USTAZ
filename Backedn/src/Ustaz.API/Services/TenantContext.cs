using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Exceptions;

namespace Ustaz.API.Services;

/// <summary>
/// Resolves the workspace for the current request.
///
/// Order of precedence:
/// 1. An explicit override set by a handler (the public website resolves by slug).
/// 2. The <c>X-Tenant-Id</c> header, which lets a user who belongs to several workspaces pick
///    one. It is only a hint: membership is still verified against the database.
/// 3. The tenantId claim in the JWT.
/// </summary>
public sealed class TenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public const string TenantHeaderName = "X-Tenant-Id";

    private Guid? _overrideTenantId;
    private string? _overrideSlug;

    public Guid? TenantId
    {
        get
        {
            if (_overrideTenantId is not null)
            {
                return _overrideTenantId;
            }

            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return null;
            }

            if (httpContext.Request.Headers.TryGetValue(TenantHeaderName, out var header)
                && Guid.TryParse(header.ToString(), out var fromHeader))
            {
                return fromHeader;
            }

            var claim = httpContext.User.FindFirst(CustomClaimTypes.TenantId)?.Value;
            return Guid.TryParse(claim, out var fromClaim) ? fromClaim : null;
        }
    }

    public string? TenantSlug =>
        _overrideSlug ?? httpContextAccessor.HttpContext?.User.FindFirst(CustomClaimTypes.TenantSlug)?.Value;

    public bool HasTenant => TenantId is not null;

    public Guid RequireTenantId() =>
        TenantId ?? throw new ForbiddenAccessException(
            "This action requires an active workspace. Sign in as a teacher or workspace member.");

    public void SetTenant(Guid tenantId, string? slug = null)
    {
        _overrideTenantId = tenantId;
        _overrideSlug = slug;
    }
}
