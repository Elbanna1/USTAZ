using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities;

/// <summary>
/// One student's progress through one lesson of a recorded course. This is what makes
/// <see cref="CourseEnrollment.ProgressPercentage"/> a server-derived fact rather than a
/// number the client asserts.
/// </summary>
public class CourseLessonProgress : BaseEntity
{
    public Guid CourseEnrollmentId { get; set; }

    public CourseEnrollment CourseEnrollment { get; set; } = null!;

    /// <summary>Denormalised from the enrollment so a student's progress can be read directly.</summary>
    public Guid StudentId { get; set; }

    /// <summary>Denormalised from the lesson's section so course-wide reads need no joins.</summary>
    public Guid CourseId { get; set; }

    public Guid LessonId { get; set; }

    public CourseLesson Lesson { get; set; } = null!;

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>How far into the video the student got, for "resume where you left off".</summary>
    public int WatchedSeconds { get; set; }

    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    public void MarkCompleted(DateTime utcNow, int? watchedSeconds = null)
    {
        if (watchedSeconds.HasValue && watchedSeconds.Value > WatchedSeconds)
        {
            WatchedSeconds = watchedSeconds.Value;
        }

        // Completing twice keeps the original timestamp: the first pass is the milestone.
        if (!IsCompleted)
        {
            IsCompleted = true;
            CompletedAt = utcNow;
        }

        LastAccessedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public void Reopen(DateTime utcNow)
    {
        IsCompleted = false;
        CompletedAt = null;
        LastAccessedAt = utcNow;
        UpdatedAt = utcNow;
    }
}
