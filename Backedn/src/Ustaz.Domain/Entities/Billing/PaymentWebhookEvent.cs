using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities.Billing;

/// <summary>
/// Every webhook USTAZ receives, recorded before it is acted on. The unique index on
/// (Provider, EventId) is the idempotency guarantee: a replayed webhook is detected here and
/// never re-applies its business effect.
/// </summary>
public class PaymentWebhookEvent : BaseEntity
{
    public string Provider { get; set; } = string.Empty;

    /// <summary>The provider's event identifier; unique per provider.</summary>
    public string EventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public WebhookProcessingStatus Status { get; set; } = WebhookProcessingStatus.Received;

    public string? Payload { get; set; }

    public string? SignatureHeader { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? TenantSubscriptionId { get; set; }

    public Guid? BillingTransactionId { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    public string? ProcessingError { get; set; }

    public int AttemptCount { get; set; }

    public void MarkProcessed(DateTime utcNow)
    {
        Status = WebhookProcessingStatus.Processed;
        ProcessedAt = utcNow;
        ProcessingError = null;
        UpdatedAt = utcNow;
    }

    public void MarkFailed(DateTime utcNow, string error)
    {
        Status = WebhookProcessingStatus.Failed;
        ProcessingError = error;
        AttemptCount++;
        UpdatedAt = utcNow;
    }

    public void MarkIgnored(DateTime utcNow, string reason)
    {
        Status = WebhookProcessingStatus.Ignored;
        ProcessingError = reason;
        ProcessedAt = utcNow;
        UpdatedAt = utcNow;
    }
}
