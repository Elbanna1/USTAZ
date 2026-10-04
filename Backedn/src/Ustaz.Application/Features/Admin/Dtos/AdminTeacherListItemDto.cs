using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Admin.Dtos;

/// <summary>
/// A teacher row as a moderator sees it. Deliberately separate from the public
/// <c>TeacherListItemDto</c>: the review notes are private feedback for the teacher and
/// must never be projected onto the public directory.
/// </summary>
public sealed record AdminTeacherListItemDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? Headline { get; init; }

    public string? City { get; init; }

    public int YearsOfExperience { get; init; }

    public int StudentsCount { get; init; }

    public decimal Rating { get; init; }

    public int ReviewCount { get; init; }

    public bool IsFeatured { get; init; }

    public bool IsActive { get; init; }

    public bool IsVerified { get; init; }

    public VerificationStatus VerificationStatus { get; init; }

    public string VerificationStatusName => VerificationStatus.ToString();

    /// <summary>When the teacher asked to be reviewed — lets the queue show who waited longest.</summary>
    public DateTime? VerificationSubmittedAt { get; init; }

    public DateTime? VerifiedAt { get; init; }

    /// <summary>The last decision note. Admin-only.</summary>
    public string? VerificationNotes { get; init; }

    public int GroupsCount { get; init; }

    public int CoursesCount { get; init; }

    public DateTime CreatedAt { get; init; }
}
