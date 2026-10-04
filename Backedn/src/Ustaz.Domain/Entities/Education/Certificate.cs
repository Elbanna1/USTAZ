using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities.Education;

/// <summary>
/// Proof that a student finished a recorded course.
///
/// Everything a verifier needs is snapshotted onto the row at issue time. A certificate has
/// to keep saying the same thing years later, after the course was renamed, the teacher left
/// the workspace, or the enrollment was archived — so verification never joins live data.
/// </summary>
public class Certificate : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid StudentId { get; set; }

    public StudentProfile Student { get; set; } = null!;

    public Guid CourseId { get; set; }

    public Course Course { get; set; } = null!;

    public Guid CourseEnrollmentId { get; set; }

    public Guid TeacherId { get; set; }

    /// <summary>Human-readable reference, e.g. <c>USTAZ-2026-7Q4M2K</c>.</summary>
    public string CertificateNumber { get; set; } = string.Empty;

    /// <summary>
    /// The secret half of verification. It is long and random, so a certificate cannot be
    /// found by guessing: the public endpoint takes this, never the sequential number alone.
    /// </summary>
    public string VerificationCode { get; set; } = string.Empty;

    public CertificateStatus Status { get; set; } = CertificateStatus.Issued;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public DateTime CompletedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? RevocationReason { get; set; }

    // --- Snapshots, frozen at issue time --------------------------------------
    public string StudentName { get; set; } = string.Empty;

    public string CourseTitle { get; set; } = string.Empty;

    public string TeacherName { get; set; } = string.Empty;

    public string WorkspaceName { get; set; } = string.Empty;

    public int LessonsCompleted { get; set; }

    public bool IsValid => Status == CertificateStatus.Issued;

    public void Revoke(DateTime utcNow, string? reason)
    {
        if (Status == CertificateStatus.Revoked)
        {
            throw new BusinessRuleException(
                "CERTIFICATE_ALREADY_REVOKED",
                "This certificate has already been revoked.");
        }

        Status = CertificateStatus.Revoked;
        RevokedAt = utcNow;
        RevocationReason = reason;
        UpdatedAt = utcNow;
    }
}
