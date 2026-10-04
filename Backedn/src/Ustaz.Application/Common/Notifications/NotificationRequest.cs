namespace Ustaz.Application.Common.Notifications;

/// <summary>
/// A request to raise one catalog notification. Handlers describe *what happened* and supply
/// the values the copy needs; wording, category, priority and deep link come from the catalog.
/// </summary>
public sealed record NotificationRequest
{
    /// <summary>A key from <see cref="NotificationEvents"/>.</summary>
    public required string Event { get; init; }

    /// <summary>The workspace this happened in, when it happened inside one.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Values for the <c>{placeholder}</c> tokens in the copy and the deep link.</summary>
    public IReadOnlyDictionary<string, string>? Values { get; init; }

    /// <summary>The entity that triggered this, used for the deep link and for grouping.</summary>
    public Guid? ReferenceId { get; init; }

    /// <summary>
    /// Overrides the catalog's deep link when the destination cannot be expressed as a
    /// template, e.g. it depends on the recipient's role.
    /// </summary>
    public string? ActionUrl { get; init; }

    /// <summary>
    /// Identifies "this same event again" so it is only delivered once — for example
    /// <c>homework-due:{homeworkId}</c> for a deadline reminder. Scoped per recipient.
    /// </summary>
    public string? DeduplicationKey { get; init; }
}
