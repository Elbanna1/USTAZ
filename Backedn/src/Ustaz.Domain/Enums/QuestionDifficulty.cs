namespace Ustaz.Domain.Enums;

/// <summary>
/// How hard a question turned out to be, derived from the share of graded answers that were
/// correct. This is observed difficulty, not a property the author sets.
/// </summary>
public enum QuestionDifficulty
{
    /// <summary>Not enough graded answers yet to judge.</summary>
    Unknown = 0,

    /// <summary>At least 80% answered it correctly.</summary>
    Easy = 1,

    /// <summary>Between 50% and 80%.</summary>
    Moderate = 2,

    /// <summary>Between 30% and 50%.</summary>
    Hard = 3,

    /// <summary>Under 30% answered it correctly.</summary>
    VeryHard = 4
}
