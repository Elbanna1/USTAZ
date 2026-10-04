using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Exams.Dtos;

// ---------------------------------------------------------------------------
// Teacher facing: the full question, model answers included.
// ---------------------------------------------------------------------------

/// <summary>One option of a multiple choice question, as its author sees it.</summary>
public sealed record ExamQuestionOptionDto
{
    public Guid Id { get; init; }

    public string Text { get; init; } = string.Empty;

    public bool IsCorrect { get; init; }

    public int SortOrder { get; init; }
}

/// <summary>A question with its answer key. Never returned to a student.</summary>
public sealed record ExamQuestionDto
{
    public Guid Id { get; init; }

    public Guid ExamId { get; init; }

    public string Text { get; init; } = string.Empty;

    public ExamQuestionType Type { get; init; }

    public string TypeName => Type.ToString();

    public decimal Points { get; init; }

    public int SortOrder { get; init; }

    public string? CorrectAnswer { get; init; }

    public string? Explanation { get; init; }

    public IReadOnlyList<ExamQuestionOptionDto> Options { get; init; } = [];
}

// ---------------------------------------------------------------------------
// Student facing: the same question with every trace of the answer removed.
// ---------------------------------------------------------------------------

/// <summary>An option as a student sees it while sitting the exam: text and nothing else.</summary>
public sealed record StudentExamOptionDto
{
    public Guid Id { get; init; }

    public string Text { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}

/// <summary>
/// A question as a student sees it. There is deliberately no correct-answer field on this
/// record: the shape itself is what guarantees the key cannot leak before submission.
/// </summary>
public sealed record StudentExamQuestionDto
{
    public Guid Id { get; init; }

    public string Text { get; init; } = string.Empty;

    public ExamQuestionType Type { get; init; }

    public string TypeName => Type.ToString();

    public decimal Points { get; init; }

    public int SortOrder { get; init; }

    public IReadOnlyList<StudentExamOptionDto> Options { get; init; } = [];

    /// <summary>The answer this student has already recorded in the current attempt, if any.</summary>
    public Guid? SelectedOptionId { get; init; }

    public string? TextAnswer { get; init; }
}

// ---------------------------------------------------------------------------
// Attempts
// ---------------------------------------------------------------------------

/// <summary>One marked answer, visible once the student may see their marks.</summary>
public sealed record ExamAttemptAnswerDto
{
    public Guid Id { get; init; }

    public Guid QuestionId { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public ExamQuestionType QuestionType { get; init; }

    public Guid? SelectedOptionId { get; init; }

    public string? TextAnswer { get; init; }

    public bool? IsCorrect { get; init; }

    public decimal AwardedPoints { get; init; }

    public decimal MaximumPoints { get; init; }

    public string? TeacherFeedback { get; init; }

    /// <summary>Only populated once the marks are visible to the student.</summary>
    public string? CorrectAnswer { get; init; }

    public string? Explanation { get; init; }
}

/// <summary>A sitting of an online exam.</summary>
public sealed record ExamAttemptDto
{
    public Guid Id { get; init; }

    public Guid ExamId { get; init; }

    public string ExamTitle { get; init; } = string.Empty;

    public Guid StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public int AttemptNumber { get; init; }

    public ExamAttemptStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public DateTime StartedAt { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public DateTime? SubmittedAt { get; init; }

    public bool RequiresManualGrading { get; init; }

    /// <summary>
    /// False while the marks are withheld. The score fields below are zeroed in that case,
    /// so a student cannot infer their grade from an unreleased attempt.
    /// </summary>
    public bool ScoresVisible { get; init; }

    public decimal Score { get; init; }

    public decimal MaximumScore { get; init; }

    public decimal Percentage { get; init; }

    public bool IsPassed { get; init; }

    public string? TeacherComment { get; init; }

    public DateTime? GradedAt { get; init; }

    public IReadOnlyList<ExamAttemptAnswerDto> Answers { get; init; } = [];
}

/// <summary>What a student receives when they start or resume a sitting.</summary>
public sealed record ExamAttemptSessionDto
{
    public Guid AttemptId { get; init; }

    public Guid ExamId { get; init; }

    public string ExamTitle { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int AttemptNumber { get; init; }

    public int MaxAttempts { get; init; }

    public int DurationInMinutes { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public decimal MaximumScore { get; init; }

    public IReadOnlyList<StudentExamQuestionDto> Questions { get; init; } = [];
}
