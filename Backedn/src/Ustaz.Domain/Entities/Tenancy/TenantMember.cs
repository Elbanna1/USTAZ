using Ustaz.Domain.Common;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities.Tenancy;

/// <summary>
/// A user's membership of one workspace, together with the granular permissions granted to
/// them. Assistants are modelled here rather than as a separate account type: an assistant is
/// simply a member whose role is <see cref="TenantMemberRole.Assistant"/>.
/// </summary>
public class TenantMember : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public TenantMemberRole Role { get; set; } = TenantMemberRole.Assistant;

    public TenantMemberStatus Status { get; set; } = TenantMemberStatus.Invited;

    /// <summary>Free-text job title shown in the team list, e.g. "مساعد أول".</summary>
    public string? Title { get; set; }

    public Guid? InvitedByUserId { get; set; }

    public DateTime? InvitedAt { get; set; }

    public DateTime? JoinedAt { get; set; }

    public DateTime? DeactivatedAt { get; set; }

    public ICollection<TenantMemberPermission> Permissions { get; set; } = [];

    public bool IsOwner => Role == TenantMemberRole.Owner;

    public bool IsActive => Status == TenantMemberStatus.Active;

    /// <summary>
    /// Effective permissions: what the role implies, widened by explicit grants.
    /// Assistants start empty, so every capability must be granted deliberately.
    /// </summary>
    public IReadOnlySet<string> EffectivePermissions()
    {
        var effective = new HashSet<string>(TenantPermissions.ForRole(Role), StringComparer.Ordinal);

        foreach (var grant in Permissions.Where(permission => permission.IsGranted))
        {
            // Owner-only capabilities can never be granted to a non-owner, whatever the row says.
            if (!IsOwner && TenantPermissions.OwnerOnly.Contains(grant.Permission))
            {
                continue;
            }

            effective.Add(grant.Permission);
        }

        return effective;
    }

    public bool HasPermission(string permission) =>
        IsActive && EffectivePermissions().Contains(permission);

    public void EnsureActive()
    {
        if (Status != TenantMemberStatus.Active)
        {
            throw new ForbiddenAccessException("Your access to this workspace is not active.");
        }
    }

    /// <summary>The owner row is structural: it cannot be demoted, deactivated or deleted.</summary>
    public void EnsureNotOwner(string action)
    {
        if (IsOwner)
        {
            throw new BusinessRuleException(
                "OWNER_IMMUTABLE",
                "The workspace owner cannot be " + action + ". Transfer ownership first.");
        }
    }

    public void Accept(DateTime utcNow)
    {
        if (Status is not (TenantMemberStatus.Invited or TenantMemberStatus.Inactive))
        {
            throw new BusinessRuleException("INVITATION_NOT_PENDING", "This invitation is no longer pending.");
        }

        Status = TenantMemberStatus.Active;
        JoinedAt ??= utcNow;
        DeactivatedAt = null;
        UpdatedAt = utcNow;
    }

    public void Decline(DateTime utcNow)
    {
        if (Status != TenantMemberStatus.Invited)
        {
            throw new BusinessRuleException("INVITATION_NOT_PENDING", "This invitation is no longer pending.");
        }

        Status = TenantMemberStatus.Declined;
        UpdatedAt = utcNow;
    }

    public void Deactivate(DateTime utcNow)
    {
        EnsureNotOwner("deactivated");
        Status = TenantMemberStatus.Inactive;
        DeactivatedAt = utcNow;
        UpdatedAt = utcNow;
    }
}
