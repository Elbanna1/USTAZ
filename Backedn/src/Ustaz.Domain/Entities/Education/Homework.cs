using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities.Education;

/// <summary>
/// An assignment given to a group, with a deadline and optional attachments.
///
/// The lifecycle is Draft (unpublished) to Open (published, still accepting work) to Closed
/// (accepting nothing further, whatever the deadline says). Closing is the teacher's explicit
/// act and is the only state that overrides <see cref="AllowLateSubmission"/>.
/// </summary>
public class Homework : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Guid TeacherId { get; set; }

    public TeacherProfile Teacher { get; set; } = null!;

    public Guid GroupId { get; set; }

    public Group Group { get; set; } = null!;

    public Guid? SubjectId { get; set; }

    public Subject? Subject { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Instructions { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public DateTime DueAt { get; set; }

    public decimal MaximumScore { get; set; } = 10m;

    /// <summary>When false, a submission after the deadline is refused outright.</summary>
    public bool AllowLateSubmission { get; set; } = true;

    /// <summary>
    /// When false, the first submission is final: a student cannot overwrite it even while the
    /// assignment is still open. Returning work for revision always grants one more attempt,
    /// regardless of this setting.
    /// </summary>
    public bool AllowResubmission { get; set; } = true;

    /// <summary>
    /// Hard cap on how many times one student may hand in. Null means no cap. Ignored when
    /// <see cref="AllowResubmission"/> is false, which already caps it at one.
    /// </summary>
    public int? MaxSubmissions { get; set; }

    public bool IsPublished { get; set; }

    public DateTime? PublishedAt { get; set; }

    /// <summary>Set when the teacher closes the assignment. Null while it is still open.</summary>
    public DateTime? ClosedAt { get; set; }

    public ICollection<HomeworkAttachment> Attachments { get; set; } = [];

    public ICollection<HomeworkSubmission> Submissions { get; set; } = [];

    public bool IsClosed => ClosedAt is not null;

    public bool IsOverdue(DateTime utcNow) => utcNow > DueAt;

    /// <summary>
    /// True while a student could still hand something in. Drives the status the client shows
    /// and mirrors exactly what <see cref="EnsureAcceptsSubmission"/> permits.
    /// </summary>
    public bool IsOpenForSubmissions(DateTime utcNow) =>
        IsPublished && !IsClosed && (!IsOverdue(utcNow) || AllowLateSubmission);

    public void Publish(DateTime utcNow)
    {
        if (DueAt <= AssignedAt)
        {
            throw new BusinessRuleException(
                "HOMEWORK_INVALID_DEADLINE",
                "The deadline must be after the assignment date.");
        }

        if (IsClosed)
        {
            throw new BusinessRuleException(
                "HOMEWORK_CLOSED",
                "This homework is closed. Reopen it before publishing again.");
        }

        IsPublished = true;
        PublishedAt ??= utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Stops accepting work. Marks already awarded are untouched, and the teacher can still
    /// grade what came in before the close.
    /// </summary>
    public void Close(DateTime utcNow)
    {
        if (!IsPublished)
        {
            throw new BusinessRuleException(
                "HOMEWORK_NOT_PUBLISHED",
                "An unpublished assignment has nothing to close.");
        }

        if (IsClosed)
        {
            throw new BusinessRuleException("HOMEWORK_ALREADY_CLOSED", "This homework is already closed.");
        }

        ClosedAt = utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>Reopens a closed assignment so late work can still be accepted.</summary>
    public void Reopen(DateTime utcNow)
    {
        if (!IsClosed)
        {
            throw new BusinessRuleException("HOMEWORK_NOT_CLOSED", "This homework is not closed.");
        }

        ClosedAt = null;
        UpdatedAt = utcNow;
    }

    public void EnsureAcceptsSubmission(DateTime utcNow)
    {
        if (!IsPublished)
        {
            throw new BusinessRuleException("HOMEWORK_NOT_PUBLISHED", "This homework is not open for submissions.");
        }

        // A closed assignment refuses everything, including work the deadline would still allow.
        if (IsClosed)
        {
            throw new BusinessRuleException(
                "HOMEWORK_CLOSED",
                "This homework has been closed and is no longer accepting submissions.");
        }

        if (IsOverdue(utcNow) && !AllowLateSubmission)
        {
            throw new BusinessRuleException(
                "HOMEWORK_DEADLINE_PASSED",
                "The deadline has passed and late submissions are not accepted.");
        }
    }
}
