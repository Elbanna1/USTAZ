using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities.Education;

/// <summary>One choice on a multiple choice question.</summary>
public class ExamQuestionOption : BaseEntity
{
    public Guid QuestionId { get; set; }

    public ExamQuestion Question { get; set; } = null!;

    public string Text { get; set; } = string.Empty;

    /// <summary>Never projected into a student-facing DTO before the attempt is graded.</summary>
    public bool IsCorrect { get; set; }

    public int SortOrder { get; set; }
}
