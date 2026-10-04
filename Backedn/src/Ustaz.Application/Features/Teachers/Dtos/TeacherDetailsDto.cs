using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Teachers.Dtos;

/// <summary>Full public teacher profile page payload.</summary>
public sealed record TeacherDetailsDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string? Headline { get; init; }

    public string? Bio { get; init; }

    public string? ProfileImageUrl { get; init; }

    public string? CoverImageUrl { get; init; }

    public string? City { get; init; }

    public int YearsOfExperience { get; init; }

    public int StudentsCount { get; init; }

    public decimal Rating { get; init; }

    public int ReviewCount { get; init; }

    public bool IsFeatured { get; init; }

    public bool IsVerified { get; init; }

    public VerificationStatus VerificationStatus { get; init; }

    public TeachingMode PreferredTeachingMode { get; init; }

    public string? WhatsAppLink { get; init; }

    public int GroupsCount { get; init; }

    public int CoursesCount { get; init; }

    public DateTime CreatedAt { get; init; }

    public IReadOnlyList<SubjectDto> Subjects { get; init; } = [];

    public IReadOnlyList<GradeDto> Grades { get; init; } = [];

    public IReadOnlyList<TeacherAchievementDto> Achievements { get; init; } = [];

    public IReadOnlyList<TeacherSocialLinkDto> SocialLinks { get; init; } = [];
}
