using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities;

/// <summary>
/// One attempt to deliver one transactional email.
///
/// This is a record, not a queue: the email is sent inline and the row says what happened, so
/// support can answer "did the reset email actually go out" without reading SMTP logs. A
/// failed, retryable row is picked up by the retry pass and re-rendered from
/// <see cref="PayloadJson"/>.
///
/// Deliberately absent: the rendered body, the recipient's password, and any token. A
/// security email stores no payload at all, which is why it is never retried — a stale reset
/// link is worse than no link.
/// </summary>
public class EmailDelivery : BaseEntity
{
    /// <summary>Catalog key, e.g. <c>security.password_changed</c>.</summary>
    public string Event { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>Recipient address. Needed to retry; never written to a log.</summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>The subject line as sent, for support and for auditing.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>The language the message was rendered in, e.g. <c>ar-EG</c>.</summary>
    public string Language { get; set; } = "ar-EG";

    public EmailDeliveryStatus Status { get; set; } = EmailDeliveryStatus.Pending;

    public int Attempts { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime? FailedAt { get; set; }

    /// <summary>Coarse reason a send failed, e.g. <c>SmtpUnavailable</c>. Never the raw body.</summary>
    public string? ErrorCategory { get; set; }

    /// <summary>Short, sanitised failure message. Truncated and never contains credentials.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Ties the email to the request that produced it.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Makes "the same email again" detectable. Unique per recipient.</summary>
    public string? DeduplicationKey { get; set; }

    public Guid? RelatedEntityId { get; set; }

    public string? RelatedEntityType { get; set; }

    /// <summary>
    /// The template values, so a failed email can be re-rendered on retry. Null for security
    /// emails, whose values may contain a one-time link.
    /// </summary>
    public string? PayloadJson { get; set; }

    public bool IsRetryable => Status == EmailDeliveryStatus.Failed && PayloadJson is not null;

    public void MarkSent(DateTime utcNow)
    {
        Status = EmailDeliveryStatus.Sent;
        SentAt = utcNow;
        Attempts++;
        UpdatedAt = utcNow;
        ErrorCategory = null;
        ErrorMessage = null;
    }

    public void MarkFailed(DateTime utcNow, string category, string? message, int maxAttempts)
    {
        Attempts++;
        FailedAt = utcNow;
        UpdatedAt = utcNow;
        ErrorCategory = category;

        // Truncated on the way in: a provider message is diagnostics, not a place for detail.
        ErrorMessage = message is null
            ? null
            : message.Length > 400 ? message[..400] : message;

        Status = Attempts >= maxAttempts
            ? EmailDeliveryStatus.Abandoned
            : EmailDeliveryStatus.Failed;
    }

    public void MarkSkipped(DateTime utcNow, string reason)
    {
        Status = EmailDeliveryStatus.Skipped;
        ErrorCategory = reason;
        UpdatedAt = utcNow;
    }
}
