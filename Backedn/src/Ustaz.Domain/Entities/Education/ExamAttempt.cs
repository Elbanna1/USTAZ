using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities.Education;

/// <summary>One sitting of an online exam by one student.</summary>
public class ExamAttempt : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid ExamId { get; set; }

    public Exam Exam { get; set; } = null!;

    public Guid StudentId { get; set; }

    public StudentProfile Student { get; set; } = null!;

    /// <summary>1 for the first sitting, incremented for each retake.</summary>
    public int AttemptNumber { get; set; } = 1;

    public ExamAttemptStatus Status { get; set; } = ExamAttemptStatus.InProgress;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this sitting stops accepting answers. Derived from the exam's duration at start
    /// time, so shortening the exam later cannot retroactively void a sitting in progress.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public decimal Score { get; set; }

    /// <summary>Snapshot of the total points on offer when the attempt started.</summary>
    public decimal MaximumScore { get; set; }

    public decimal Percentage { get; set; }

    public bool IsPassed { get; set; }

    /// <summary>True while at least one essay answer is still waiting for a teacher.</summary>
    public bool RequiresManualGrading { get; set; }

    public DateTime? GradedAt { get; set; }

    public Guid? GradedByUserId { get; set; }

    public string? TeacherComment { get; set; }

    /// <summary>
    /// Concurrency token. The background expiry sweep and a student pressing submit can
    /// reach the same attempt at the same moment; whichever writes second is rejected by
    /// the database rather than silently overwriting the other outcome.
    /// </summary>
    public byte[]? RowVersion { get; set; }

    public ICollection<ExamAttemptAnswer> Answers { get; set; } = [];

    public bool IsOpen => Status == ExamAttemptStatus.InProgress;

    public bool HasExpired(DateTime utcNow) => ExpiresAt is not null && utcNow > ExpiresAt.Value;

    /// <summary>Closes the sitting. Scoring happens through <see cref="ApplyScore"/>.</summary>
    public void Submit(DateTime utcNow, bool requiresManualGrading)
    {
        if (Status is ExamAttemptStatus.Submitted or ExamAttemptStatus.Graded)
        {
            throw new BusinessRuleException(
                "ATTEMPT_ALREADY_SUBMITTED",
                "This attempt has already been submitted.");
        }

        Status = requiresManualGrading ? ExamAttemptStatus.Submitted : ExamAttemptStatus.Graded;
        RequiresManualGrading = requiresManualGrading;
        SubmittedAt = utcNow;
        UpdatedAt = utcNow;

        if (!requiresManualGrading)
        {
            GradedAt = utcNow;
        }
    }

    /// <summary>Recomputes the totals from the awarded points.</summary>
    public void ApplyScore(decimal score, decimal maximumScore, decimal passingScore, DateTime utcNow)
    {
        Score = score;
        MaximumScore = maximumScore;
        Percentage = maximumScore <= 0m
            ? 0m
            : Math.Round(score / maximumScore * 100m, 2, MidpointRounding.AwayFromZero);
        IsPassed = score >= passingScore;
        UpdatedAt = utcNow;
    }

    /// <summary>Marks a teacher-graded attempt as final.</summary>
    public void CompleteManualGrading(Guid gradedByUserId, DateTime utcNow, string? comment)
    {
        if (Status == ExamAttemptStatus.InProgress)
        {
            throw new BusinessRuleException(
                "ATTEMPT_NOT_SUBMITTED",
                "This attempt has not been submitted yet.");
        }

        Status = ExamAttemptStatus.Graded;
        RequiresManualGrading = false;
        GradedByUserId = gradedByUserId;
        GradedAt = utcNow;
        TeacherComment = comment;
        UpdatedAt = utcNow;
    }
}
