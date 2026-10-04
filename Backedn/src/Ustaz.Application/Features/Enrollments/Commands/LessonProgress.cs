using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Constants;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Features.Enrollments.Dtos;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Enrollments.Commands;

/// <summary>
/// Shared plumbing for lesson-level progress. Course completion is derived here, on the
/// server, from the lessons the student actually finished — the client never asserts it.
/// </summary>
internal static class LessonProgressWriter
{
    /// <summary>
    /// Resolves the caller's live enrollment in a course. Cancelled enrollments lose access,
    /// which is what stops a refunded student from continuing to record progress.
    /// </summary>
    public static async Task<CourseEnrollment> LoadActiveEnrollmentAsync(
        IApplicationDbContext context,
        Guid studentId,
        Guid courseId,
        CancellationToken cancellationToken)
    {
        var enrollment = await context.CourseEnrollments
            .FirstOrDefaultAsync(
                entity => entity.CourseId == courseId && entity.StudentId == studentId,
                cancellationToken)
            ?? throw new NotFoundException("CourseEnrollment", courseId);

        if (enrollment.Status == EnrollmentStatus.Cancelled)
        {
            throw new ForbiddenAccessException("This enrollment has been cancelled.");
        }

        return enrollment;
    }

    /// <summary>
    /// Verifies the lesson really belongs to the course and is published, so a lesson id
    /// lifted from another course cannot be completed through this enrollment.
    /// </summary>
    public static async Task EnsureLessonBelongsToCourseAsync(
        IApplicationDbContext context,
        Guid courseId,
        Guid lessonId,
        CancellationToken cancellationToken)
    {
        var lesson = await context.CourseLessons
            .AsNoTracking()
            .Where(entity => entity.Id == lessonId)
            .Select(entity => new { entity.Section.CourseId, entity.IsPublished })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("CourseLesson", lessonId);

        if (lesson.CourseId != courseId)
        {
            throw new NotFoundException("CourseLesson", lessonId);
        }

        if (!lesson.IsPublished)
        {
            throw new BusinessRuleException(
                "LESSON_NOT_PUBLISHED",
                "This lesson is not published yet.");
        }
    }

    /// <summary>
    /// Recomputes the enrollment's percentage from completed published lessons and returns
    /// the resulting summary. A course with no published lessons stays at zero.
    /// </summary>
    public static async Task<LessonProgressSummaryDto> RecalculateAsync(
        IApplicationDbContext context,
        CourseEnrollment enrollment,
        DateTime utcNow,
        CancellationToken cancellationToken,
        INotificationDispatcher? notifications = null,
        ICertificateIssuer? certificateIssuer = null)
    {
        var wasCompleted = enrollment.Status == EnrollmentStatus.Completed;
        // The completion the caller just recorded has to be in the store before it can be
        // counted; the totals below are computed by the database, not from tracked state.
        await context.SaveChangesAsync(cancellationToken);

        var totalLessons = await context.CourseLessons
            .CountAsync(
                lesson => lesson.Section.CourseId == enrollment.CourseId && lesson.IsPublished,
                cancellationToken);

        var completedLessons = await context.CourseLessonProgress
            .CountAsync(
                progress => progress.CourseEnrollmentId == enrollment.Id
                    && progress.IsCompleted
                    && progress.Lesson.IsPublished,
                cancellationToken);

        var percentage = totalLessons == 0
            ? 0
            : (int)Math.Round(completedLessons * 100d / totalLessons, MidpointRounding.AwayFromZero);

        enrollment.UpdateProgress(percentage, utcNow);

        // UpdateProgress only ever promotes to Completed. Reopening a lesson has to be able to
        // walk that back, otherwise a course stays "finished" while lessons are outstanding.
        if (percentage < 100 && enrollment.Status == EnrollmentStatus.Completed)
        {
            enrollment.Status = EnrollmentStatus.Active;
            enrollment.CompletedAt = null;
        }

        await context.SaveChangesAsync(cancellationToken);

        // Crossing the finish line is worth one notification, the first time only.
        if (!wasCompleted && enrollment.Status == EnrollmentStatus.Completed)
        {
            if (notifications is not null)
            {
                await AnnounceCompletionAsync(context, notifications, enrollment, cancellationToken);
            }

            // Finishing the course is what earns the certificate. Issuing is idempotent, so
            // a replayed completion cannot mint a second one.
            if (certificateIssuer is not null)
            {
                await certificateIssuer.IssueForCompletedCourseAsync(enrollment.Id, cancellationToken);
            }
        }

        return new LessonProgressSummaryDto
        {
            CourseId = enrollment.CourseId,
            EnrollmentId = enrollment.Id,
            TotalLessons = totalLessons,
            CompletedLessons = completedLessons,
            ProgressPercentage = enrollment.ProgressPercentage,
            Status = enrollment.Status,
            CompletedAt = enrollment.CompletedAt
        };
    }

    /// <summary>Tells the learner they finished, and the teacher that someone did.</summary>
    private static async Task AnnounceCompletionAsync(
        IApplicationDbContext context,
        INotificationDispatcher notifications,
        CourseEnrollment enrollment,
        CancellationToken cancellationToken)
    {
        var course = await context.Courses
            .AsNoTracking()
            .Where(entity => entity.Id == enrollment.CourseId)
            .Select(entity => new { entity.Title, entity.TenantId, entity.TeacherId })
            .FirstOrDefaultAsync(cancellationToken);

        if (course is null)
        {
            return;
        }

        var values = new Dictionary<string, string>
        {
            ["course"] = course.Title,
            ["courseId"] = enrollment.CourseId.ToString()
        };

        var studentUserId = await NotificationAudience.StudentUserIdAsync(
            context, enrollment.StudentId, cancellationToken);

        if (studentUserId != Guid.Empty)
        {
            await notifications.SendAsync(
                studentUserId,
                new NotificationRequest
                {
                    Event = NotificationEvents.CourseCompleted,
                    TenantId = course.TenantId,
                    ReferenceId = enrollment.Id,
                    DeduplicationKey = "course-completed:" + enrollment.Id,
                    Values = values
                },
                cancellationToken);
        }

        var teacherUserId = await NotificationAudience.TeacherUserIdAsync(
            context, course.TeacherId, cancellationToken);

        if (teacherUserId != Guid.Empty)
        {
            await notifications.SendAsync(
                teacherUserId,
                new NotificationRequest
                {
                    Event = NotificationEvents.CourseCompletedByStudent,
                    TenantId = course.TenantId,
                    ReferenceId = enrollment.Id,
                    DeduplicationKey = "course-completed-teacher:" + enrollment.Id,
                    Values = new Dictionary<string, string>(values)
                    {
                        ["student"] = await NotificationAudience.StudentNameAsync(
                            context, enrollment.StudentId, cancellationToken)
                    }
                },
                cancellationToken);
        }
    }
}

/// <summary>Marks one lesson of a course the caller is enrolled in as completed.</summary>
public sealed record CompleteLessonCommand : IRequest<LessonProgressSummaryDto>
{
    public Guid CourseId { get; init; }

    public Guid LessonId { get; init; }

    /// <summary>Optional furthest playback position, used to resume the video.</summary>
    public int? WatchedSeconds { get; init; }
}

public sealed class CompleteLessonCommandValidator : AbstractValidator<CompleteLessonCommand>
{
    public CompleteLessonCommandValidator()
    {
        RuleFor(command => command.CourseId).NotEmpty();
        RuleFor(command => command.LessonId).NotEmpty();

        RuleFor(command => command.WatchedSeconds!.Value)
            .InclusiveBetween(0, 24 * 60 * 60)
            .When(command => command.WatchedSeconds.HasValue);
    }
}

public sealed class CompleteLessonCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    IActivityRecorder activity,
    ICertificateIssuer certificateIssuer) : IRequestHandler<CompleteLessonCommand, LessonProgressSummaryDto>
{
    public async Task<LessonProgressSummaryDto> Handle(
        CompleteLessonCommand command,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();
        var utcNow = dateTimeProvider.UtcNow;

        var enrollment = await LessonProgressWriter.LoadActiveEnrollmentAsync(
            context, studentId, command.CourseId, cancellationToken);

        await LessonProgressWriter.EnsureLessonBelongsToCourseAsync(
            context, command.CourseId, command.LessonId, cancellationToken);

        var progress = await context.CourseLessonProgress
            .FirstOrDefaultAsync(
                entity => entity.CourseEnrollmentId == enrollment.Id && entity.LessonId == command.LessonId,
                cancellationToken);

        // Completing an already-completed lesson is a no-op rather than a conflict: clients
        // replay this call as a video ends, and a retry must not fail.
        if (progress is null)
        {
            progress = new CourseLessonProgress
            {
                Id = Guid.NewGuid(),
                CourseEnrollmentId = enrollment.Id,
                StudentId = studentId,
                CourseId = command.CourseId,
                LessonId = command.LessonId,
                CreatedAt = utcNow
            };

            await context.CourseLessonProgress.AddAsync(progress, cancellationToken);
        }

        var wasAlreadyComplete = progress.IsCompleted;

        progress.MarkCompleted(utcNow, command.WatchedSeconds);

        var summary = await LessonProgressWriter.RecalculateAsync(
            context, enrollment, utcNow, cancellationToken, notifications, certificateIssuer);

        // Clients replay this call as a video ends, so only the first completion is timeline
        // worthy. The deduplication key makes that safe even across replays.
        if (!wasAlreadyComplete)
        {
            var tenantId = await context.Courses
                .AsNoTracking()
                .Where(course => course.Id == command.CourseId)
                .Select(course => course.TenantId)
                .FirstOrDefaultAsync(cancellationToken);

            await activity.RecordAsync(
                new ActivityRequest
                {
                    Type = ActivityTypes.LessonCompleted,
                    TenantId = tenantId,
                    ActorUserId = currentUser.UserId,
                    StudentId = studentId,
                    RelatedEntityType = nameof(CourseLesson),
                    RelatedEntityId = command.LessonId,
                    // A learner's own progress; the staff feed would drown in these.
                    Visibility = ActivityVisibility.Subject,
                    DeduplicationKey = ActivityTypes.LessonCompleted + ":" + progress.Id,
                    Metadata = new Dictionary<string, string>
                    {
                        ["courseId"] = command.CourseId.ToString(),
                        ["progress"] = summary.ProgressPercentage.ToString()
                    }
                },
                cancellationToken);

            if (summary.IsCompleted)
            {
                await activity.RecordAsync(
                    new ActivityRequest
                    {
                        Type = ActivityTypes.CourseCompleted,
                        TenantId = tenantId,
                        ActorUserId = currentUser.UserId,
                        StudentId = studentId,
                        RelatedEntityType = nameof(CourseEnrollment),
                        RelatedEntityId = enrollment.Id,
                        Visibility = ActivityVisibility.Shared,
                        DeduplicationKey = ActivityTypes.CourseCompleted + ":" + enrollment.Id,
                        Metadata = new Dictionary<string, string>
                        {
                            ["courseId"] = command.CourseId.ToString()
                        }
                    },
                    cancellationToken);
            }
        }

        return summary;
    }
}

/// <summary>Reopens a lesson the caller had marked as completed.</summary>
public sealed record ReopenLessonCommand(Guid CourseId, Guid LessonId) : IRequest<LessonProgressSummaryDto>;

public sealed class ReopenLessonCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<ReopenLessonCommand, LessonProgressSummaryDto>
{
    public async Task<LessonProgressSummaryDto> Handle(
        ReopenLessonCommand command,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();
        var utcNow = dateTimeProvider.UtcNow;

        var enrollment = await LessonProgressWriter.LoadActiveEnrollmentAsync(
            context, studentId, command.CourseId, cancellationToken);

        var progress = await context.CourseLessonProgress
            .FirstOrDefaultAsync(
                entity => entity.CourseEnrollmentId == enrollment.Id && entity.LessonId == command.LessonId,
                cancellationToken)
            ?? throw new NotFoundException("CourseLessonProgress", command.LessonId);

        progress.Reopen(utcNow);

        return await LessonProgressWriter.RecalculateAsync(context, enrollment, utcNow, cancellationToken);
    }
}
