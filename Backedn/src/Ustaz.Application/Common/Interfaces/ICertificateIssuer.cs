namespace Ustaz.Application.Common.Interfaces;

/// <summary>
/// Issues completion certificates. Kept behind an interface so the course-progress path can
/// trigger issuance without depending on the certificate feature, and so issuance can be
/// stubbed in tests that only care about progress.
/// </summary>
public interface ICertificateIssuer
{
    /// <summary>
    /// Issues the certificate for a completed enrollment, or returns the existing one.
    /// Returns null when the enrollment is not actually complete.
    /// </summary>
    Task<Guid?> IssueForCompletedCourseAsync(
        Guid courseEnrollmentId,
        CancellationToken cancellationToken = default);
}
