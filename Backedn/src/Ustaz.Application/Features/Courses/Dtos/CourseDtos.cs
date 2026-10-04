using Ustaz.Application.Features.Shared.Dtos;

namespace Ustaz.Application.Features.Courses.Dtos;

/// <summary>Card shape for course discovery listings.</summary>
public sealed record CourseListItemDto
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? ThumbnailUrl { get; init; }

    public PricingDto Pricing { get; init; } = new();

    public int DurationInMinutes { get; init; }

    public int LessonsCount { get; init; }

    public decimal Rating { get; init; }

    public int ReviewCount { get; init; }

    public int StudentCount { get; init; }

    public bool IsPublished { get; init; }

    public bool IsFeatured { get; init; }

    public TeacherSummaryDto Teacher { get; init; } = new();

    public SubjectDto Subject { get; init; } = new();

    public GradeDto Grade { get; init; } = new();

    public DateTime CreatedAt { get; init; }
}

/// <summary>Full course page payload.</summary>
public sealed record CourseDetailsDto
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? ThumbnailUrl { get; init; }

    public PricingDto Pricing { get; init; } = new();

    public int DurationInMinutes { get; init; }

    public int LessonsCount { get; init; }

    public decimal Rating { get; init; }

    public int ReviewCount { get; init; }

    public int StudentCount { get; init; }

    public bool IsPublished { get; init; }

    public bool IsFeatured { get; init; }

    public TeacherSummaryDto Teacher { get; init; } = new();

    public SubjectDto Subject { get; init; } = new();

    public GradeDto Grade { get; init; } = new();

    public IReadOnlyList<CourseSectionDto> Sections { get; init; } = [];

    /// <summary>True when the caller holds an active enrollment.</summary>
    public bool IsEnrolled { get; init; }

    public bool IsOwner { get; init; }

    public int ProgressPercentage { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}

public sealed record CourseSectionDto
{
    public Guid Id { get; init; }

    public Guid CourseId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int SortOrder { get; init; }

    public int LessonsCount { get; init; }

    public int DurationInMinutes { get; init; }

    public IReadOnlyList<CourseLessonDto> Lessons { get; init; } = [];
}

public sealed record CourseLessonDto
{
    public Guid Id { get; init; }

    public Guid SectionId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <summary>Null unless the lesson is a free preview or the caller is enrolled.</summary>
    public string? VideoUrl { get; init; }

    public int DurationInMinutes { get; init; }

    public int SortOrder { get; init; }

    public bool IsPreview { get; init; }

    public bool IsPublished { get; init; }

    public bool IsLocked { get; init; }
}
