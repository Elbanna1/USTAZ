using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Tenants.Dtos;

public sealed record TenantDto
{
    public Guid Id { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public TenantStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public Guid OwnerUserId { get; init; }

    public string? OwnerDisplayName { get; init; }

    public string? ContactEmail { get; init; }

    public string? ContactPhone { get; init; }

    public string? WhatsAppNumber { get; init; }

    public string? WhatsAppLink { get; init; }

    public string? City { get; init; }

    public int MembersCount { get; init; }

    public int StudentsCount { get; init; }

    public int GroupsCount { get; init; }

    public int CoursesCount { get; init; }

    public TenantBrandingDto? Branding { get; init; }

    public DateTime CreatedAt { get; init; }
}

public sealed record TenantSettingsDto
{
    public Guid TenantId { get; init; }

    public string DefaultLanguage { get; init; } = "ar";

    public string TimeZoneId { get; init; } = "Egypt Standard Time";

    public string Currency { get; init; } = "EGP";

    public int GradeNotificationThresholdPercentage { get; init; }

    public bool NotifyParentsOnAbsence { get; init; }

    public bool NotifyParentsOnLowGrade { get; init; }

    public bool NotifyParentsOnHomeworkMissed { get; init; }

    public bool AllowStudentSelfEnrollment { get; init; }

    public bool AutoApproveReviews { get; init; }

    public int LateThresholdMinutes { get; init; }
}

public sealed record TenantBrandingDto
{
    public Guid TenantId { get; init; }

    public string? LogoUrl { get; init; }

    public string? CoverImageUrl { get; init; }

    public string? FaviconUrl { get; init; }

    public string PrimaryColorHex { get; init; } = "#2563EB";

    public string SecondaryColorHex { get; init; } = "#1E293B";

    public string AccentColorHex { get; init; } = "#F59E0B";

    public string? FontFamily { get; init; }

    public string? Tagline { get; init; }
}

public sealed record TenantDomainDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Host { get; init; } = string.Empty;

    public bool IsPrimary { get; init; }

    public DomainVerificationStatus VerificationStatus { get; init; }

    public string VerificationStatusName => VerificationStatus.ToString();

    /// <summary>DNS record the tenant must publish to prove ownership.</summary>
    public string VerificationRecordName { get; init; } = string.Empty;

    public string VerificationRecordValue { get; init; } = string.Empty;

    public DateTime? VerifiedAt { get; init; }

    public string? LastVerificationError { get; init; }

    public DateTime CreatedAt { get; init; }
}
