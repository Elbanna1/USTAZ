using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Common.Interfaces;

/// <summary>Resolved, database-backed view of what the caller may do in a workspace.</summary>
public sealed record TenantAccess
{
    public required Guid TenantId { get; init; }

    public required Guid UserId { get; init; }

    public required TenantMemberRole Role { get; init; }

    public required IReadOnlySet<string> Permissions { get; init; }

    public bool IsOwner => Role == TenantMemberRole.Owner;

    /// <summary>True for a platform administrator, who bypasses membership but not existence.</summary>
    public bool IsPlatformAdmin { get; init; }

    public bool Has(string permission) => IsPlatformAdmin || Permissions.Contains(permission);
}

/// <summary>
/// The single gate for workspace authorization. Everything is resolved from the database on
/// each call: permissions in the JWT are a convenience for the UI, never an authority.
/// </summary>
public interface ITenantAuthorizationService
{
    /// <summary>
    /// Resolves the caller's access to the given workspace (or the ambient one).
    /// Throws <see cref="Domain.Exceptions.ForbiddenAccessException"/> when the caller is not
    /// an active member and is not a platform admin.
    /// </summary>
    Task<TenantAccess> RequireAccessAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>Resolves access and additionally requires one granular permission.</summary>
    Task<TenantAccess> RequirePermissionAsync(
        string permission,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves access and requires the caller to be the workspace owner.</summary>
    Task<TenantAccess> RequireOwnerAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>Non-throwing probe, for shaping responses rather than gating them.</summary>
    Task<TenantAccess?> TryGetAccessAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that a resource's <c>TenantId</c> matches the workspace the caller is acting
    /// in. This is the check that defeats id tampering across tenants.
    /// </summary>
    void EnsureSameTenant(TenantAccess access, Guid resourceTenantId);

    /// <summary>Loads the caller's membership row, tracked, for mutation.</summary>
    Task<TenantMember?> FindMembershipAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
