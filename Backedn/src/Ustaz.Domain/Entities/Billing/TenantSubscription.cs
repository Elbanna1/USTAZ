using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities.Billing;

/// <summary>
/// A tenant's USTAZ subscription and its lifecycle. State only ever moves through the methods
/// below, which is what keeps the trial/active/past-due/grace/canceled/expired transitions honest.
/// A subscription is never activated from a client callback: only a verified provider webhook
/// (or an explicit admin action) calls <see cref="MarkPaid"/>.
/// </summary>
public class TenantSubscription : BaseEntity
{
    /// <summary>Days a past-due subscription keeps working while payment is retried.</summary>
    public const int DefaultGracePeriodDays = 7;

    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Guid SubscriptionPlanId { get; set; }

    public SubscriptionPlan Plan { get; set; } = null!;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;

    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EGP";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? TrialEndsAt { get; set; }

    /// <summary>Start of the period currently paid for.</summary>
    public DateTime CurrentPeriodStart { get; set; } = DateTime.UtcNow;

    /// <summary>End of the period currently paid for; renewal extends this.</summary>
    public DateTime CurrentPeriodEnd { get; set; } = DateTime.UtcNow;

    public DateTime? GracePeriodEndsAt { get; set; }

    public DateTime? CanceledAt { get; set; }

    /// <summary>A cancellation takes effect at period end, so access is not cut off mid-month.</summary>
    public bool CancelAtPeriodEnd { get; set; }

    public DateTime? EndedAt { get; set; }

    public string? CancellationReason { get; set; }

    public string? ProviderName { get; set; }

    public string? ProviderSubscriptionId { get; set; }

    public string? ProviderCustomerId { get; set; }

    public int FailedPaymentCount { get; set; }

    public ICollection<SubscriptionEvent> Events { get; set; } = [];

    public ICollection<Invoice> Invoices { get; set; } = [];

    /// <summary>True while the tenant should keep full access to paid features.</summary>
    public bool IsUsable(DateTime utcNow) => Status switch
    {
        SubscriptionStatus.Trial => TrialEndsAt is null || utcNow <= TrialEndsAt,
        SubscriptionStatus.Active => utcNow <= CurrentPeriodEnd,
        SubscriptionStatus.PastDue => true,
        SubscriptionStatus.GracePeriod => GracePeriodEndsAt is null || utcNow <= GracePeriodEndsAt,
        // A cancelled subscription still runs to the end of the paid period.
        SubscriptionStatus.Canceled => utcNow <= CurrentPeriodEnd,
        _ => false
    };

    public bool IsTrialing(DateTime utcNow) =>
        Status == SubscriptionStatus.Trial && (TrialEndsAt is null || utcNow <= TrialEndsAt);

    public void StartTrial(DateTime utcNow, int trialDays)
    {
        Status = SubscriptionStatus.Trial;
        StartedAt = utcNow;
        TrialEndsAt = utcNow.AddDays(trialDays);
        CurrentPeriodStart = utcNow;
        CurrentPeriodEnd = TrialEndsAt.Value;
        UpdatedAt = utcNow;
        Record(SubscriptionEventType.TrialStarted, utcNow, "Trial started for " + trialDays + " days.");
    }

    /// <summary>Called when a provider confirms a payment. Idempotent per billing period.</summary>
    public void MarkPaid(DateTime utcNow, BillingCycle cycle, decimal amount)
    {
        var wasReactivation = Status is SubscriptionStatus.Canceled
            or SubscriptionStatus.Expired
            or SubscriptionStatus.GracePeriod
            or SubscriptionStatus.PastDue;

        // Renew from the later of "now" and the current period end, so paying early
        // extends the subscription instead of shortening it.
        var anchor = CurrentPeriodEnd > utcNow && Status != SubscriptionStatus.Trial
            ? CurrentPeriodEnd
            : utcNow;

        BillingCycle = cycle;
        Amount = amount;
        CurrentPeriodStart = anchor;
        CurrentPeriodEnd = cycle == BillingCycle.Yearly ? anchor.AddYears(1) : anchor.AddMonths(1);
        Status = SubscriptionStatus.Active;
        GracePeriodEndsAt = null;
        FailedPaymentCount = 0;
        CancelAtPeriodEnd = false;
        CanceledAt = null;
        EndedAt = null;
        UpdatedAt = utcNow;

        Record(
            wasReactivation ? SubscriptionEventType.Reactivated : SubscriptionEventType.Renewed,
            utcNow,
            "Payment confirmed; period now ends " + CurrentPeriodEnd.ToString("u") + ".");
    }

    public void MarkPaymentFailed(DateTime utcNow, string? reason = null)
    {
        FailedPaymentCount++;

        if (Status is SubscriptionStatus.Active or SubscriptionStatus.Trial)
        {
            Status = SubscriptionStatus.PastDue;
            Record(SubscriptionEventType.PaymentFailed, utcNow, reason ?? "Payment failed.");
        }
        else if (Status == SubscriptionStatus.PastDue)
        {
            Status = SubscriptionStatus.GracePeriod;
            GracePeriodEndsAt = utcNow.AddDays(DefaultGracePeriodDays);
            Record(SubscriptionEventType.EnteredGracePeriod, utcNow, "Entered grace period after repeated failures.");
        }

        UpdatedAt = utcNow;
    }

    public void ChangePlan(SubscriptionPlan plan, BillingCycle cycle, DateTime utcNow)
    {
        if (Status == SubscriptionStatus.Expired)
        {
            throw new BusinessRuleException(
                "SUBSCRIPTION_EXPIRED",
                "This subscription has expired. Start a new one instead of changing the plan.");
        }

        SubscriptionPlanId = plan.Id;
        Plan = plan;
        BillingCycle = cycle;
        Amount = plan.PriceFor(cycle);
        UpdatedAt = utcNow;

        Record(SubscriptionEventType.PlanChanged, utcNow, "Plan changed to " + plan.Code + " (" + cycle + ").");
    }

    public void Cancel(DateTime utcNow, bool immediately, string? reason)
    {
        if (Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired)
        {
            throw new BusinessRuleException(
                "SUBSCRIPTION_ALREADY_CANCELED",
                "This subscription is already cancelled.");
        }

        CanceledAt = utcNow;
        CancellationReason = reason;
        Status = SubscriptionStatus.Canceled;

        if (immediately)
        {
            CancelAtPeriodEnd = false;
            CurrentPeriodEnd = utcNow;
            EndedAt = utcNow;
            Status = SubscriptionStatus.Expired;
        }
        else
        {
            CancelAtPeriodEnd = true;
        }

        UpdatedAt = utcNow;
        Record(SubscriptionEventType.Canceled, utcNow, reason ?? "Cancelled by the tenant.");
    }

    public void Reactivate(DateTime utcNow)
    {
        if (Status is not (SubscriptionStatus.Canceled or SubscriptionStatus.Expired))
        {
            throw new BusinessRuleException(
                "SUBSCRIPTION_NOT_CANCELED",
                "Only a cancelled or expired subscription can be reactivated.");
        }

        // Still inside the paid period: simply undo the pending cancellation.
        if (CurrentPeriodEnd > utcNow)
        {
            Status = SubscriptionStatus.Active;
            CancelAtPeriodEnd = false;
            CanceledAt = null;
            CancellationReason = null;
            EndedAt = null;
        }
        else
        {
            // Past the paid period: the tenant must pay before access returns.
            Status = SubscriptionStatus.PastDue;
            CancelAtPeriodEnd = false;
            CanceledAt = null;
            EndedAt = null;
        }

        UpdatedAt = utcNow;
        Record(SubscriptionEventType.Reactivated, utcNow, "Subscription reactivated.");
    }

    /// <summary>
    /// Advances expiry-driven state. Safe to call repeatedly; it only ever moves forward.
    /// </summary>
    public bool ApplyElapsedTime(DateTime utcNow)
    {
        var changed = false;

        if (Status == SubscriptionStatus.Trial && TrialEndsAt is not null && utcNow > TrialEndsAt)
        {
            Status = SubscriptionStatus.PastDue;
            Record(SubscriptionEventType.TrialExpired, utcNow, "Trial period ended.");
            changed = true;
        }

        if (Status == SubscriptionStatus.GracePeriod && GracePeriodEndsAt is not null && utcNow > GracePeriodEndsAt)
        {
            Status = SubscriptionStatus.Expired;
            EndedAt = utcNow;
            Record(SubscriptionEventType.Expired, utcNow, "Grace period ended without payment.");
            changed = true;
        }

        if (Status == SubscriptionStatus.Canceled && CancelAtPeriodEnd && utcNow > CurrentPeriodEnd)
        {
            Status = SubscriptionStatus.Expired;
            EndedAt = utcNow;
            Record(SubscriptionEventType.Expired, utcNow, "Cancelled subscription reached the end of its period.");
            changed = true;
        }

        if (changed)
        {
            UpdatedAt = utcNow;
        }

        return changed;
    }

    private void Record(SubscriptionEventType type, DateTime utcNow, string description) =>
        Events.Add(new SubscriptionEvent
        {
            Id = Guid.NewGuid(),
            TenantSubscriptionId = Id,
            TenantId = TenantId,
            Type = type,
            Description = description,
            OccurredAt = utcNow,
            CreatedAt = utcNow
        });
}
