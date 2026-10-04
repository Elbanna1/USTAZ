using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Common.Notifications;

/// <summary>
/// Resolves "who should hear about this" once, so every workflow that fans a notification out
/// to a class uses the same definition of the audience — and the same single projected query
/// rather than loading enrollments and walking navigations.
/// </summary>
public static class NotificationAudience
{
    /// <summary>User ids of the students actively enrolled in a group.</summary>
    public static Task<List<Guid>> GroupStudentUserIdsAsync(
        IApplicationDbContext context,
        Guid groupId,
        CancellationToken cancellationToken) =>
        context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.GroupId == groupId
                && enrollment.Status == EnrollmentStatus.Active)
            .Select(enrollment => enrollment.Student.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

    /// <summary>User ids of the students actively enrolled in a recorded course.</summary>
    public static Task<List<Guid>> CourseStudentUserIdsAsync(
        IApplicationDbContext context,
        Guid courseId,
        CancellationToken cancellationToken) =>
        context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.CourseId == courseId
                && enrollment.Status != EnrollmentStatus.Cancelled)
            .Select(enrollment => enrollment.Student.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

    /// <summary>User ids of the confirmed guardians of one student.</summary>
    public static Task<List<Guid>> GuardianUserIdsAsync(
        IApplicationDbContext context,
        Guid studentId,
        CancellationToken cancellationToken) =>
        context.ParentStudentLinks
            .AsNoTracking()
            .Where(link => link.StudentId == studentId && link.IsConfirmed)
            .Select(link => link.Parent.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

    /// <summary>The user id behind a student profile, or <see cref="Guid.Empty"/>.</summary>
    public static async Task<Guid> StudentUserIdAsync(
        IApplicationDbContext context,
        Guid studentId,
        CancellationToken cancellationToken) =>
        await context.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == studentId)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>The user id behind a teacher profile, or <see cref="Guid.Empty"/>.</summary>
    public static async Task<Guid> TeacherUserIdAsync(
        IApplicationDbContext context,
        Guid teacherId,
        CancellationToken cancellationToken) =>
        await context.TeacherProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == teacherId)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Display name of a student, with a neutral fallback for the copy.</summary>
    public static async Task<string> StudentNameAsync(
        IApplicationDbContext context,
        Guid studentId,
        CancellationToken cancellationToken) =>
        await context.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == studentId)
            .Select(profile => profile.User.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "طالب";
}
