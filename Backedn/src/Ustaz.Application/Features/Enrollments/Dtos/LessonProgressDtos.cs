using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Enrollments.Dtos;

/// <summary>Where a student stands in one course, recomputed from completed lessons.</summary>
public sealed record LessonProgressSummaryDto
{
    public Guid CourseId { get; init; }

    public Guid EnrollmentId { get; init; }

    public int TotalLessons { get; init; }

    public int CompletedLessons { get; init; }

    public int ProgressPercentage { get; init; }

    public EnrollmentStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public bool IsCompleted => Status == EnrollmentStatus.Completed;

    public DateTime? CompletedAt { get; init; }
}

/// <summary>One lesson inside a student's progress report.</summary>
public sealed record LessonProgressItemDto
{
    public Guid LessonId { get; init; }

    public Guid SectionId { get; init; }

    public string SectionTitle { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public int DurationInMinutes { get; init; }

    public bool IsPreview { get; init; }

    public bool IsCompleted { get; init; }

    public int WatchedSeconds { get; init; }

    public DateTime? CompletedAt { get; init; }

    public DateTime? LastAccessedAt { get; init; }
}

/// <summary>A student's full progress report for one course.</summary>
public sealed record CourseProgressReportDto
{
    public Guid CourseId { get; init; }

    public Guid EnrollmentId { get; init; }

    public string CourseTitle { get; init; } = string.Empty;

    public int TotalLessons { get; init; }

    public int CompletedLessons { get; init; }

    public int ProgressPercentage { get; init; }

    public EnrollmentStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public DateTime? CompletedAt { get; init; }

    public DateTime? LastAccessedAt { get; init; }

    /// <summary>The first unfinished lesson in syllabus order; null once the course is done.</summary>
    public Guid? CurrentLessonId { get; init; }

    public string? CurrentLessonTitle { get; init; }

    /// <summary>The lesson after the current one; null at the end of the syllabus.</summary>
    public Guid? NextLessonId { get; init; }

    public string? NextLessonTitle { get; init; }

    /// <summary>Total watch time recorded across the course, in seconds.</summary>
    public long WatchedSeconds { get; init; }

    public IReadOnlyList<LessonProgressItemDto> Lessons { get; init; } = [];
}
