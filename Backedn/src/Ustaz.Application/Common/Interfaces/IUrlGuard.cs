using Ustaz.Domain.Enums;

namespace Ustaz.Application.Common.Interfaces;

/// <summary>Validates externally supplied URLs (social links, meeting links, material links).</summary>
public interface IUrlGuard
{
    /// <summary>True when the URL is absolute, uses http(s) and is not a private/loopback host.</summary>
    bool IsSafeHttpUrl(string? url);

    /// <summary>True when the URL belongs to the expected social platform.</summary>
    bool MatchesPlatform(string? url, SocialPlatform platform);

    /// <summary>Returns a trimmed, safe URL or throws a business rule exception.</summary>
    string EnsureSafeHttpUrl(string url, string fieldName);
}
