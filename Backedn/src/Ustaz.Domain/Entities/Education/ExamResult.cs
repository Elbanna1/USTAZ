using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities.Education;

/// <summary>One student's outcome for one exam.</summary>
public class ExamResult : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid ExamId { get; set; }

    public Exam Exam { get; set; } = null!;

    public Guid StudentId { get; set; }

    public StudentProfile Student { get; set; } = null!;

    public decimal Score { get; set; }

    /// <summary>Snapshot of the exam's maximum at grading time, so history survives edits.</summary>
    public decimal MaximumScore { get; set; }

    /// <summary>Stored rather than computed so it can be filtered and sorted in SQL.</summary>
    public decimal Percentage { get; set; }

    public bool IsPassed { get; set; }

    public bool IsAbsent { get; set; }

    public string? TeacherComment { get; set; }

    public Guid RecordedByUserId { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public bool ParentNotified { get; set; }

    public DateTime? ParentNotifiedAt { get; set; }

    /// <summary>Recomputes percentage and pass state from the score.</summary>
    public void ApplyScore(decimal score, decimal maximumScore, decimal passingScore, DateTime utcNow)
    {
        Score = score;
        MaximumScore = maximumScore;
        Percentage = maximumScore <= 0m
            ? 0m
            : Math.Round(score / maximumScore * 100m, 2, MidpointRounding.AwayFromZero);
        IsPassed = !IsAbsent && score >= passingScore;
        UpdatedAt = utcNow;
    }
}
