using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities.Tenancy;

/// <summary>
/// A pending invitation to join a workspace. Only the SHA-256 hash of the invitation token is
/// stored, mirroring how refresh tokens are handled.
/// </summary>
public class TenantInvitation : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public string Email { get; set; } = string.Empty;

    public TenantMemberRole Role { get; set; } = TenantMemberRole.Assistant;

    public string? Title { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime? DeclinedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public Guid InvitedByUserId { get; set; }

    /// <summary>Permissions to apply once the invitation is accepted.</summary>
    public string? PermissionsCsv { get; set; }

    public bool IsPending => AcceptedAt is null && DeclinedAt is null && RevokedAt is null;

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;

    public void EnsureRedeemable(DateTime utcNow)
    {
        if (!IsPending)
        {
            throw new BusinessRuleException("INVITATION_NOT_PENDING", "This invitation has already been used.");
        }

        if (IsExpired(utcNow))
        {
            throw new BusinessRuleException("INVITATION_EXPIRED", "This invitation has expired. Ask for a new one.");
        }
    }

    public IReadOnlyList<string> Permissions() =>
        string.IsNullOrWhiteSpace(PermissionsCsv)
            ? []
            : [.. PermissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
