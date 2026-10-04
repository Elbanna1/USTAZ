using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Exams.Dtos;

/// <summary>
/// Everything a teacher needs to understand how one exam went. Composed of sections the UI
/// renders as tabs; each section is computed from the marks actually recorded, so an exam
/// nobody has sat reports zeros and empty lists rather than invented figures.
/// </summary>
public sealed record ExamAnalyticsDto
{
    public ExamAnalyticsOverviewDto Overview { get; init; } = new();

    public ExamPerformanceSummaryDto Performance { get; init; } = new();

    /// <summary>Marks bucketed into ten-point bands. Always all ten bands.</summary>
    public IReadOnlyList<ScoreBucketDto> ScoreDistribution { get; init; } = [];

    /// <summary>
    /// Per-question breakdown. Empty for an offline exam, which has no question-level data:
    /// only the total mark is recorded.
    /// </summary>
    public IReadOnlyList<QuestionAnalyticsDto> Questions { get; init; } = [];

    /// <summary>The questions students got wrong most often, hardest first.</summary>
    public IReadOnlyList<QuestionAnalyticsDto> MostDifficultQuestions { get; init; } = [];

    /// <summary>The questions students got right most often, easiest first.</summary>
    public IReadOnlyList<QuestionAnalyticsDto> MostSuccessfulQuestions { get; init; } = [];

    /// <summary>
    /// Cohort performance per question type, which is the only grouping the question paper
    /// actually records. Empty for an offline exam.
    /// </summary>
    public IReadOnlyList<ExamTopicPerformanceDto> TopicPerformance { get; init; } = [];

    public IReadOnlyList<ExamStudentStandingDto> TopPerformers { get; init; } = [];

    public IReadOnlyList<ExamStudentStandingDto> LowestPerformers { get; init; } = [];

    /// <summary>
    /// Students who were expected to sit this exam and have no mark. Drives the chase-up list;
    /// empty when the exam is not tied to a group.
    /// </summary>
    public IReadOnlyList<ExamStudentStandingDto> NotAttempted { get; init; } = [];

    /// <summary>Plain observations derived from the numbers above. Never speculative.</summary>
    public IReadOnlyList<ExamInsightDto> Insights { get; init; } = [];
}

public sealed record ExamAnalyticsOverviewDto
{
    public Guid ExamId { get; init; }

    public string Title { get; init; } = string.Empty;

    public ExamType Type { get; init; }

    public string TypeName => Type.ToString();

    public bool IsOnline { get; init; }

    public DateTime ExamDate { get; init; }

    public int DurationInMinutes { get; init; }

    public decimal MaximumScore { get; init; }

    public decimal PassingScore { get; init; }

    public bool IsPublished { get; init; }

    public bool ResultsPublished { get; init; }

    public int QuestionsCount { get; init; }

    public Guid? GroupId { get; init; }

    public string? GroupName { get; init; }

    public Guid SubjectId { get; init; }

    public string SubjectNameAr { get; init; } = string.Empty;

    public string SubjectNameEn { get; init; } = string.Empty;

    /// <summary>Active enrollments in the exam group; null when the exam has no group.</summary>
    public int? ExpectedParticipants { get; init; }
}

public sealed record ExamPerformanceSummaryDto
{
    /// <summary>Sittings, or recorded marks for an offline exam. Absentees included.</summary>
    public int TotalAttempts { get; init; }

    /// <summary>Attempts that produced a mark: absentees and unfinished sittings excluded.</summary>
    public int ScoredAttempts { get; init; }

    public int AbsentCount { get; init; }

    /// <summary>Online sittings still open. Their marks are not final yet.</summary>
    public int InProgressCount { get; init; }

    /// <summary>Sittings that ran out of time before the student submitted.</summary>
    public int ExpiredCount { get; init; }

    /// <summary>Sittings with at least one essay still waiting for the teacher.</summary>
    public int AwaitingGradingCount { get; init; }

    public decimal AverageScore { get; init; }

    public decimal AveragePercentage { get; init; }

    public decimal HighestPercentage { get; init; }

    public decimal LowestPercentage { get; init; }

    /// <summary>Middle mark; less sensitive to one outlier than the mean.</summary>
    public decimal MedianPercentage { get; init; }

    /// <summary>Population standard deviation of the percentages: how spread the class is.</summary>
    public decimal StandardDeviation { get; init; }

    public int PassedCount { get; init; }

    public int FailedCount { get; init; }

    public decimal PassRate { get; init; }

    public decimal FailRate { get; init; }

    /// <summary>
    /// Mean wall-clock minutes from starting a sitting to submitting it. Online exams only,
    /// and null when nothing has been submitted.
    /// </summary>
    public decimal? AverageDurationMinutes { get; init; }
}

public sealed record ScoreBucketDto
{
    /// <summary>Inclusive lower bound of the band, as a percentage.</summary>
    public int FromPercentage { get; init; }

    /// <summary>Upper bound of the band. The top band includes 100.</summary>
    public int ToPercentage { get; init; }

    public string Label { get; init; } = string.Empty;

    public int Count { get; init; }

    /// <summary>Share of scored attempts that fell in this band.</summary>
    public decimal Percentage { get; init; }
}

public sealed record QuestionAnalyticsDto
{
    public Guid QuestionId { get; init; }

    public string Text { get; init; } = string.Empty;

    public ExamQuestionType Type { get; init; }

    public string TypeName => Type.ToString();

    public decimal Points { get; init; }

    public int SortOrder { get; init; }

    /// <summary>Students who supplied an answer to this question.</summary>
    public int AnsweredCount { get; init; }

    /// <summary>Students who sat the exam and left this one blank.</summary>
    public int SkippedCount { get; init; }

    public int CorrectCount { get; init; }

    public int IncorrectCount { get; init; }

    /// <summary>Essay answers still waiting for the teacher; they count as neither yet.</summary>
    public int AwaitingGradingCount { get; init; }

    /// <summary>Share of graded answers that were correct.</summary>
    public decimal CorrectPercentage { get; init; }

    public decimal IncorrectPercentage { get; init; }

    /// <summary>Mean points awarded per graded answer.</summary>
    public decimal AverageAwardedPoints { get; init; }

    public QuestionDifficulty Difficulty { get; init; }

    public string DifficultyName => Difficulty.ToString();
}

/// <summary>Cohort performance for one slice of the question paper.</summary>
public sealed record ExamTopicPerformanceDto
{
    public string Topic { get; init; } = string.Empty;

    public int QuestionsCount { get; init; }

    public int GradedAnswers { get; init; }

    public int CorrectAnswers { get; init; }

    public decimal CorrectPercentage { get; init; }
}

public sealed record ExamStudentStandingDto
{
    public Guid StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public decimal? Score { get; init; }

    public decimal? Percentage { get; init; }

    public bool? IsPassed { get; init; }

    public bool IsAbsent { get; init; }

    /// <summary>Set for an online exam; null for a recorded offline mark.</summary>
    public ExamAttemptStatus? AttemptStatus { get; init; }

    public string? AttemptStatusName => AttemptStatus == null ? null : AttemptStatus.Value.ToString();

    public DateTime? SubmittedAt { get; init; }

    /// <summary>Minutes spent on the sitting; online exams only.</summary>
    public decimal? DurationMinutes { get; init; }
}

/// <summary>
/// One observation about the exam. <see cref="Code"/> is stable so the client can localise or
/// switch on it; <see cref="Message"/> is a ready-to-show Arabic sentence.
/// </summary>
public sealed record ExamInsightDto
{
    public string Code { get; init; } = string.Empty;

    public ExamInsightSeverity Severity { get; init; }

    public string SeverityName => Severity.ToString();

    public string Message { get; init; } = string.Empty;

    /// <summary>The number the observation is about, when there is one.</summary>
    public decimal? Value { get; init; }
}

public enum ExamInsightSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3
}
