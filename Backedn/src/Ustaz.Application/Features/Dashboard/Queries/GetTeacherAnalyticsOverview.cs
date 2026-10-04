using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Dashboard.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Dashboard.Queries;

/// <summary>
/// The workspace KPI strip for a chosen date window, with the preceding window of equal length
/// alongside each count so the UI can show movement.
///
/// Scoped to the caller's workspace throughout: every aggregate below filters on
/// <c>TenantId</c>, so one teacher can never read another's figures even by guessing ids.
/// </summary>
public sealed record GetTeacherAnalyticsOverviewQuery
    : IRequest<TeacherAnalyticsOverviewDto>, IHasAnalyticsRange
{
    public AnalyticsPeriod Period { get; init; } = AnalyticsPeriod.Last30Days;

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}

public sealed class GetTeacherAnalyticsOverviewQueryValidator
    : AbstractValidator<GetTeacherAnalyticsOverviewQuery>
{
    public GetTeacherAnalyticsOverviewQueryValidator() => this.ApplyAnalyticsRangeRules();
}

public sealed class GetTeacherAnalyticsOverviewQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetTeacherAnalyticsOverviewQuery, TeacherAnalyticsOverviewDto>
{
    public async Task<TeacherAnalyticsOverviewDto> Handle(
        GetTeacherAnalyticsOverviewQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AnalyticsView, cancellationToken: cancellationToken);

        var tenantId = access.TenantId;

        var range = AnalyticsRange.Resolve(
            query.Period, query.From, query.To, dateTimeProvider.UtcNow);

        var previous = range.PreviousPeriod();

        return new TeacherAnalyticsOverviewDto
        {
            Window = AnalyticsWindowDto.From(range),
            Students = await LoadStudentsAsync(tenantId, range, previous, cancellationToken),
            Catalogue = await LoadCatalogueAsync(tenantId, cancellationToken),
            Teaching = await LoadTeachingAsync(tenantId, range, previous, cancellationToken),
            Performance = await LoadPerformanceAsync(tenantId, range, cancellationToken),
            Revenue = await LoadRevenueAsync(tenantId, range, previous, cancellationToken)
        };
    }

    // -----------------------------------------------------------------------
    // Students
    // -----------------------------------------------------------------------

    private async Task<TeacherStudentMetricsDto> LoadStudentsAsync(
        Guid tenantId,
        AnalyticsRange range,
        AnalyticsRange? previous,
        CancellationToken cancellationToken)
    {
        var total = await context.TenantStudents
            .AsNoTracking()
            .CountAsync(link => link.TenantId == tenantId, cancellationToken);

        return new TeacherStudentMetricsDto
        {
            Total = total,
            Active = TrendDto.Of(
                await CountActiveStudentsAsync(tenantId, range, cancellationToken),
                previous is null
                    ? null
                    : await CountActiveStudentsAsync(tenantId, previous, cancellationToken)),
            New = TrendDto.Of(
                await CountNewStudentsAsync(tenantId, range, cancellationToken),
                previous is null
                    ? null
                    : await CountNewStudentsAsync(tenantId, previous, cancellationToken)),
            Churned = TrendDto.Of(
                await CountChurnedAsync(tenantId, range, cancellationToken),
                previous is null
                    ? null
                    : await CountChurnedAsync(tenantId, previous, cancellationToken))
        };
    }

    /// <summary>
    /// A student counts as active on any recorded learning action inside the window. The four
    /// sources are unioned in the database and counted once, so a student who did several
    /// things is still one student.
    /// </summary>
    private Task<int> CountActiveStudentsAsync(
        Guid tenantId,
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var fromLessons = context.CourseLessonProgress
            .AsNoTracking()
            .Where(progress => progress.CourseEnrollment.Course.TenantId == tenantId
                && progress.LastAccessedAt >= range.FromUtc
                && progress.LastAccessedAt < range.ToUtc)
            .Select(progress => progress.StudentId);

        var fromHomework = context.HomeworkSubmissions
            .AsNoTracking()
            .Where(submission => submission.TenantId == tenantId
                && submission.SubmittedAt != null
                && submission.SubmittedAt >= range.FromUtc
                && submission.SubmittedAt < range.ToUtc)
            .Select(submission => submission.StudentId);

        var fromExams = context.ExamAttempts
            .AsNoTracking()
            .Where(attempt => attempt.TenantId == tenantId
                && attempt.StartedAt >= range.FromUtc
                && attempt.StartedAt < range.ToUtc)
            .Select(attempt => attempt.StudentId);

        var fromAttendance = context.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.TenantId == tenantId
                && record.Session.SessionDate >= range.FromUtc
                && record.Session.SessionDate < range.ToUtc
                && record.Status != AttendanceStatus.Absent)
            .Select(record => record.StudentId);

        return fromLessons
            .Union(fromHomework)
            .Union(fromExams)
            .Union(fromAttendance)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// Students who joined the workspace in the window. <c>TenantStudent.JoinedAt</c> is the
    /// single place a student's arrival is recorded, so this needs no union over enrollments.
    /// </summary>
    private Task<int> CountNewStudentsAsync(
        Guid tenantId,
        AnalyticsRange range,
        CancellationToken cancellationToken) =>
        context.TenantStudents
            .AsNoTracking()
            .CountAsync(
                link => link.TenantId == tenantId
                    && link.JoinedAt >= range.FromUtc
                    && link.JoinedAt < range.ToUtc,
                cancellationToken);

    private async Task<int> CountChurnedAsync(
        Guid tenantId,
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var groups = await context.GroupEnrollments
            .AsNoTracking()
            .CountAsync(
                enrollment => enrollment.Group.TenantId == tenantId
                    && enrollment.CancelledAt != null
                    && enrollment.CancelledAt >= range.FromUtc
                    && enrollment.CancelledAt < range.ToUtc,
                cancellationToken);

        var courses = await context.CourseEnrollments
            .AsNoTracking()
            .CountAsync(
                enrollment => enrollment.Course.TenantId == tenantId
                    && enrollment.CancelledAt != null
                    && enrollment.CancelledAt >= range.FromUtc
                    && enrollment.CancelledAt < range.ToUtc,
                cancellationToken);

        return groups + courses;
    }

    // -----------------------------------------------------------------------
    // Catalogue
    // -----------------------------------------------------------------------

    /// <summary>
    /// What the workspace has built. Not windowed: a course published last year is still part
    /// of the catalogue today, so filtering these by date would understate it.
    /// </summary>
    private async Task<TeacherCatalogueMetricsDto> LoadCatalogueAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var groups = await context.Groups
            .AsNoTracking()
            .Where(group => group.TenantId == tenantId)
            .GroupBy(group => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Published = group.Count(entity => entity.IsPublished)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var courses = await context.Courses
            .AsNoTracking()
            .Where(course => course.TenantId == tenantId)
            .GroupBy(course => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Published = group.Count(entity => entity.IsPublished)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var publishedLessons = await context.CourseLessons
            .AsNoTracking()
            .CountAsync(
                lesson => lesson.IsPublished && lesson.Section.Course.TenantId == tenantId,
                cancellationToken);

        var groupSubjects = context.Groups
            .AsNoTracking()
            .Where(group => group.TenantId == tenantId)
            .Select(group => group.SubjectId);

        var courseSubjects = context.Courses
            .AsNoTracking()
            .Where(course => course.TenantId == tenantId)
            .Select(course => course.SubjectId);

        return new TeacherCatalogueMetricsDto
        {
            Groups = groups?.Total ?? 0,
            PublishedGroups = groups?.Published ?? 0,
            Courses = courses?.Total ?? 0,
            PublishedCourses = courses?.Published ?? 0,
            PublishedLessons = publishedLessons,
            Subjects = await groupSubjects.Union(courseSubjects).CountAsync(cancellationToken)
        };
    }

    // -----------------------------------------------------------------------
    // Teaching activity
    // -----------------------------------------------------------------------

    private async Task<TeacherTeachingMetricsDto> LoadTeachingAsync(
        Guid tenantId,
        AnalyticsRange range,
        AnalyticsRange? previous,
        CancellationToken cancellationToken) =>
        new()
        {
            ExamsPublished = TrendDto.Of(
                await CountExamsAsync(tenantId, range, cancellationToken),
                previous is null ? null : await CountExamsAsync(tenantId, previous, cancellationToken)),
            HomeworkPublished = TrendDto.Of(
                await CountHomeworkAsync(tenantId, range, cancellationToken),
                previous is null ? null : await CountHomeworkAsync(tenantId, previous, cancellationToken)),
            AttendanceSessions = TrendDto.Of(
                await CountSessionsAsync(tenantId, range, cancellationToken),
                previous is null ? null : await CountSessionsAsync(tenantId, previous, cancellationToken)),
            LessonsCompleted = TrendDto.Of(
                await CountLessonsCompletedAsync(tenantId, range, cancellationToken),
                previous is null
                    ? null
                    : await CountLessonsCompletedAsync(tenantId, previous, cancellationToken)),
            HomeworkAwaitingGrading = await context.HomeworkSubmissions
                .AsNoTracking()
                .CountAsync(
                    submission => submission.TenantId == tenantId
                        && (submission.Status == HomeworkSubmissionStatus.Submitted
                            || submission.Status == HomeworkSubmissionStatus.Late),
                    cancellationToken),
            ExamAttemptsAwaitingGrading = await context.ExamAttempts
                .AsNoTracking()
                .CountAsync(
                    attempt => attempt.TenantId == tenantId && attempt.RequiresManualGrading,
                    cancellationToken)
        };

    private Task<int> CountExamsAsync(
        Guid tenantId,
        AnalyticsRange range,
        CancellationToken cancellationToken) =>
        context.Exams
            .AsNoTracking()
            .CountAsync(
                exam => exam.TenantId == tenantId
                    && exam.PublishedAt != null
                    && exam.PublishedAt >= range.FromUtc
                    && exam.PublishedAt < range.ToUtc,
                cancellationToken);

    private Task<int> CountHomeworkAsync(
        Guid tenantId,
        AnalyticsRange range,
        CancellationToken cancellationToken) =>
        context.Homeworks
            .AsNoTracking()
            .CountAsync(
                homework => homework.TenantId == tenantId
                    && homework.PublishedAt != null
                    && homework.PublishedAt >= range.FromUtc
                    && homework.PublishedAt < range.ToUtc,
                cancellationToken);

    private Task<int> CountSessionsAsync(
        Guid tenantId,
        AnalyticsRange range,
        CancellationToken cancellationToken) =>
        context.AttendanceSessions
            .AsNoTracking()
            .CountAsync(
                session => session.TenantId == tenantId
                    && session.SessionDate >= range.FromUtc
                    && session.SessionDate < range.ToUtc,
                cancellationToken);

    private Task<int> CountLessonsCompletedAsync(
        Guid tenantId,
        AnalyticsRange range,
        CancellationToken cancellationToken) =>
        context.CourseLessonProgress
            .AsNoTracking()
            .CountAsync(
                progress => progress.CourseEnrollment.Course.TenantId == tenantId
                    && progress.IsCompleted
                    && progress.CompletedAt != null
                    && progress.CompletedAt >= range.FromUtc
                    && progress.CompletedAt < range.ToUtc,
                cancellationToken);

    // -----------------------------------------------------------------------
    // Academic performance
    // -----------------------------------------------------------------------

    private async Task<TeacherPerformanceMetricsDto> LoadPerformanceAsync(
        Guid tenantId,
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        // Offline marks and online sittings are separate tables, so both are summed and the
        // averages are weighted by how many marks each contributed.
        var offline = await context.ExamResults
            .AsNoTracking()
            .Where(result => result.TenantId == tenantId
                && !result.IsAbsent
                && result.Exam.ExamDate >= range.FromUtc
                && result.Exam.ExamDate < range.ToUtc)
            .GroupBy(result => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Sum = group.Sum(result => result.Percentage),
                Passed = group.Count(result => result.IsPassed)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var online = await context.ExamAttempts
            .AsNoTracking()
            .Where(attempt => attempt.TenantId == tenantId
                && attempt.Status == ExamAttemptStatus.Graded
                && attempt.SubmittedAt != null
                && attempt.SubmittedAt >= range.FromUtc
                && attempt.SubmittedAt < range.ToUtc)
            .GroupBy(attempt => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Sum = group.Sum(attempt => attempt.Percentage),
                Passed = group.Count(attempt => attempt.IsPassed)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var markCount = (offline?.Count ?? 0) + (online?.Count ?? 0);
        var markSum = (offline?.Sum ?? 0m) + (online?.Sum ?? 0m);
        var passedCount = (offline?.Passed ?? 0) + (online?.Passed ?? 0);

        var homework = await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(submission => submission.TenantId == tenantId
                && submission.Status == HomeworkSubmissionStatus.Graded
                && submission.GradedAt != null
                && submission.GradedAt >= range.FromUtc
                && submission.GradedAt < range.ToUtc)
            .GroupBy(submission => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Sum = group.Sum(submission => submission.Percentage ?? 0m)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var attendance = await context.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.TenantId == tenantId
                && record.Session.SessionDate >= range.FromUtc
                && record.Session.SessionDate < range.ToUtc)
            .GroupBy(record => 1)
            .Select(group => new
            {
                Count = group.Count(),
                // Late is still attendance; only an unexcused absence counts against the rate.
                Attended = group.Count(record => record.Status != AttendanceStatus.Absent)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var enrollments = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Course.TenantId == tenantId
                && enrollment.Status != EnrollmentStatus.Cancelled)
            .GroupBy(enrollment => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Completed = group.Count(entity => entity.Status == EnrollmentStatus.Completed),
                ProgressSum = group.Sum(entity => entity.ProgressPercentage)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new TeacherPerformanceMetricsDto
        {
            ExamAveragePercentage = markCount == 0 ? null : Round(markSum / markCount),
            ExamPassRate = markCount == 0 ? null : Percentage(passedCount, markCount),
            HomeworkAveragePercentage = homework is null or { Count: 0 }
                ? null
                : Round(homework.Sum / homework.Count),
            HomeworkSubmissionRate = await HomeworkSubmissionRateAsync(
                tenantId, range, cancellationToken),
            AttendanceRate = attendance is null or { Count: 0 }
                ? null
                : Percentage(attendance.Attended, attendance.Count),
            CourseCompletionRate = enrollments is null or { Count: 0 }
                ? null
                : Percentage(enrollments.Completed, enrollments.Count),
            AverageCourseProgress = enrollments is null or { Count: 0 }
                ? null
                : Round((decimal)enrollments.ProgressSum / enrollments.Count)
        };
    }

    /// <summary>
    /// Of the students who were asked to do homework published in the window, what share handed
    /// something in. The denominator is expected submissions — homework published in the window
    /// times the active roll of its group — not the rows that happen to exist, because a student
    /// who never submits leaves no row behind.
    /// </summary>
    private async Task<decimal?> HomeworkSubmissionRateAsync(
        Guid tenantId,
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var expected = await context.Homeworks
            .AsNoTracking()
            .Where(homework => homework.TenantId == tenantId
                && homework.PublishedAt != null
                && homework.PublishedAt >= range.FromUtc
                && homework.PublishedAt < range.ToUtc)
            .SumAsync(
                homework => context.GroupEnrollments.Count(enrollment =>
                    enrollment.GroupId == homework.GroupId
                    && enrollment.Status == EnrollmentStatus.Active),
                cancellationToken);

        if (expected == 0)
        {
            return null;
        }

        var submitted = await context.HomeworkSubmissions
            .AsNoTracking()
            .CountAsync(
                submission => submission.TenantId == tenantId
                    && submission.Status != HomeworkSubmissionStatus.NotSubmitted
                    && submission.Homework.PublishedAt != null
                    && submission.Homework.PublishedAt >= range.FromUtc
                    && submission.Homework.PublishedAt < range.ToUtc,
                cancellationToken);

        // A late roll change can make submissions exceed the expected count; the rate is a
        // share, so it is capped rather than reported above 100%.
        return Math.Min(100m, Percentage(submitted, expected));
    }

    // -----------------------------------------------------------------------
    // Revenue
    // -----------------------------------------------------------------------

    private async Task<TeacherRevenueMetricsDto> LoadRevenueAsync(
        Guid tenantId,
        AnalyticsRange range,
        AnalyticsRange? previous,
        CancellationToken cancellationToken)
    {
        var current = await SumRevenueAsync(tenantId, range, cancellationToken);

        var before = previous is null
            ? null
            : await SumRevenueAsync(tenantId, previous, cancellationToken);

        var currency = await context.TenantSettings
            .AsNoTracking()
            .Where(settings => settings.TenantId == tenantId)
            .Select(settings => settings.Currency)
            .FirstOrDefaultAsync(cancellationToken);

        return new TeacherRevenueMetricsDto
        {
            Total = TrendDto.Of(current.Total, before?.Total),
            GroupRevenue = current.Groups,
            CourseRevenue = current.Courses,
            Enrollments = TrendDto.Of(current.Enrollments, before?.Enrollments),
            AverageOrderValue = current.Enrollments == 0
                ? null
                : Round(current.Total / current.Enrollments),
            Currency = string.IsNullOrWhiteSpace(currency) ? "EGP" : currency
        };
    }

    private async Task<RevenueSlice> SumRevenueAsync(
        Guid tenantId,
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var groups = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Group.TenantId == tenantId
                && enrollment.Status != EnrollmentStatus.Cancelled
                && enrollment.EnrolledAt >= range.FromUtc
                && enrollment.EnrolledAt < range.ToUtc)
            .GroupBy(enrollment => 1)
            .Select(group => new { Revenue = group.Sum(entity => entity.FinalPrice), Count = group.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        var courses = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Course.TenantId == tenantId
                && enrollment.Status != EnrollmentStatus.Cancelled
                && enrollment.EnrolledAt >= range.FromUtc
                && enrollment.EnrolledAt < range.ToUtc)
            .GroupBy(enrollment => 1)
            .Select(group => new { Revenue = group.Sum(entity => entity.FinalPrice), Count = group.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        return new RevenueSlice
        {
            Groups = groups?.Revenue ?? 0m,
            Courses = courses?.Revenue ?? 0m,
            Enrollments = (groups?.Count ?? 0) + (courses?.Count ?? 0)
        };
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Percentage(int part, int total) =>
        total == 0 ? 0m : Round((decimal)part / total * 100m);

    private sealed record RevenueSlice
    {
        public decimal Groups { get; init; }

        public decimal Courses { get; init; }

        public decimal Total => Groups + Courses;

        public int Enrollments { get; init; }
    }
}
