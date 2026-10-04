using Ustaz.Application.Features.Billing;
using Ustaz.Application.Features.Billing.Dtos;

namespace Ustaz.API.Controllers;

/// <summary>
/// USTAZ SaaS billing: what a teacher or centre pays the platform.
/// Entirely separate from tuition, which students pay teachers.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing")]
[ApiExplorerSettings(GroupName = "Billing")]
public sealed class BillingController : ApiControllerBase
{
    /// <summary>The public price list.</summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    [SwaggerOperation(Tags = ["Billing"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriptionPlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetSubscriptionPlansQuery(), cancellationToken));

    /// <summary>One plan, with its quotas and features.</summary>
    [HttpGet("plans/{id:guid}")]
    [AllowAnonymous]
    [SwaggerOperation(Tags = ["Billing"])]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlan(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetSubscriptionPlanQuery(id), cancellationToken));

    /// <summary>The caller's subscription, with live quota usage.</summary>
    [HttpGet("subscription")]
    [Authorize(Policy = PolicyNames.TenantMember)]
    [SwaggerOperation(Tags = ["Billing"])]
    [ProducesResponseType(typeof(ApiResponse<TenantSubscriptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscription(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetMySubscriptionQuery(), cancellationToken));

    /// <summary>
    /// Starts a payment: issues an invoice and a pending transaction. The subscription is
    /// NOT activated here; only a verified provider webhook can do that.
    /// </summary>
    [HttpPost("checkout")]
    [Authorize(Policy = PolicyNames.TenantMember)]
    [SwaggerOperation(Tags = ["Billing"])]
    [ProducesResponseType(typeof(ApiResponse<CheckoutSessionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Checkout(
        [FromBody] CreateCheckoutCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command, cancellationToken),
            "Checkout started. Complete the payment to activate the plan.");

    /// <summary>Switches plan. A downgrade below current usage is refused.</summary>
    [HttpPost("change-plan")]
    [Authorize(Policy = PolicyNames.TenantMember)]
    [SwaggerOperation(Tags = ["Billing"])]
    [ProducesResponseType(typeof(ApiResponse<TenantSubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangePlan(
        [FromBody] ChangePlanCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Plan changed successfully.");

    /// <summary>Cancels the subscription, by default at the end of the paid period.</summary>
    [HttpPost("cancel")]
    [Authorize(Policy = PolicyNames.TenantMember)]
    [SwaggerOperation(Tags = ["Billing"])]
    [ProducesResponseType(typeof(ApiResponse<TenantSubscriptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(
        [FromBody] CancelSubscriptionCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Subscription cancelled.");

    /// <summary>Reactivates a cancelled subscription.</summary>
    [HttpPost("reactivate")]
    [Authorize(Policy = PolicyNames.TenantMember)]
    [SwaggerOperation(Tags = ["Billing"])]
    [ProducesResponseType(typeof(ApiResponse<TenantSubscriptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reactivate(CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(new ReactivateSubscriptionCommand(), cancellationToken),
            "Subscription reactivated.");

    /// <summary>The workspace's SaaS invoices.</summary>
    [HttpGet("invoices")]
    [Authorize(Policy = PolicyNames.TenantMember)]
    [SwaggerOperation(Tags = ["Billing"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<InvoiceDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetInvoices(
        [FromQuery] GetMyInvoicesQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>One SaaS invoice.</summary>
    [HttpGet("invoices/{id:guid}")]
    [Authorize(Policy = PolicyNames.TenantMember)]
    [SwaggerOperation(Tags = ["Billing"])]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoice(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetInvoiceQuery(id), cancellationToken));

    /// <summary>The workspace's SaaS payment attempts.</summary>
    [HttpGet("transactions")]
    [Authorize(Policy = PolicyNames.TenantMember)]
    [SwaggerOperation(Tags = ["Billing"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<BillingTransactionDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetTransactions(
        [FromQuery] GetMyTransactionsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>One SaaS transaction.</summary>
    [HttpGet("transactions/{id:guid}")]
    [Authorize(Policy = PolicyNames.TenantMember)]
    [SwaggerOperation(Tags = ["Billing"])]
    [ProducesResponseType(typeof(ApiResponse<BillingTransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransaction(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTransactionQuery(id), cancellationToken));

    /// <summary>
    /// Payment provider callback. The signature is verified before anything is written, and
    /// the (provider, eventId) pair is unique, so replays are detected and never re-applied.
    /// </summary>
    [HttpPost("webhooks/{provider}")]
    [AllowAnonymous]
    [SwaggerOperation(Tags = ["Billing"])]
    [ProducesResponseType(typeof(ApiResponse<PaymentWebhookResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Webhook(string provider, CancellationToken cancellationToken)
    {
        // The raw body is required verbatim for HMAC verification.
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var payload = WebhookPayloadReader.Parse(rawBody);

        var result = await Mediator.Send(
            new ProcessPaymentWebhookCommand
            {
                Provider = provider,
                EventId = payload.EventId ?? Request.Headers["X-Event-Id"].ToString(),
                EventType = payload.EventType ?? Request.Headers["X-Event-Type"].ToString(),
                RawBody = rawBody,
                SignatureHeader = ResolveSignature(),
                ProviderReference = payload.Reference,
                ProviderTransactionId = payload.TransactionId,
                Amount = payload.Amount,
                Currency = payload.Currency
            },
            cancellationToken);

        return OkResponse(result, result.Message);
    }

    /// <summary>Providers use different header names for the signature digest.</summary>
    private string? ResolveSignature()
    {
        foreach (var header in new[] { "X-Signature", "X-Hub-Signature-256", "Stripe-Signature", "X-Paymob-Signature" })
        {
            if (Request.Headers.TryGetValue(header, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.ToString();
            }
        }

        return null;
    }
}

/// <summary>Reads the fields USTAZ needs out of a provider payload, tolerating naming differences.</summary>
internal static class WebhookPayloadReader
{
    public sealed record Payload(
        string? EventId,
        string? EventType,
        string? Reference,
        string? TransactionId,
        decimal? Amount,
        string? Currency);

    public static Payload Parse(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return new Payload(null, null, null, null, null, null);
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(rawBody);
            var root = document.RootElement;

            return new Payload(
                ReadString(root, "id", "eventId", "event_id"),
                ReadString(root, "type", "eventType", "event_type"),
                ReadString(root, "reference", "providerReference", "merchant_order_id", "order_id"),
                ReadString(root, "transactionId", "transaction_id", "paymentId", "payment_id"),
                ReadDecimal(root, "amount", "amount_cents", "total"),
                ReadString(root, "currency"));
        }
        catch (System.Text.Json.JsonException)
        {
            return new Payload(null, null, null, null, null, null);
        }
    }

    private static string? ReadString(System.Text.Json.JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return value.GetString();
                }

                if (value.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    return value.ToString();
                }
            }
        }

        return null;
    }

    private static decimal? ReadDecimal(System.Text.Json.JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value)
                && value.ValueKind == System.Text.Json.JsonValueKind.Number
                && value.TryGetDecimal(out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}
