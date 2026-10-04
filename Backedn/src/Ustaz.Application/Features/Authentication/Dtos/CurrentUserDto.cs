namespace Ustaz.Application.Features.Authentication.Dtos;

/// <summary>The authenticated principal as the frontend sees it.</summary>
public sealed record CurrentUserDto
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? ProfileImageUrl { get; init; }

    public string? PhoneNumber { get; init; }

    public string? City { get; init; }

    /// <summary>The language this account reads the platform in: <c>ar-EG</c> or <c>en</c>.</summary>
    public string PreferredLanguage { get; init; } = "ar-EG";

    public bool EmailConfirmed { get; init; }

    public bool IsActive { get; init; }

    public bool IsVerified { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>Present when the user holds the Teacher role.</summary>
    public Guid? TeacherId { get; init; }

    /// <summary>Present when the user holds the Student role.</summary>
    public Guid? StudentId { get; init; }

    /// <summary>Present when the user holds the Parent role.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>The workspace the user is acting inside, if any.</summary>
    public Guid? TenantId { get; init; }

    public string? TenantSlug { get; init; }

    public string? TenantName { get; init; }

    /// <summary>The user's role inside the active workspace.</summary>
    public Ustaz.Domain.Enums.TenantMemberRole? TenantRole { get; init; }

    public DateTime CreatedAt { get; init; }
}
