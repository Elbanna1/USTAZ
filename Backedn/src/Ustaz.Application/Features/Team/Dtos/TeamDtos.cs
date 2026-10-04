using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Team.Dtos;

public sealed record TenantMemberDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid UserId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? ProfileImageUrl { get; init; }

    public string? PhoneNumber { get; init; }

    public TenantMemberRole Role { get; init; }

    public string RoleName => Role.ToString();

    public TenantMemberStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public string? Title { get; init; }

    /// <summary>Everything this member may actually do, role defaults included.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Only the explicitly granted rows, which is what the edit screen binds to.</summary>
    public IReadOnlyList<string> ExplicitPermissions { get; init; } = [];

    public DateTime? InvitedAt { get; init; }

    public DateTime? JoinedAt { get; init; }

    public DateTime CreatedAt { get; init; }
}

public sealed record TenantInvitationDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string TenantName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public TenantMemberRole Role { get; init; }

    public string RoleName => Role.ToString();

    public string? Title { get; init; }

    public IReadOnlyList<string> Permissions { get; init; } = [];

    public DateTime ExpiresAt { get; init; }

    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// The raw invitation token, returned exactly once at creation so the caller can deliver
    /// it. Only its hash is stored.
    /// </summary>
    public string? Token { get; init; }
}

/// <summary>The catalogue the permissions editor renders.</summary>
public sealed record PermissionCatalogDto
{
    public IReadOnlyList<PermissionGroupDto> Groups { get; init; } = [];
}

public sealed record PermissionGroupDto
{
    public string Name { get; init; } = string.Empty;

    public IReadOnlyList<string> Permissions { get; init; } = [];
}
