namespace Ustaz.Domain.Enums;

/// <summary>
/// How loudly a notification should be presented. Priority also decides what may be silenced:
/// <see cref="Critical"/> notifications ignore per-category preferences because they carry
/// security or money consequences the user must see.
/// </summary>
public enum NotificationPriority
{
    /// <summary>Nice to know; safe to batch or collapse in the UI.</summary>
    Low = 1,

    Normal = 2,

    /// <summary>Deserves a badge and a push: a deadline, a result, a failed payment.</summary>
    High = 3,

    /// <summary>Always delivered, whatever the user's preferences say.</summary>
    Critical = 4
}
