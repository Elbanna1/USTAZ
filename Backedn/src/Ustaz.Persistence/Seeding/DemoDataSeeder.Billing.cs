using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Entities.Billing;
using Ustaz.Domain.Enums;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    /// <summary>
    /// The SaaS side of the money: what each demo workspace pays USTAZ.
    ///
    /// Nothing here touches a payment gateway. Every transaction's provider is
    /// <c>manual</c> — the same value the product's own admin-recorded payment path uses —
    /// and no provider is called, no charge is made, and no successful production payment is
    /// implied. The subscription states are produced by calling the aggregate's own lifecycle
    /// methods rather than by assigning <see cref="TenantSubscription.Status"/>, so every
    /// seeded state is one the domain can actually reach.
    /// </summary>
    private const string DemoPaymentProvider = "manual";

    private async Task SeedBillingAsync(CancellationToken cancellationToken)
    {
        var plans = await context.SubscriptionPlans
            .AsNoTracking()
            .ToDictionaryAsync(plan => plan.Code, cancellationToken);

        if (plans.Count == 0)
        {
            report.RecordFailure("Billing fixtures skipped: no subscription plans found. Seed reference data first.");
            return;
        }

        // Workspace, plan, and the lifecycle the subscription has been through.
        (string TenantKey, string PlanCode, string Lifecycle, BillingCycle Cycle)[] plan =
        [
            ("ahmed", "professional", "active", BillingCycle.Monthly),
            ("mariam", "professional", "trial", BillingCycle.Monthly),
            ("omar", "starter", "grace", BillingCycle.Monthly),
            ("nour", "starter", "canceled", BillingCycle.Monthly),
            ("center", "center", "active", BillingCycle.Yearly),
            ("tarek", "starter", "trial", BillingCycle.Monthly),
            // Trial that already lapsed: the quota-exceeded / must-pay fixture.
            ("dalia", "starter", "expired-trial", BillingCycle.Monthly)
        ];

        var subscriptions = new List<TenantSubscription>();

        foreach (var (tenantKey, planCode, lifecycle, cycle) in plan)
        {
            if (!plans.TryGetValue(planCode, out var subscriptionPlan))
            {
                report.RecordFailure("Billing fixture for " + tenantKey + " skipped: plan '" + planCode + "' is missing.");
                continue;
            }

            var tenant = _tenants[tenantKey];
            var startedAt = tenant.CreatedAt;

            var subscription = new TenantSubscription
            {
                Id = SeedId.For("demo:subscription", tenantKey),
                TenantId = tenant.Id,
                SubscriptionPlanId = subscriptionPlan.Id,
                BillingCycle = cycle,
                Amount = subscriptionPlan.PriceFor(cycle),
                Currency = subscriptionPlan.Currency,
                StartedAt = startedAt,
                CurrentPeriodStart = startedAt,
                CurrentPeriodEnd = startedAt,
                CreatedAt = startedAt
            };

            ApplyLifecycle(subscription, subscriptionPlan, lifecycle, cycle, startedAt);
            subscriptions.Add(subscription);
        }

        // The subscription graph carries the SubscriptionEvent rows its lifecycle methods
        // recorded, so both land in one insert and the history matches the state exactly.
        var inserted = await AddMissingAsync("TenantSubscriptions", subscriptions, cancellationToken);

        report.Record(
            "SubscriptionEvents",
            inserted.Sum(subscription => subscription.Events.Count),
            note: "recorded by the subscription lifecycle");

        await SeedInvoicesAsync(cancellationToken);
    }

    /// <summary>
    /// Drives a subscription through a real sequence of domain calls to arrive at the wanted
    /// state. A trial that expired, for instance, is produced by starting a trial in the past
    /// and letting <see cref="TenantSubscription.ApplyElapsedTime"/> notice — not by writing
    /// <c>Status = PastDue</c>, which would skip the event history a billing screen reads.
    /// </summary>
    private void ApplyLifecycle(
        TenantSubscription subscription,
        SubscriptionPlan plan,
        string lifecycle,
        BillingCycle cycle,
        DateTime startedAt)
    {
        switch (lifecycle)
        {
            case "trial":
                // Started recently enough that the trial is still running.
                subscription.StartTrial(_utcNow.AddDays(-3), plan.TrialDays);
                break;

            case "expired-trial":
                subscription.StartTrial(_utcNow.AddDays(-plan.TrialDays - 5), plan.TrialDays);
                subscription.ApplyElapsedTime(_utcNow);
                break;

            case "active":
                subscription.StartTrial(startedAt, plan.TrialDays);
                subscription.MarkPaid(startedAt.AddDays(plan.TrialDays), cycle, plan.PriceFor(cycle));
                // Renew forward until the paid period covers today, the way real renewals do.
                while (subscription.CurrentPeriodEnd <= _utcNow)
                {
                    subscription.MarkPaid(subscription.CurrentPeriodEnd, cycle, plan.PriceFor(cycle));
                }

                break;

            case "grace":
                subscription.StartTrial(startedAt, plan.TrialDays);
                subscription.MarkPaid(startedAt.AddDays(plan.TrialDays), cycle, plan.PriceFor(cycle));
                subscription.MarkPaymentFailed(_utcNow.AddDays(-5), "Card declined by the issuing bank (demo).");
                subscription.MarkPaymentFailed(_utcNow.AddDays(-2), "Retry declined (demo).");
                break;

            case "canceled":
                subscription.StartTrial(startedAt, plan.TrialDays);
                subscription.MarkPaid(_utcNow.AddDays(-10), cycle, plan.PriceFor(cycle));
                subscription.Cancel(_utcNow.AddDays(-2), immediately: false, "تغيير الخطة لاحقاً");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(lifecycle), lifecycle, "Unknown demo billing lifecycle.");
        }
    }

    /// <summary>
    /// Invoices and the transactions that settled them. Totals are computed, never asserted:
    /// subtotal plus tax equals total on every row, and a paid invoice has a paid transaction
    /// for exactly its total.
    /// </summary>
    private async Task SeedInvoicesAsync(CancellationToken cancellationToken)
    {
        var subscriptions = await context.TenantSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.TenantId == _tenants["ahmed"].Id
                || subscription.TenantId == _tenants["center"].Id
                || subscription.TenantId == _tenants["omar"].Id)
            .ToListAsync(cancellationToken);

        var invoices = new List<Invoice>();
        var transactions = new List<BillingTransaction>();
        var sequence = 0;

        foreach (var subscription in subscriptions.OrderBy(subscription => subscription.TenantId))
        {
            var tenantKey = _tenants.First(entry => entry.Value.Id == subscription.TenantId).Key;
            // omar's subscription is in grace after two declines, so its invoice is overdue
            // and its transaction failed. The other two are settled.
            var isSettled = tenantKey != "omar";

            for (var period = 1; period <= (isSettled ? 2 : 1); period++)
            {
                sequence++;
                var issuedAt = _utcNow.AddMonths(-period).Date;
                var subtotal = Math.Round(subscription.Amount, 2);
                var tax = 0m;
                var total = subtotal + tax;

                var invoiceId = SeedId.For("demo:invoice", tenantKey + ":" + period);

                invoices.Add(new Invoice
                {
                    Id = invoiceId,
                    TenantId = subscription.TenantId,
                    TenantSubscriptionId = subscription.Id,
                    Number = "USTAZ-" + issuedAt.Year + "-" + (900000 + sequence).ToString("D6"),
                    Status = isSettled ? InvoiceStatus.Paid : InvoiceStatus.Overdue,
                    Subtotal = subtotal,
                    TaxAmount = tax,
                    Total = total,
                    Currency = subscription.Currency,
                    IssuedAt = issuedAt,
                    DueAt = issuedAt.AddDays(7),
                    PaidAt = isSettled ? issuedAt.AddDays(1) : null,
                    Description = "اشتراك منصة أستاذ - "
                                  + (subscription.BillingCycle == BillingCycle.Yearly ? "سنوي" : "شهري"),
                    PeriodStart = issuedAt,
                    PeriodEnd = subscription.BillingCycle == BillingCycle.Yearly
                        ? issuedAt.AddYears(1)
                        : issuedAt.AddMonths(1),
                    CreatedAt = issuedAt
                });

                transactions.Add(new BillingTransaction
                {
                    Id = SeedId.For("demo:billing-transaction", tenantKey + ":" + period),
                    TenantId = subscription.TenantId,
                    TenantSubscriptionId = subscription.Id,
                    InvoiceId = invoiceId,
                    Amount = total,
                    Currency = subscription.Currency,
                    Status = isSettled ? PaymentStatus.Paid : PaymentStatus.Failed,
                    Provider = DemoPaymentProvider,
                    // Unique per provider: the schema enforces it, and that uniqueness is
                    // what makes a replayed provider callback idempotent.
                    ProviderTransactionId = "demo-" + tenantKey + "-" + period,
                    ProviderReference = "USTAZ-DEMO-REF-" + (900000 + sequence).ToString("D6"),
                    FailureReason = isSettled ? null : "Card declined by the issuing bank (demo).",
                    PaidAt = isSettled ? issuedAt.AddDays(1) : null,
                    CreatedAt = issuedAt
                });
            }
        }

        await AddMissingAsync("Invoices", invoices, cancellationToken);
        await AddMissingAsync("BillingTransactions", transactions, cancellationToken);
        await SeedWebhookEventsAsync(cancellationToken);
    }

    /// <summary>
    /// Received webhooks, recorded the way the real handler records them.
    ///
    /// Payloads are short, invented JSON with no provider secret and no signature: the
    /// signature column is deliberately left null, because storing something that looks like
    /// a valid signature would be storing a lie. The processed/ignored/failed trio is what a
    /// webhook log screen has to render.
    /// </summary>
    private async Task SeedWebhookEventsAsync(CancellationToken cancellationToken)
    {
        var ahmed = _tenants["ahmed"];
        var omar = _tenants["omar"];

        var events = new List<PaymentWebhookEvent>
        {
            new()
            {
                Id = SeedId.For("demo:webhook", "processed"),
                Provider = DemoPaymentProvider,
                EventId = "demo-evt-payment-succeeded-1",
                EventType = "payment.succeeded",
                Status = WebhookProcessingStatus.Processed,
                Payload = "{\"demo\":true,\"event\":\"payment.succeeded\",\"amount\":499}",
                TenantId = ahmed.Id,
                TenantSubscriptionId = SeedId.For("demo:subscription", "ahmed"),
                BillingTransactionId = SeedId.For("demo:billing-transaction", "ahmed:1"),
                ReceivedAt = _utcNow.AddMonths(-1),
                ProcessedAt = _utcNow.AddMonths(-1).AddSeconds(2),
                AttemptCount = 1,
                CreatedAt = _utcNow.AddMonths(-1)
            },
            new()
            {
                Id = SeedId.For("demo:webhook", "failed"),
                Provider = DemoPaymentProvider,
                EventId = "demo-evt-payment-failed-1",
                EventType = "payment.failed",
                Status = WebhookProcessingStatus.Failed,
                Payload = "{\"demo\":true,\"event\":\"payment.failed\",\"amount\":0}",
                TenantId = omar.Id,
                TenantSubscriptionId = SeedId.For("demo:subscription", "omar"),
                ReceivedAt = _utcNow.AddDays(-5),
                ProcessingError = "Subscription not found for the provider customer id (demo).",
                AttemptCount = 2,
                CreatedAt = _utcNow.AddDays(-5)
            },
            new()
            {
                Id = SeedId.For("demo:webhook", "ignored"),
                Provider = DemoPaymentProvider,
                EventId = "demo-evt-unrelated-1",
                EventType = "invoice.draft_created",
                Status = WebhookProcessingStatus.Ignored,
                Payload = "{\"demo\":true,\"event\":\"invoice.draft_created\"}",
                ReceivedAt = _utcNow.AddDays(-4),
                ProcessedAt = _utcNow.AddDays(-4),
                ProcessingError = "Event type is not handled by USTAZ.",
                AttemptCount = 1,
                CreatedAt = _utcNow.AddDays(-4)
            }
        };

        await AddMissingAsync("PaymentWebhookEvents", events, cancellationToken);
    }
}
