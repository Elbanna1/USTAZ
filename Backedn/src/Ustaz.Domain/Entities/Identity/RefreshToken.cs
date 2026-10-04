using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities.Identity;

/// <summary>
/// A rotating refresh token. Only the SHA-256 hash of the token is persisted, so a database
/// leak cannot be replayed against the API.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    /// <summary>SHA-256 hash (Base64) of the opaque token handed to the client.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Groups every token derived from the same original login, enabling family revocation.</summary>
    public Guid FamilyId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    /// <summary>Hash of the token that replaced this one during rotation.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }

    public string? UserAgent { get; set; }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;

    public bool IsRevoked => RevokedAt is not null;

    public bool IsActive(DateTime utcNow) => !IsRevoked && !IsExpired(utcNow);

    public void Revoke(DateTime utcNow, string reason, string? replacedByTokenHash = null)
    {
        RevokedAt = utcNow;
        RevokedReason = reason;
        ReplacedByTokenHash = replacedByTokenHash;
        UpdatedAt = utcNow;
    }
}
