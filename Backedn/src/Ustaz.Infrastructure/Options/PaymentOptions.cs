namespace Ustaz.Infrastructure.Options;

/// <summary>
/// Payment provider configuration. Webhook secrets are keyed by provider name and must come
/// from environment variables or a secret store; there is no default, and a provider without
/// a secret has its webhooks rejected.
/// </summary>
public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    /// <summary>Provider name (lower-case) to HMAC signing secret.</summary>
    public Dictionary<string, string> WebhookSecrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Provider used when a checkout does not name one.</summary>
    public string DefaultProvider { get; set; } = "manual";
}
