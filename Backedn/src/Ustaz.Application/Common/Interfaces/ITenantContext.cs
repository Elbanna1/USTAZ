namespace Ustaz.Application.Common.Interfaces;

/// <summary>
/// The workspace the current request is acting inside, resolved by the API layer from the
/// JWT (and, for public website requests, from the route slug or Host header).
///
/// The id here is a *hint* only. Every mutation still re-checks membership against the
/// database through <see cref="ITenantAuthorizationService"/>, because a token can outlive a
/// permission change and because a client value must never be the last word on access.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    string? TenantSlug { get; }

    bool HasTenant { get; }

    /// <summary>Returns the active workspace id, or throws when the request carries none.</summary>
    Guid RequireTenantId();

    /// <summary>
    /// Overrides the ambient tenant for the rest of the request. Used by the public website
    /// endpoints, which resolve the tenant from the URL rather than from a token.
    /// </summary>
    void SetTenant(Guid tenantId, string? slug = null);
}
