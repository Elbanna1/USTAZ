using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities.Education;

/// <summary>One student's answer to one question inside an attempt.</summary>
public class ExamAttemptAnswer : BaseEntity
{
    public Guid AttemptId { get; set; }

    public ExamAttempt Attempt { get; set; } = null!;

    public Guid QuestionId { get; set; }

    public ExamQuestion Question { get; set; } = null!;

    /// <summary>Chosen option for a multiple choice question.</summary>
    public Guid? SelectedOptionId { get; set; }

    /// <summary>Free text for true/false, short answer and essay questions.</summary>
    public string? TextAnswer { get; set; }

    /// <summary>Null while an essay waits for the teacher.</summary>
    public bool? IsCorrect { get; set; }

    public decimal AwardedPoints { get; set; }

    /// <summary>Snapshot of the question's value, so later edits cannot rewrite history.</summary>
    public decimal MaximumPoints { get; set; }

    public string? TeacherFeedback { get; set; }

    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
}
