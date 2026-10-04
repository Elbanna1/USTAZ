namespace Ustaz.Domain.Enums;

public enum ExamAttemptStatus
{
    /// <summary>The student has started and can still answer.</summary>
    InProgress = 1,

    /// <summary>Answers are in; essay questions are still awaiting the teacher.</summary>
    Submitted = 2,

    /// <summary>Every question carries a final mark.</summary>
    Graded = 3,

    /// <summary>The window closed before the student submitted.</summary>
    Expired = 4
}
