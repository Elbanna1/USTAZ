using Ustaz.Domain.Enums;

namespace Ustaz.Application.Common.Interfaces;

/// <summary>One thing to record on the timeline.</summary>
public sealed record ActivityRequest
{
    /// <summary>A key from <c>ActivityTypes</c>. An unknown key is refused.</summary>
    public required string Type { get; init; }

    public required Guid TenantId { get; init; }

    /// <summary>Who did it. Null for something the system did.</summary>
    public Guid? ActorUserId { get; init; }

    /// <summary>
    /// The actor's name. Left unset, the recorder looks it up from
    /// <see cref="ActorUserId"/> and falls back to a neutral label.
    /// </summary>
    public string? ActorName { get; init; }

    /// <summary>The student the entry is about, when there is one.</summary>
    public Guid? StudentId { get; init; }

    public string? RelatedEntityType { get; init; }

    public Guid? RelatedEntityId { get; init; }

    /// <summary>
    /// Display values only — a title, a count, a score. Never credentials, tokens, contact
    /// details or document bodies: these rows are read by everyone who can see the feed.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    public ActivityVisibility Visibility { get; init; } = ActivityVisibility.Shared;

    /// <summary>
    /// Supply this for anything that could legitimately be attempted twice — republishing,
    /// a retried webhook, a sweep catching up — and the second attempt is dropped.
    /// </summary>
    public string? DeduplicationKey { get; init; }

    /// <summary>When it happened. Defaults to now.</summary>
    public DateTime? OccurredAt { get; init; }
}

/// <summary>
/// The single place activity is written. Business handlers call this after their own work has
/// succeeded, so the timeline never claims something that did not happen.
///
/// Recording is best-effort by design: a feed entry is not worth failing a payment or a
/// submission over, so a failure here is logged and swallowed rather than propagated.
/// </summary>
public interface IActivityRecorder
{
    /// <summary>
    /// Records one entry. Returns false when it was dropped as a duplicate or could not be
    /// written. Never throws.
    /// </summary>
    Task<bool> RecordAsync(ActivityRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records several entries in one save. Returns how many were written; duplicates are
    /// skipped individually.
    /// </summary>
    Task<int> RecordManyAsync(
        IEnumerable<ActivityRequest> requests,
        CancellationToken cancellationToken = default);
}
