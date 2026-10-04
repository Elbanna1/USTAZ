using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities.Education;

/// <summary>
/// One student's answer to a homework assignment, plus its grading.
///
/// States run NotSubmitted, then Submitted or Late, then Graded. From Graded a teacher may
/// return the work for revision, which moves it to Returned and is the only route back to
/// accepting another submission from that student.
/// </summary>
public class HomeworkSubmission : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid HomeworkId { get; set; }

    public Homework Homework { get; set; } = null!;

    public Guid StudentId { get; set; }

    public StudentProfile Student { get; set; } = null!;

    public HomeworkSubmissionStatus Status { get; set; } = HomeworkSubmissionStatus.NotSubmitted;

    public string? Content { get; set; }

    public string? FileUrl { get; set; }

    public string? FilePublicId { get; set; }

    public DateTime? SubmittedAt { get; set; }

    /// <summary>How many times this student has handed work in. Zero until the first submission.</summary>
    public int SubmissionCount { get; set; }

    public bool IsLate { get; set; }

    public decimal? Score { get; set; }

    public decimal? MaximumScore { get; set; }

    public decimal? Percentage { get; set; }

    public string? TeacherFeedback { get; set; }

    public Guid? GradedByUserId { get; set; }

    public DateTime? GradedAt { get; set; }

    /// <summary>Set when a teacher sends the work back for revision.</summary>
    public DateTime? ReturnedAt { get; set; }

    /// <summary>What the student has to fix. Required when returning work.</summary>
    public string? ReturnReason { get; set; }

    public bool ParentNotified { get; set; }

    /// <summary>True once anything has been handed in, whatever happened to it afterwards.</summary>
    public bool HasSubmission => SubmissionCount > 0 || SubmittedAt is not null;

    /// <summary>
    /// Records a submission, enforcing the assignment's resubmission policy. Lateness is decided
    /// here from the server clock and the assignment's own deadline, never from the client.
    /// </summary>
    public void Submit(
        Homework homework,
        DateTime utcNow,
        string? content,
        string? fileUrl,
        string? filePublicId)
    {
        if (Status == HomeworkSubmissionStatus.Graded)
        {
            throw new BusinessRuleException(
                "HOMEWORK_ALREADY_GRADED",
                "This submission has already been graded and can no longer be changed.");
        }

        // Returned work always earns one more attempt: that is the whole point of returning it,
        // so the policy below applies only to voluntary resubmissions.
        var isRevision = Status == HomeworkSubmissionStatus.Returned;

        if (!isRevision && HasSubmission)
        {
            if (!homework.AllowResubmission)
            {
                throw new BusinessRuleException(
                    "HOMEWORK_RESUBMISSION_NOT_ALLOWED",
                    "You have already handed this in and it cannot be changed.");
            }

            if (homework.MaxSubmissions is { } limit && SubmissionCount >= limit)
            {
                throw new BusinessRuleException(
                    "HOMEWORK_SUBMISSION_LIMIT_REACHED",
                    "You have used all " + limit + " submission attempts for this homework.");
            }
        }

        if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(fileUrl))
        {
            throw new BusinessRuleException(
                "HOMEWORK_EMPTY_SUBMISSION",
                "Write an answer or attach a file before submitting.");
        }

        Content = content;
        FileUrl = fileUrl;
        FilePublicId = filePublicId;
        SubmittedAt = utcNow;
        SubmissionCount++;
        IsLate = utcNow > homework.DueAt;
        Status = IsLate ? HomeworkSubmissionStatus.Late : HomeworkSubmissionStatus.Submitted;

        // A revision starts a fresh grading cycle, so the previous verdict is cleared rather
        // than left to describe work that has since changed.
        ReturnedAt = null;
        ReturnReason = null;
        Score = null;
        Percentage = null;
        GradedAt = null;
        GradedByUserId = null;

        UpdatedAt = utcNow;
    }

    public void Grade(decimal score, decimal maximumScore, string? feedback, Guid gradedByUserId, DateTime utcNow)
    {
        if (Status == HomeworkSubmissionStatus.NotSubmitted)
        {
            throw new BusinessRuleException(
                "HOMEWORK_NOT_SUBMITTED",
                "This student has not submitted anything to grade.");
        }

        if (score < 0m || score > maximumScore)
        {
            throw new BusinessRuleException(
                "HOMEWORK_SCORE_OUT_OF_RANGE",
                "The score must be between 0 and " + maximumScore + ".");
        }

        Score = score;
        MaximumScore = maximumScore;
        Percentage = maximumScore <= 0m
            ? 0m
            : Math.Round(score / maximumScore * 100m, 2, MidpointRounding.AwayFromZero);
        TeacherFeedback = feedback;
        GradedByUserId = gradedByUserId;
        GradedAt = utcNow;
        Status = HomeworkSubmissionStatus.Graded;
        ReturnedAt = null;
        ReturnReason = null;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Hands the work back with a reason, granting the student one more attempt. The mark is
    /// kept until the student resubmits, so the teacher can still see what they awarded.
    /// </summary>
    public void ReturnForRevision(string reason, Guid returnedByUserId, DateTime utcNow)
    {
        if (!HasSubmission)
        {
            throw new BusinessRuleException(
                "HOMEWORK_NOT_SUBMITTED",
                "There is nothing to return: this student has not submitted anything.");
        }

        if (Status == HomeworkSubmissionStatus.Returned)
        {
            throw new BusinessRuleException(
                "HOMEWORK_ALREADY_RETURNED",
                "This submission has already been returned for revision.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException(
                "HOMEWORK_RETURN_NEEDS_REASON",
                "Tell the student what to fix before returning their work.");
        }

        Status = HomeworkSubmissionStatus.Returned;
        ReturnedAt = utcNow;
        ReturnReason = reason;
        GradedByUserId = returnedByUserId;
        UpdatedAt = utcNow;
    }
}
