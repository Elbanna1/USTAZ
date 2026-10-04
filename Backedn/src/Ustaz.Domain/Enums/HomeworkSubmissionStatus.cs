namespace Ustaz.Domain.Enums;

/// <summary>
/// Where one student's homework stands. The numeric values are part of the API contract and
/// are never reordered.
/// </summary>
public enum HomeworkSubmissionStatus
{
    /// <summary>Nothing handed in. Also the state of a submission row that only holds a draft file.</summary>
    NotSubmitted = 1,

    /// <summary>Handed in before the deadline, waiting to be marked.</summary>
    Submitted = 2,

    /// <summary>Handed in after the deadline, waiting to be marked.</summary>
    Late = 3,

    /// <summary>Marked. Final unless the teacher returns it.</summary>
    Graded = 4,

    /// <summary>
    /// Sent back for revision with a reason. The ball is with the student, who has one more
    /// attempt whatever the assignment's resubmission policy says.
    /// </summary>
    Returned = 5
}
