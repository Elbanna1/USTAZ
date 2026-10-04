using Ustaz.Domain.Common;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities;

/// <summary>
/// A student rating for a teacher, optionally scoped to one of that teacher's groups or courses.
/// A review always carries a teacher so profile aggregates stay simple.
/// </summary>
public class Review : BaseEntity
{
    public Guid StudentId { get; set; }

    public StudentProfile Student { get; set; } = null!;

    public Guid TeacherId { get; set; }

    public TeacherProfile Teacher { get; set; } = null!;

    public Guid? GroupId { get; set; }

    public Group? Group { get; set; }

    public Guid? CourseId { get; set; }

    public Course? Course { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public bool IsApproved { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? ModerationNote { get; set; }

    public ReviewTargetType TargetType =>
        CourseId.HasValue ? ReviewTargetType.Course
        : GroupId.HasValue ? ReviewTargetType.Group
        : ReviewTargetType.Teacher;

    public void EnsureRatingIsValid()
    {
        if (Rating is < DomainRules.MinRating or > DomainRules.MaxRating)
        {
            throw new BusinessRuleException(
                "REVIEW_INVALID_RATING",
                $"Rating must be between {DomainRules.MinRating} and {DomainRules.MaxRating}.");
        }
    }

    public void Approve(DateTime utcNow, string? note = null)
    {
        IsApproved = true;
        ApprovedAt = utcNow;
        ModerationNote = note;
        UpdatedAt = utcNow;
    }

    public void Reject(DateTime utcNow, string note)
    {
        IsApproved = false;
        ApprovedAt = null;
        ModerationNote = note;
        UpdatedAt = utcNow;
    }
}
