using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Billing;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Billing;

/// <summary>Verifies that a webhook payload really came from the payment provider.</summary>
public interface IPaymentWebhookVerifier
{
    bool IsConfigured(string provider);

    /// <summary>Constant-time HMAC comparison of the raw body against the signature header.</summary>
    bool IsSignatureValid(string provider, string rawBody, string? signatureHeader);
}

public sealed record PaymentWebhookResultDto
{
    public required string Status { get; init; }

    public required string Message { get; init; }

    public Guid? WebhookEventId { get; init; }

    /// <summary>True when this exact event had already been processed.</summary>
    public bool WasDuplicate { get; init; }
}

/// <summary>
/// Handles a provider callback.
///
/// Three things make this safe to call repeatedly:
/// 1. The signature is verified before anything is written.
/// 2. Every event is recorded under a unique (provider, eventId) index; a replay is detected
///    and short-circuits.
/// 3. The transaction is also keyed on (provider, providerTransactionId), so even a
///    provider that reuses event ids cannot double-credit a payment.
/// </summary>
public sealed record ProcessPaymentWebhookCommand : IRequest<PaymentWebhookResultDto>
{
    public required string Provider { get; init; }

    /// <summary>The provider's own event identifier.</summary>
    public required string EventId { get; init; }

    public required string EventType { get; init; }

    /// <summary>Exact bytes received, needed for signature verification.</summary>
    public required string RawBody { get; init; }

    public string? SignatureHeader { get; init; }

    /// <summary>Reference issued at checkout, echoed back by the provider.</summary>
    public string? ProviderReference { get; init; }

    public string? ProviderTransactionId { get; init; }

    public decimal? Amount { get; init; }

    public string? Currency { get; init; }
}

public sealed class ProcessPaymentWebhookCommandValidator : AbstractValidator<ProcessPaymentWebhookCommand>
{
    public ProcessPaymentWebhookCommandValidator()
    {
        RuleFor(command => command.Provider).NotEmpty().MaximumLength(50);
        RuleFor(command => command.EventId).NotEmpty().MaximumLength(200);
        RuleFor(command => command.EventType).NotEmpty().MaximumLength(100);
        RuleFor(command => command.RawBody).NotNull();
    }
}

public sealed class ProcessPaymentWebhookCommandHandler(
    IApplicationDbContext context,
    IPaymentWebhookVerifier verifier,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    ILogger<ProcessPaymentWebhookCommandHandler> logger)
    : IRequestHandler<ProcessPaymentWebhookCommand, PaymentWebhookResultDto>
{
    /// <summary>Event types that mean "money received".</summary>
    private static readonly string[] SuccessEvents =
    [
        "payment.succeeded", "payment_intent.succeeded", "charge.succeeded",
        "invoice.paid", "transaction.completed", "TRANSACTION_SUCCESS"
    ];

    private static readonly string[] FailureEvents =
    [
        "payment.failed", "payment_intent.payment_failed", "charge.failed",
        "invoice.payment_failed", "transaction.failed", "TRANSACTION_FAILED"
    ];

    public async Task<PaymentWebhookResultDto> Handle(
        ProcessPaymentWebhookCommand command,
        CancellationToken cancellationToken)
    {
        var provider = command.Provider.Trim().ToLowerInvariant();
        var utcNow = dateTimeProvider.UtcNow;

        // 1. Authenticity first: an unsigned or wrongly signed payload is never recorded.
        if (!verifier.IsSignatureValid(provider, command.RawBody, command.SignatureHeader))
        {
            logger.LogWarning("Rejected a {Provider} webhook with an invalid signature.", provider);
            throw new ForbiddenAccessException("The webhook signature could not be verified.");
        }

        // 2. Replay protection: the unique index on (Provider, EventId) is the contract.
        var existing = await context.PaymentWebhookEvents
            .FirstOrDefaultAsync(
                entity => entity.Provider == provider && entity.EventId == command.EventId,
                cancellationToken);

        if (existing is not null)
        {
            logger.LogInformation(
                "Ignored a duplicate {Provider} webhook {EventId} (already {Status}).",
                provider,
                command.EventId,
                existing.Status);

            return new PaymentWebhookResultDto
            {
                Status = existing.Status.ToString(),
                Message = "This event was already received and will not be applied again.",
                WebhookEventId = existing.Id,
                WasDuplicate = true
            };
        }

        var webhookEvent = new PaymentWebhookEvent
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            EventId = command.EventId,
            EventType = command.EventType,
            Status = WebhookProcessingStatus.Received,
            Payload = Truncate(command.RawBody, 8000),
            SignatureHeader = Truncate(command.SignatureHeader, 512),
            ReceivedAt = utcNow,
            CreatedAt = utcNow
        };

        await context.PaymentWebhookEvents.AddAsync(webhookEvent, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            var transaction = await ResolveTransactionAsync(provider, command, cancellationToken);

            if (transaction is null)
            {
                webhookEvent.MarkIgnored(utcNow, "No matching transaction for the supplied reference.");
                await context.SaveChangesAsync(cancellationToken);

                return new PaymentWebhookResultDto
                {
                    Status = webhookEvent.Status.ToString(),
                    Message = "No matching transaction; the event was recorded and ignored.",
                    WebhookEventId = webhookEvent.Id
                };
            }

            webhookEvent.TenantId = transaction.TenantId;
            webhookEvent.BillingTransactionId = transaction.Id;

            if (IsSuccess(command.EventType))
            {
                await ApplySuccessAsync(transaction, command, webhookEvent, utcNow, cancellationToken);
            }
            else if (IsFailure(command.EventType))
            {
                await ApplyFailureAsync(transaction, webhookEvent, utcNow, cancellationToken);
            }
            else
            {
                webhookEvent.MarkIgnored(utcNow, "Event type is not actionable.");
            }

            await context.SaveChangesAsync(cancellationToken);

            return new PaymentWebhookResultDto
            {
                Status = webhookEvent.Status.ToString(),
                Message = "Webhook processed.",
                WebhookEventId = webhookEvent.Id
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to process the {Provider} webhook {EventId}.", provider, command.EventId);

            webhookEvent.MarkFailed(utcNow, exception.Message);
            await context.SaveChangesAsync(cancellationToken);

            throw;
        }
    }

    /// <summary>
    /// Applies a confirmed payment: settles the transaction and invoice and renews the
    /// subscription. A transaction already marked Paid is left alone, which is the second
    /// layer of idempotency.
    /// </summary>
    private async Task ApplySuccessAsync(
        BillingTransaction transaction,
        ProcessPaymentWebhookCommand command,
        PaymentWebhookEvent webhookEvent,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (transaction.Status == PaymentStatus.Paid)
        {
            webhookEvent.MarkIgnored(utcNow, "The transaction was already settled.");
            return;
        }

        transaction.Status = PaymentStatus.Paid;
        transaction.PaidAt = utcNow;
        transaction.ProviderTransactionId ??= command.ProviderTransactionId;
        transaction.UpdatedAt = utcNow;

        if (transaction.InvoiceId is not null)
        {
            var invoice = await context.Invoices
                .FirstOrDefaultAsync(entity => entity.Id == transaction.InvoiceId.Value, cancellationToken);

            invoice?.MarkPaid(utcNow);
        }

        var subscription = await context.TenantSubscriptions
            .Include(entity => entity.Plan)
            .FirstOrDefaultAsync(entity => entity.TenantId == transaction.TenantId, cancellationToken);

        if (subscription is not null)
        {
            var cycle = ResolveCycle(transaction.ProviderReference, subscription.BillingCycle);

            // If the checkout was for a different plan, move the subscription onto it first.
            var planCode = ResolvePlanCode(transaction.ProviderReference);

            if (planCode is not null && !string.Equals(planCode, subscription.Plan.Code, StringComparison.Ordinal))
            {
                var plan = await context.SubscriptionPlans
                    .Include(entity => entity.Features)
                    .FirstOrDefaultAsync(entity => entity.Code == planCode, cancellationToken);

                if (plan is not null)
                {
                    subscription.ChangePlan(plan, cycle, utcNow);
                }
            }

            // A first successful payment turns a trial or a lapsed plan into a live one;
            // that reads differently to the owner than a routine renewal.
            var wasLive = subscription.Status == SubscriptionStatus.Active;

            subscription.MarkPaid(utcNow, cycle, transaction.Amount);
            webhookEvent.TenantSubscriptionId = subscription.Id;

            await NotifyOwnerAsync(
                subscription.TenantId,
                new NotificationRequest
                {
                    Event = wasLive
                        ? NotificationEvents.SubscriptionRenewed
                        : NotificationEvents.SubscriptionActivated,
                    TenantId = subscription.TenantId,
                    ReferenceId = subscription.Id,
                    // One receipt per provider event, however often it is replayed.
                    DeduplicationKey = "subscription-renewed:" + webhookEvent.EventId,
                    Values = new Dictionary<string, string>
                    {
                        ["plan"] = subscription.Plan != null ? subscription.Plan.NameAr : "الباقة",
                        ["date"] = subscription.CurrentPeriodEnd.ToString("yyyy-MM-dd")
                    }
                },
                cancellationToken);
        }

        webhookEvent.MarkProcessed(utcNow);
    }

    private async Task ApplyFailureAsync(
        BillingTransaction transaction,
        PaymentWebhookEvent webhookEvent,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (transaction.Status == PaymentStatus.Paid)
        {
            webhookEvent.MarkIgnored(utcNow, "A settled transaction cannot be failed by a later event.");
            return;
        }

        transaction.Status = PaymentStatus.Failed;
        transaction.FailureReason = "The payment provider reported a failure.";
        transaction.UpdatedAt = utcNow;

        var subscription = await context.TenantSubscriptions
            .FirstOrDefaultAsync(entity => entity.TenantId == transaction.TenantId, cancellationToken);

        if (subscription is not null)
        {
            subscription.MarkPaymentFailed(utcNow, transaction.FailureReason);
            webhookEvent.TenantSubscriptionId = subscription.Id;

            await NotifyOwnerAsync(
                subscription.TenantId,
                new NotificationRequest
                {
                    Event = NotificationEvents.SubscriptionPaymentFailed,
                    TenantId = subscription.TenantId,
                    ReferenceId = subscription.Id,
                    DeduplicationKey = "subscription-failed:" + webhookEvent.EventId,
                    Values = new Dictionary<string, string>
                    {
                        ["amount"] = transaction.Amount.ToString("0.##")
                    }
                },
                cancellationToken);
        }

        webhookEvent.MarkProcessed(utcNow);
    }

    private async Task<BillingTransaction?> ResolveTransactionAsync(
        string provider,
        ProcessPaymentWebhookCommand command,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.ProviderTransactionId))
        {
            var byProviderId = await context.BillingTransactions
                .FirstOrDefaultAsync(
                    entity => entity.Provider == provider
                        && entity.ProviderTransactionId == command.ProviderTransactionId,
                    cancellationToken);

            if (byProviderId is not null)
            {
                return byProviderId;
            }
        }

        if (!string.IsNullOrWhiteSpace(command.ProviderReference))
        {
            return await context.BillingTransactions
                .FirstOrDefaultAsync(
                    entity => entity.ProviderReference == command.ProviderReference,
                    cancellationToken);
        }

        return null;
    }

    /// <summary>
    /// Billing news goes to the workspace owner. Both events are Critical in the catalog, so
    /// a failed renewal reaches them even if they muted subscription notifications.
    /// </summary>
    private async Task NotifyOwnerAsync(
        Guid tenantId,
        NotificationRequest request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await context.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => (Guid?)tenant.OwnerUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerUserId is null)
        {
            return;
        }

        await notifications.SendAsync(ownerUserId.Value, request, cancellationToken);
    }

    private static bool IsSuccess(string eventType) =>
        SuccessEvents.Contains(eventType, StringComparer.OrdinalIgnoreCase);

    private static bool IsFailure(string eventType) =>
        FailureEvents.Contains(eventType, StringComparer.OrdinalIgnoreCase);

    /// <summary>Reference format: ustaz-{invoiceNumber}-{planCode}-{cycle}.</summary>
    private static BillingCycle ResolveCycle(string? reference, BillingCycle fallback)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return fallback;
        }

        return reference.EndsWith(BillingCycle.Yearly.ToString(), StringComparison.OrdinalIgnoreCase)
            ? BillingCycle.Yearly
            : BillingCycle.Monthly;
    }

    private static string? ResolvePlanCode(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var parts = reference.Split('-', StringSplitOptions.RemoveEmptyEntries);

        // ustaz | USTAZ | 2026 | 000001 | planCode | cycle
        return parts.Length >= 2 ? parts[^2] : null;
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
