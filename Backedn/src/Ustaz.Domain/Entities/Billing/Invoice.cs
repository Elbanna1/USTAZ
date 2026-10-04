using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities.Billing;

/// <summary>An invoice USTAZ issues to a tenant for their SaaS subscription.</summary>
public class Invoice : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Guid? TenantSubscriptionId { get; set; }

    public TenantSubscription? Subscription { get; set; }

    /// <summary>Human readable sequence, e.g. USTAZ-2026-000123.</summary>
    public string Number { get; set; } = string.Empty;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal Total { get; set; }

    public string Currency { get; set; } = "EGP";

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DueAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? Description { get; set; }

    public DateTime? PeriodStart { get; set; }

    public DateTime? PeriodEnd { get; set; }

    public ICollection<BillingTransaction> Transactions { get; set; } = [];

    public void MarkPaid(DateTime utcNow)
    {
        Status = InvoiceStatus.Paid;
        PaidAt ??= utcNow;
        UpdatedAt = utcNow;
    }
}
