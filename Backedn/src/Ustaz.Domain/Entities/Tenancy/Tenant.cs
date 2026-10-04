using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities.Tenancy;

/// <summary>
/// A workspace: one teacher or educational centre that subscribes to USTAZ. Every piece of
/// operational data (groups, students, attendance, exams, payments) hangs off a tenant, and
/// nothing may ever be read across tenant boundaries.
/// </summary>
public class Tenant : BaseEntity
{
    /// <summary>Public identifier used by the tenant's website, e.g. /t/ahmed-physics.</summary>
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public TenantStatus Status { get; set; } = TenantStatus.Active;

    /// <summary>The user who owns the workspace. Exactly one owner at any time.</summary>
    public Guid OwnerUserId { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public string? WhatsAppNumber { get; set; }

    public string? City { get; set; }

    public string? SuspendedReason { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public TenantSettings? Settings { get; set; }

    public TenantBranding? Branding { get; set; }

    public TenantWebsite? Website { get; set; }

    public ICollection<TenantMember> Members { get; set; } = [];

    public ICollection<TenantDomain> Domains { get; set; } = [];

    public ICollection<TenantStudent> Students { get; set; } = [];

    public bool IsOperational => Status == TenantStatus.Active;

    public void EnsureOperational()
    {
        if (Status == TenantStatus.Suspended)
        {
            throw new BusinessRuleException(
                "TENANT_SUSPENDED",
                "This workspace is suspended. Please contact USTAZ support.");
        }

        if (Status == TenantStatus.Archived)
        {
            throw new BusinessRuleException("TENANT_ARCHIVED", "This workspace is no longer active.");
        }
    }

    public void Suspend(DateTime utcNow, string reason)
    {
        Status = TenantStatus.Suspended;
        SuspendedReason = reason;
        SuspendedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public void Activate(DateTime utcNow)
    {
        Status = TenantStatus.Active;
        SuspendedReason = null;
        SuspendedAt = null;
        UpdatedAt = utcNow;
    }
}
