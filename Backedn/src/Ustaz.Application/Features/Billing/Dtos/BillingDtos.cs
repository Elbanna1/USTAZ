using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Billing.Dtos;

public sealed record SubscriptionPlanDto
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string NameAr { get; init; } = string.Empty;

    public string NameEn { get; init; } = string.Empty;

    public string? Description { get; init; }

    public decimal MonthlyPrice { get; init; }

    public decimal? YearlyPrice { get; init; }

    public string Currency { get; init; } = "EGP";

    public int TrialDays { get; init; }

    public int SortOrder { get; init; }

    public bool IsActive { get; init; }

    public int? MaxStudents { get; init; }

    public int? MaxGroups { get; init; }

    public int? MaxCourses { get; init; }

    public int? MaxAssistants { get; init; }

    public int? MaxTeamMembers { get; init; }

    public int? MaxStorageMegabytes { get; init; }

    public IReadOnlyList<string> Features { get; init; } = [];
}

public sealed record TenantSubscriptionDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public SubscriptionPlanDto Plan { get; init; } = new();

    public SubscriptionStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public BillingCycle BillingCycle { get; init; }

    public string BillingCycleName => BillingCycle.ToString();

    public decimal Amount { get; init; }

    public string Currency { get; init; } = "EGP";

    public DateTime StartedAt { get; init; }

    public DateTime? TrialEndsAt { get; init; }

    public DateTime CurrentPeriodStart { get; init; }

    public DateTime CurrentPeriodEnd { get; init; }

    public DateTime? GracePeriodEndsAt { get; init; }

    public bool CancelAtPeriodEnd { get; init; }

    public DateTime? CanceledAt { get; init; }

    /// <summary>True while paid features remain available.</summary>
    public bool IsUsable { get; init; }

    public bool IsTrialing { get; init; }

    public int? DaysRemaining { get; init; }

    public IReadOnlyList<PlanQuotaUsageDto> Usage { get; init; } = [];
}

public sealed record PlanQuotaUsageDto
{
    public string Quota { get; init; } = string.Empty;

    public int? Limit { get; init; }

    public int Used { get; init; }

    public int? Remaining { get; init; }

    public bool IsUnlimited { get; init; }
}

public sealed record InvoiceDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Number { get; init; } = string.Empty;

    public InvoiceStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public decimal Subtotal { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal Total { get; init; }

    public string Currency { get; init; } = "EGP";

    public DateTime IssuedAt { get; init; }

    public DateTime? DueAt { get; init; }

    public DateTime? PaidAt { get; init; }

    public string? Description { get; init; }

    public DateTime? PeriodStart { get; init; }

    public DateTime? PeriodEnd { get; init; }
}

public sealed record BillingTransactionDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid? InvoiceId { get; init; }

    public string? InvoiceNumber { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = "EGP";

    public PaymentStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public string Provider { get; init; } = string.Empty;

    public string? ProviderTransactionId { get; init; }

    public string? FailureReason { get; init; }

    public DateTime? PaidAt { get; init; }

    public DateTime CreatedAt { get; init; }
}

/// <summary>What the client needs to send the tenant to the payment provider.</summary>
public sealed record CheckoutSessionDto
{
    public Guid InvoiceId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public Guid TransactionId { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = "EGP";

    public string Provider { get; init; } = string.Empty;

    /// <summary>Reference the provider echoes back in its webhook.</summary>
    public string ProviderReference { get; init; } = string.Empty;

    /// <summary>
    /// Where to send the tenant to pay. Null when no provider is configured, in which case
    /// the invoice must be settled manually by an administrator.
    /// </summary>
    public string? CheckoutUrl { get; init; }

    public DateTime ExpiresAt { get; init; }
}
