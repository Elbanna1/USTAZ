using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Teachers.Dtos;

/// <summary>
/// Everything a teacher needs to understand where their verification stands and what to do
/// next. The checklist is returned even once verified, so the UI never has to guess which
/// requirement failed.
/// </summary>
public sealed record TeacherVerificationDto
{
    public VerificationStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public bool IsVerified { get; init; }

    /// <summary>True when the teacher may submit right now (complete, and not already in review).</summary>
    public bool CanSubmit { get; init; }

    /// <summary>True while the request sits with the review team.</summary>
    public bool IsUnderReview { get; init; }

    /// <summary>Whether every requirement below is met.</summary>
    public bool IsProfileComplete { get; init; }

    public DateTime? SubmittedAt { get; init; }

    public DateTime? DecidedAt { get; init; }

    /// <summary>The reviewer's reason. Populated on rejection; cleared when resubmitting.</summary>
    public string? Notes { get; init; }

    public IReadOnlyList<VerificationRequirementDto> Requirements { get; init; } = [];
}

/// <summary>One thing the teacher must supply before the profile can be reviewed.</summary>
public sealed record VerificationRequirementDto
{
    /// <summary>Stable key the client maps to its own copy and to the field to focus.</summary>
    public required string Key { get; init; }

    public bool IsMet { get; init; }
}
