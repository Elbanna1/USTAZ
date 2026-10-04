using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ustaz.Application.Features.Billing;
using Ustaz.Infrastructure.Options;

namespace Ustaz.Infrastructure.Services.Common;

/// <summary>
/// HMAC-SHA256 verification of payment webhooks.
///
/// Secrets come from configuration, one per provider. When a provider has no configured
/// secret the webhook is rejected: failing closed is the only safe default, because a
/// forged callback would otherwise be able to activate a paid subscription.
/// </summary>
public sealed class PaymentWebhookVerifier(
    IOptions<PaymentOptions> options,
    ILogger<PaymentWebhookVerifier> logger) : IPaymentWebhookVerifier
{
    private readonly PaymentOptions _options = options.Value;

    public bool IsConfigured(string provider) =>
        !string.IsNullOrWhiteSpace(ResolveSecret(provider));

    public bool IsSignatureValid(string provider, string rawBody, string? signatureHeader)
    {
        var secret = ResolveSecret(provider);

        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning(
                "Rejected a webhook for provider {Provider}: no signing secret is configured.",
                provider);

            return false;
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        var expected = Compute(secret, rawBody);

        // Providers send the digest in different shapes, e.g. "sha256=<hex>" or "t=..,v1=..".
        var candidates = signatureHeader
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Contains('=') ? part[(part.IndexOf('=') + 1)..] : part)
            .Append(signatureHeader.Trim());

        foreach (var candidate in candidates)
        {
            if (FixedTimeEquals(expected, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private string? ResolveSecret(string provider) =>
        _options.WebhookSecrets.TryGetValue(provider, out var secret) ? secret : null;

    private static string Compute(string secret, string payload)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Length-independent, constant-time comparison so timing cannot leak the digest.</summary>
    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual.Trim().ToLowerInvariant());

        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
