using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    /// <summary>
    /// Certificates for the enrollments that genuinely finished.
    ///
    /// A certificate is only ever earned, never granted: this module reads the enrollments
    /// whose status the lesson-progress module promoted to
    /// <see cref="EnrollmentStatus.Completed"/> and issues one each. Nothing is issued for an
    /// enrollment at 99%, which is what keeps the eligibility rule honest in demo data too.
    ///
    /// Everything a verifier sees is snapshotted onto the row, exactly as the production
    /// issuer does it, so verification keeps working after a rename. The verification codes
    /// are derived from the enrollment id rather than randomly generated — a demo code has to
    /// be the same on every machine for a frontend fixture to use it — and they are
    /// consequently not secret. That is a property of the demo dataset, not of the product:
    /// a real certificate still gets 128 bits from the cryptographic generator.
    /// </summary>
    private async Task SeedCompletionAsync(CancellationToken cancellationToken)
    {
        var enrollmentIds = _courseEnrollments.Values.Select(enrollment => enrollment.Id).ToList();

        var completed = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollmentIds.Contains(enrollment.Id)
                && enrollment.Status == EnrollmentStatus.Completed)
            .Select(enrollment => new
            {
                enrollment.Id,
                enrollment.StudentId,
                enrollment.CourseId,
                enrollment.CompletedAt,
                StudentName = enrollment.Student.User.DisplayName,
                CourseTitle = enrollment.Course.Title,
                enrollment.Course.TenantId,
                enrollment.Course.TeacherId,
                TeacherName = enrollment.Course.Teacher.User.DisplayName,
                WorkspaceName = enrollment.Course.Teacher.Tenant.Name
            })
            .ToListAsync(cancellationToken);

        if (completed.Count == 0)
        {
            report.RecordFailure("No completed course enrollments, so no certificates were issued.");
            return;
        }

        var lessonCounts = await context.CourseLessonProgress
            .AsNoTracking()
            .Where(progress => enrollmentIds.Contains(progress.CourseEnrollmentId) && progress.IsCompleted)
            .GroupBy(progress => progress.CourseEnrollmentId)
            .Select(grouping => new { EnrollmentId = grouping.Key, Count = grouping.Count() })
            .ToListAsync(cancellationToken);

        var completedLessons = lessonCounts.ToDictionary(entry => entry.EnrollmentId, entry => entry.Count);

        var certificates = new List<Certificate>();
        var sequence = 0;

        foreach (var enrollment in completed.OrderBy(entry => entry.CompletedAt))
        {
            sequence++;
            var issuedAt = enrollment.CompletedAt ?? _utcNow.AddDays(-5);

            var certificate = new Certificate
            {
                Id = SeedId.For("demo:certificate", enrollment.Id),
                TenantId = enrollment.TenantId,
                StudentId = enrollment.StudentId,
                CourseId = enrollment.CourseId,
                CourseEnrollmentId = enrollment.Id,
                TeacherId = enrollment.TeacherId,
                CertificateNumber = "USTAZ-" + issuedAt.Year + "-DEMO" + sequence.ToString("D4"),
                VerificationCode = DemoVerificationCode(enrollment.Id),
                Status = CertificateStatus.Issued,
                IssuedAt = issuedAt,
                CompletedAt = issuedAt,
                StudentName = enrollment.StudentName,
                CourseTitle = enrollment.CourseTitle,
                TeacherName = enrollment.TeacherName,
                WorkspaceName = enrollment.WorkspaceName,
                LessonsCompleted = completedLessons.TryGetValue(enrollment.Id, out var count) ? count : 0,
                CreatedAt = issuedAt
            };

            certificates.Add(certificate);
        }

        await AddMissingAsync("Certificates", certificates, cancellationToken);
    }

    /// <summary>
    /// A stable 32-character hex code for a demo certificate. Deterministic on purpose, and
    /// therefore guessable — acceptable only because every certificate it identifies is
    /// fictional and this mode never runs in production.
    /// </summary>
    private static string DemoVerificationCode(Guid enrollmentId) =>
        Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("ustaz-demo-certificate:" + enrollmentId.ToString("N"))))[..32];
}
