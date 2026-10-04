namespace Ustaz.Domain.Enums;

/// <summary>
/// The kinds of question an online exam supports. Everything below
/// <see cref="Essay"/> can be graded without a human.
/// </summary>
public enum ExamQuestionType
{
    /// <summary>One correct option out of several.</summary>
    MultipleChoice = 1,

    /// <summary>A two-option question stored as its own type so the UI can render it as a toggle.</summary>
    TrueFalse = 2,

    /// <summary>Free text matched against the model answer, case and whitespace insensitive.</summary>
    ShortAnswer = 3,

    /// <summary>Long-form answer; always graded by the teacher.</summary>
    Essay = 4
}
