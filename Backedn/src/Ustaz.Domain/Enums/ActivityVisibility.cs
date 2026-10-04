namespace Ustaz.Domain.Enums;

/// <summary>
/// Who an activity entry is for. This is the only thing that decides which feed a row appears
/// in, so it is set when the entry is written and never inferred at read time.
/// </summary>
public enum ActivityVisibility
{
    /// <summary>
    /// Workspace staff only. For anything commercial or administrative a student has no business
    /// seeing in a feed, such as a payment landing or a review arriving.
    /// </summary>
    Workspace = 1,

    /// <summary>
    /// The student the entry is about, and their confirmed guardians. Not shown to staff, for
    /// entries that are only meaningful to the learner.
    /// </summary>
    Subject = 2,

    /// <summary>Both the workspace and the student, e.g. handing homework in.</summary>
    Shared = 3
}
