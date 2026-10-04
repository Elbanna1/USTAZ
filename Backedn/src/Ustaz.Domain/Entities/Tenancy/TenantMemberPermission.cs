using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities.Tenancy;

/// <summary>
/// One explicit permission grant for a workspace member. Rows only ever widen what the
/// member's role already allows; owner-only capabilities are filtered out when the effective
/// set is computed, so a stale or tampered row can never escalate privileges.
/// </summary>
public class TenantMemberPermission : BaseEntity
{
    public Guid TenantMemberId { get; set; }

    public TenantMember TenantMember { get; set; } = null!;

    /// <summary>A value from <see cref="Ustaz.Domain.Constants.TenantPermissions"/>.</summary>
    public string Permission { get; set; } = string.Empty;

    public bool IsGranted { get; set; } = true;

    public Guid? GrantedByUserId { get; set; }
}
