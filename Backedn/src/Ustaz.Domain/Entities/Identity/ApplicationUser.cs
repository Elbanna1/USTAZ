using Microsoft.AspNetCore.Identity;

namespace Ustaz.Domain.Entities.Identity;

/// <summary>
/// The USTAZ account. Authentication concerns live in ASP.NET Core Identity while the
/// role specific data lives in <see cref="TeacherProfile"/> / <see cref="StudentProfile"/>.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? ProfileImageUrl { get; set; }

    /// <summary>Storage id of the profile image, kept so the asset can be replaced or deleted.</summary>
    public string? ProfileImagePublicId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Mirrors the verification state of the linked teacher profile for cheap lookups.</summary>
    public bool IsVerified { get; set; }

    public string? City { get; set; }

    /// <summary>
    /// The language this account reads the platform in: <c>ar-EG</c> (Egyptian Arabic, the
    /// default) or <c>en</c>. Notifications and emails are rendered from it, so a single
    /// field decides both channels rather than each guessing.
    /// </summary>
    public string PreferredLanguage { get; set; } = "ar-EG";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public TeacherProfile? TeacherProfile { get; set; }

    public StudentProfile? StudentProfile { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    public ICollection<Notification> Notifications { get; set; } = [];

    public Education.ParentProfile? ParentProfile { get; set; }

    /// <summary>Workspaces this user is a member of (owner, assistant, ...).</summary>
    public ICollection<Tenancy.TenantMember> TenantMemberships { get; set; } = [];

    public ICollection<Education.NotificationPreference> NotificationPreferences { get; set; } = [];

    public string FullName => string.IsNullOrWhiteSpace(DisplayName)
        ? $"{FirstName} {LastName}".Trim()
        : DisplayName;
}
