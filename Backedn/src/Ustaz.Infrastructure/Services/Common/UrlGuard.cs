using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Infrastructure.Services.Common;

/// <summary>
/// Rejects URLs that are not absolute http(s), that target loopback/private hosts
/// (a basic SSRF and phishing guard), or that do not belong to the claimed platform.
/// </summary>
public sealed class UrlGuard : IUrlGuard
{
    private static readonly Dictionary<SocialPlatform, string[]> PlatformHosts = new()
    {
        [SocialPlatform.Facebook] = ["facebook.com", "fb.com", "m.facebook.com", "web.facebook.com"],
        [SocialPlatform.Instagram] = ["instagram.com"],
        [SocialPlatform.YouTube] = ["youtube.com", "youtu.be", "m.youtube.com"],
        [SocialPlatform.TikTok] = ["tiktok.com", "vm.tiktok.com"],
        [SocialPlatform.LinkedIn] = ["linkedin.com"],
        [SocialPlatform.WhatsApp] = ["wa.me", "whatsapp.com", "chat.whatsapp.com", "api.whatsapp.com"]
    };

    public bool IsSafeHttpUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (uri.IsLoopback)
        {
            return false;
        }

        var host = uri.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.StartsWith("10.", StringComparison.Ordinal)
            || host.StartsWith("192.168.", StringComparison.Ordinal)
            || host.StartsWith("169.254.", StringComparison.Ordinal)
            || host.StartsWith("127.", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    public bool MatchesPlatform(string? url, SocialPlatform platform)
    {
        if (!IsSafeHttpUrl(url))
        {
            return false;
        }

        if (!PlatformHosts.TryGetValue(platform, out var hosts))
        {
            return false;
        }

        var host = new Uri(url!.Trim()).Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return hosts.Any(allowed => host == allowed || host.EndsWith("." + allowed, StringComparison.Ordinal));
    }

    public string EnsureSafeHttpUrl(string url, string fieldName)
    {
        var trimmed = url?.Trim() ?? string.Empty;

        if (!IsSafeHttpUrl(trimmed))
        {
            throw new BusinessRuleException(
                "INVALID_URL",
                fieldName + " must be a valid public http or https URL.");
        }

        return trimmed;
    }
}
