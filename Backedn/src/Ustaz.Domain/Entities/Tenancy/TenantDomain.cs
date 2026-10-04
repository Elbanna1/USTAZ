using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities.Tenancy;

/// <summary>
/// A custom domain pointed at a tenant's public website. Verification is DNS TXT based:
/// the tenant publishes the generated token, then asks USTAZ to check it.
/// </summary>
public class TenantDomain : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    /// <summary>Lower-cased host name, e.g. ustaz.ahmed-physics.com.</summary>
    public string Host { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public DomainVerificationStatus VerificationStatus { get; set; } = DomainVerificationStatus.Pending;

    /// <summary>Value the tenant must publish as a TXT record at _ustaz-verify.{host}.</summary>
    public string VerificationToken { get; set; } = string.Empty;

    public DateTime? VerifiedAt { get; set; }

    public string? LastVerificationError { get; set; }

    public int VerificationAttempts { get; set; }

    public bool IsVerified => VerificationStatus == DomainVerificationStatus.Verified;

    public void MarkVerified(DateTime utcNow)
    {
        VerificationStatus = DomainVerificationStatus.Verified;
        VerifiedAt = utcNow;
        LastVerificationError = null;
        UpdatedAt = utcNow;
    }

    public void MarkFailed(DateTime utcNow, string error)
    {
        VerificationStatus = DomainVerificationStatus.Failed;
        LastVerificationError = error;
        VerificationAttempts++;
        UpdatedAt = utcNow;
    }

    public void EnsureCanBePrimary()
    {
        if (!IsVerified)
        {
            throw new BusinessRuleException(
                "DOMAIN_NOT_VERIFIED",
                "A domain must be verified before it can serve the public website.");
        }
    }
}
