using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities.Education;

/// <summary>An assessment set by a teacher for one of their groups.</summary>
public class Exam : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Guid TeacherId { get; set; }

    public TeacherProfile Teacher { get; set; } = null!;

    public Guid? GroupId { get; set; }

    public Group? Group { get; set; }

    public Guid SubjectId { get; set; }

    public Subject Subject { get; set; } = null!;

    public Guid? GradeId { get; set; }

    public EducationalGrade? Grade { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ExamType Type { get; set; } = ExamType.Quiz;

    public DateTime ExamDate { get; set; }

    public int DurationInMinutes { get; set; }

    public decimal MaximumScore { get; set; } = 100m;

    /// <summary>Score at or above which the student passes.</summary>
    public decimal PassingScore { get; set; } = 50m;

    public bool IsPublished { get; set; }

    public DateTime? PublishedAt { get; set; }

    /// <summary>Results stay hidden from students until the teacher releases them.</summary>
    public bool ResultsPublished { get; set; }

    public DateTime? ResultsPublishedAt { get; set; }

    /// <summary>
    /// True when students sit this exam in the app. An offline exam keeps the original
    /// behaviour: the teacher records the marks afterwards.
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>How many times a student may sit an online exam.</summary>
    public int MaxAttempts { get; set; } = 1;

    /// <summary>
    /// Lets an auto-graded quiz show its marks the moment it is submitted, without waiting
    /// for the teacher to release results for the whole class.
    /// </summary>
    public bool ShowResultsImmediately { get; set; }

    public ICollection<ExamResult> Results { get; set; } = [];

    public ICollection<ExamQuestion> Questions { get; set; } = [];

    public ICollection<ExamAttempt> Attempts { get; set; } = [];

    public void Publish(DateTime utcNow)
    {
        if (MaximumScore <= 0)
        {
            throw new BusinessRuleException("EXAM_INVALID_SCORE", "The maximum score must be greater than zero.");
        }

        if (IsOnline)
        {
            if (Questions.Count == 0)
            {
                throw new BusinessRuleException(
                    "EXAM_HAS_NO_QUESTIONS",
                    "Add at least one question before publishing an online exam.");
            }

            foreach (var question in Questions)
            {
                question.EnsureIsAnswerable();
            }
        }

        IsPublished = true;
        PublishedAt ??= utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Keeps <see cref="MaximumScore"/> equal to the points actually on the paper. Called
    /// whenever the question set changes, so a marked attempt is always out of the right total.
    /// </summary>
    public void SyncMaximumScoreFromQuestions(decimal totalPoints, DateTime utcNow)
    {
        if (!IsOnline || totalPoints <= 0m)
        {
            return;
        }

        MaximumScore = totalPoints;

        if (PassingScore > MaximumScore)
        {
            PassingScore = MaximumScore;
        }

        UpdatedAt = utcNow;
    }

    /// <summary>Whether a student may see their own marks for this exam yet.</summary>
    public bool ScoresVisibleToStudent => ResultsPublished || ShowResultsImmediately;

    public void PublishResults(DateTime utcNow)
    {
        if (!IsPublished)
        {
            throw new BusinessRuleException(
                "EXAM_NOT_PUBLISHED",
                "Publish the exam before releasing its results.");
        }

        ResultsPublished = true;
        ResultsPublishedAt ??= utcNow;
        UpdatedAt = utcNow;
    }

    public void EnsureScoreIsValid(decimal score)
    {
        if (score < 0m || score > MaximumScore)
        {
            throw new BusinessRuleException(
                "EXAM_SCORE_OUT_OF_RANGE",
                "The score must be between 0 and " + MaximumScore + ".");
        }
    }
}
