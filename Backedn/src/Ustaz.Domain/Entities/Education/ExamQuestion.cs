using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities.Education;

/// <summary>One question on an online exam.</summary>
public class ExamQuestion : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid ExamId { get; set; }

    public Exam Exam { get; set; } = null!;

    public string Text { get; set; } = string.Empty;

    public ExamQuestionType Type { get; set; } = ExamQuestionType.MultipleChoice;

    public decimal Points { get; set; } = 1m;

    public int SortOrder { get; set; }

    /// <summary>
    /// Model answer for <see cref="ExamQuestionType.ShortAnswer"/> and
    /// <see cref="ExamQuestionType.TrueFalse"/>. Never leaves the server for a student.
    /// </summary>
    public string? CorrectAnswer { get; set; }

    /// <summary>Shown with the result once the student may see their marks.</summary>
    public string? Explanation { get; set; }

    public ICollection<ExamQuestionOption> Options { get; set; } = [];

    /// <summary>True when the server can mark this question without a teacher.</summary>
    public bool IsAutoGradable => Type != ExamQuestionType.Essay;

    /// <summary>
    /// Validates the question against the rules of its own type. Called before a question is
    /// stored and again before the exam is published, so a half-built question can never be
    /// sat by a student.
    /// </summary>
    public void EnsureIsAnswerable()
    {
        switch (Type)
        {
            case ExamQuestionType.MultipleChoice:
                if (Options.Count < 2)
                {
                    throw new BusinessRuleException(
                        "QUESTION_NEEDS_OPTIONS",
                        "A multiple choice question needs at least two options.");
                }

                if (Options.Count(option => option.IsCorrect) != 1)
                {
                    throw new BusinessRuleException(
                        "QUESTION_NEEDS_ONE_CORRECT_OPTION",
                        "A multiple choice question needs exactly one correct option.");
                }

                break;

            case ExamQuestionType.TrueFalse:
                if (!IsTrueFalseAnswer(CorrectAnswer))
                {
                    throw new BusinessRuleException(
                        "QUESTION_NEEDS_BOOLEAN_ANSWER",
                        "A true/false question needs its correct answer set to true or false.");
                }

                break;

            case ExamQuestionType.ShortAnswer:
                if (string.IsNullOrWhiteSpace(CorrectAnswer))
                {
                    throw new BusinessRuleException(
                        "QUESTION_NEEDS_MODEL_ANSWER",
                        "A short answer question needs a model answer to grade against.");
                }

                break;

            case ExamQuestionType.Essay:
                // Nothing to verify: an essay is graded by the teacher.
                break;
        }
    }

    /// <summary>
    /// Marks a submitted answer. Returns the points awarded, which is the question's full
    /// value or nothing — partial credit on auto-graded questions is a teacher's call.
    /// </summary>
    public decimal Grade(Guid? selectedOptionId, string? textAnswer) => Type switch
    {
        ExamQuestionType.MultipleChoice =>
            selectedOptionId.HasValue
            && Options.Any(option => option.Id == selectedOptionId.Value && option.IsCorrect)
                ? Points
                : 0m,

        ExamQuestionType.TrueFalse =>
            NormalizeBoolean(textAnswer) is { } answer && answer == NormalizeBoolean(CorrectAnswer)
                ? Points
                : 0m,

        ExamQuestionType.ShortAnswer =>
            Normalize(textAnswer).Length > 0 && Normalize(textAnswer) == Normalize(CorrectAnswer)
                ? Points
                : 0m,

        // Essays start at zero and wait for the teacher.
        _ => 0m
    };

    public static bool IsTrueFalseAnswer(string? value) => NormalizeBoolean(value) is not null;

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Accepts the spellings a client might send for a boolean answer.</summary>
    private static bool? NormalizeBoolean(string? value) => Normalize(value) switch
    {
        "true" or "1" or "yes" or "صح" or "صحيح" => true,
        "false" or "0" or "no" or "خطأ" or "خطا" => false,
        _ => null
    };
}
