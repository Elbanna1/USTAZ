using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Reviews.Dtos;

public sealed record ReviewDto
{
    public Guid Id { get; init; }

    public Guid StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string? StudentImageUrl { get; init; }

    public Guid TeacherId { get; init; }

    public string TeacherName { get; init; } = string.Empty;

    public Guid? GroupId { get; init; }

    public string? GroupName { get; init; }

    public Guid? CourseId { get; init; }

    public string? CourseTitle { get; init; }

    public ReviewTargetType TargetType { get; init; }

    public string TargetTypeName => TargetType.ToString();

    public int Rating { get; init; }

    public string? Comment { get; init; }

    public bool IsApproved { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    /// <summary>True when the caller wrote this review and may edit or delete it.</summary>
    public bool IsOwn { get; init; }
}

/// <summary>Aggregate rating summary shown next to a review list.</summary>
public sealed record ReviewSummaryDto
{
    public decimal AverageRating { get; init; }

    public int TotalReviews { get; init; }

    public int FiveStar { get; init; }

    public int FourStar { get; init; }

    public int ThreeStar { get; init; }

    public int TwoStar { get; init; }

    public int OneStar { get; init; }
}
