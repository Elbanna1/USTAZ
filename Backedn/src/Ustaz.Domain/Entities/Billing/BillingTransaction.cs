using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities.Billing;

/// <summary>
/// A money movement against a tenant's SaaS subscription. Distinct from
/// <c>EducationalPayment</c>, which is a student paying a teacher.
/// </summary>
public class BillingTransaction : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Guid? TenantSubscriptionId { get; set; }

    public TenantSubscription? Subscription { get; set; }

    public Guid? InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EGP";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public string Provider { get; set; } = string.Empty;

    /// <summary>The provider's own id. Unique per provider, which makes retries idempotent.</summary>
    public string? ProviderTransactionId { get; set; }

    public string? ProviderReference { get; set; }

    public string? FailureReason { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    public string? MetadataJson { get; set; }
}
