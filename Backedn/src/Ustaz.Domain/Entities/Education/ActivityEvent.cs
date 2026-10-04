using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities.Education;

/// <summary>
/// One thing that happened, recorded once for the timeline.
///
/// Written at the moment a business action succeeds, never reconstructed afterwards: a feed
/// assembled by querying live tables can only ever show what those tables still say, and would
/// silently rewrite history when a row is edited or deleted. This is a durable, append-only
/// record of events, which is what makes a paginated feed stable between requests.
///
/// It deliberately holds no sensitive payload. <see cref="Metadata"/> carries display values
/// only — a title, a count, a score — never credentials, tokens, contact details or full
/// document bodies.
/// </summary>
public class ActivityEvent : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    /// <summary>
    /// Stable key from <c>ActivityTypes</c>, e.g. <c>homework.submitted</c>. The client switches
    /// on this and renders its own copy, so wording is never stored here.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Who did it. Null for something the system did, such as an expiry sweep.</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>
    /// The actor's display name as it stood when this happened. Snapshotted so the feed reads
    /// correctly after a rename and still renders if the account is later removed.
    /// </summary>
    public string ActorName { get; set; } = string.Empty;

    /// <summary>
    /// The student this entry is about, when there is one. Drives the student's own feed and
    /// their guardians' view; null for a workspace-level event.
    /// </summary>
    public Guid? StudentId { get; set; }

    public StudentProfile? Student { get; set; }

    /// <summary>The entity this entry is about, e.g. <c>Homework</c>. Lets the client deep-link.</summary>
    public string? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }

    /// <summary>Small JSON object of display values. Never sensitive.</summary>
    public string? Metadata { get; set; }

    public ActivityVisibility Visibility { get; set; } = ActivityVisibility.Shared;

    /// <summary>
    /// When it happened, which is not always when the row was written: a sweep that catches up
    /// on yesterday records yesterday. Ordering and paging use this.
    /// </summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Makes recording idempotent. A unique index over tenant and key means re-publishing the
    /// same homework or replaying a webhook cannot produce a second entry, even if two requests
    /// race. Null for events that legitimately repeat.
    /// </summary>
    public string? DeduplicationKey { get; set; }
}
