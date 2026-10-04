using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Features.Attendance.Dtos;
using Ustaz.Application.Features.Exams.Dtos;
using Ustaz.Application.Features.Notifications;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Parents;

public sealed record ParentDashboardDto
{
    public int ChildrenCount { get; init; }

    public int UnreadNotificationsCount { get; init; }

    public int AbsenceAlertsCount { get; init; }

    public int PendingHomeworkCount { get; init; }

    public int UpcomingExamsCount { get; init; }

    public decimal OutstandingPayments { get; init; }

    public IReadOnlyList<ChildOverviewDto> Children { get; init; } = [];

    public IReadOnlyList<ScheduleSlotDto> TodaysSchedule { get; init; } = [];

    public IReadOnlyList<ScheduleSlotDto> UpcomingClasses { get; init; } = [];

    public IReadOnlyList<AttendanceRecordDto> RecentAbsences { get; init; } = [];

    public IReadOnlyList<ExamResultDto> RecentGrades { get; init; } = [];

    public IReadOnlyList<NotificationDto> RecentNotifications { get; init; } = [];
}

/// <summary>A compact per-child summary for the dashboard cards.</summary>
public sealed record ChildOverviewDto
{
    public Guid StudentId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? ProfileImageUrl { get; init; }

    public string? GradeName { get; init; }

    public int ActiveGroupsCount { get; init; }

    public int ActiveCoursesCount { get; init; }

    public decimal AttendanceRate { get; init; }

    public decimal AverageGradePercentage { get; init; }

    public int PendingHomeworkCount { get; init; }

    public int UpcomingExamsCount { get; init; }
}

public sealed record GetParentDashboardQuery : IRequest<ParentDashboardDto>;

public sealed class GetParentDashboardQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IParentAccessGuard parentAccess,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetParentDashboardQuery, ParentDashboardDto>
{
    private const int PreviewSize = 5;

    public async Task<ParentDashboardDto> Handle(
        GetParentDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var childIds = await parentAccess.LinkedStudentIdsAsync(cancellationToken);
        var utcNow = dateTimeProvider.UtcNow;

        var unread = await context.Notifications
            .AsNoTracking()
            .CountAsync(notification => notification.UserId == userId && !notification.IsRead, cancellationToken);

        var recentNotifications = await context.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(PreviewSize)
            .Select(notification => new NotificationDto
            {
                Id = notification.Id,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type,
                IsRead = notification.IsRead,
                ActionUrl = notification.ActionUrl,
                ReferenceId = notification.ReferenceId,
                CreatedAt = notification.CreatedAt,
                ReadAt = notification.ReadAt
            })
            .ToListAsync(cancellationToken);

        if (childIds.Count == 0)
        {
            return new ParentDashboardDto
            {
                UnreadNotificationsCount = unread,
                RecentNotifications = recentNotifications
            };
        }

        var children = await BuildChildOverviewsAsync(childIds, utcNow, cancellationToken);

        var recentAbsences = await context.AttendanceRecords
            .AsNoTracking()
            .Where(record => childIds.Contains(record.StudentId) && record.Status == AttendanceStatus.Absent)
            .OrderByDescending(record => record.Session.SessionDate)
            .Take(PreviewSize)
            .Select(Attendance.AttendanceProjections.ToRecordDto)
            .ToListAsync(cancellationToken);

        var recentGrades = await context.ExamResults
            .AsNoTracking()
            .Where(result => childIds.Contains(result.StudentId) && result.Exam.ResultsPublished)
            .OrderByDescending(result => result.Exam.ExamDate)
            .Take(PreviewSize)
            .Select(Exams.ExamProjections.ToResultDto)
            .ToListAsync(cancellationToken);

        var schedule = await BuildScheduleAsync(childIds, utcNow, cancellationToken);

        var outstanding = await context.EducationalPayments
            .AsNoTracking()
            .Where(payment => childIds.Contains(payment.StudentId) && payment.Status == PaymentStatus.Pending)
            .SumAsync(payment => (decimal?)payment.NetAmount, cancellationToken) ?? 0m;

        return new ParentDashboardDto
        {
            ChildrenCount = childIds.Count,
            UnreadNotificationsCount = unread,
            AbsenceAlertsCount = recentAbsences.Count,
            PendingHomeworkCount = children.Sum(child => child.PendingHomeworkCount),
            UpcomingExamsCount = children.Sum(child => child.UpcomingExamsCount),
            OutstandingPayments = outstanding,
            Children = children,
            TodaysSchedule = [.. schedule.Where(slot => slot.DayOfWeek == utcNow.DayOfWeek)],
            UpcomingClasses = [.. schedule.Take(PreviewSize)],
            RecentAbsences = recentAbsences,
            RecentGrades = recentGrades,
            RecentNotifications = recentNotifications
        };
    }

    private async Task<List<ChildOverviewDto>> BuildChildOverviewsAsync(
        IReadOnlyList<Guid> childIds,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var basics = await context.StudentProfiles
            .AsNoTracking()
            .Where(student => childIds.Contains(student.Id))
            .Select(student => new
            {
                student.Id,
                student.User.DisplayName,
                student.User.ProfileImageUrl,
                GradeName = student.Grade != null ? student.Grade.NameAr : null,
                ActiveGroups = student.GroupEnrollments.Count(enrollment =>
                    enrollment.Status == EnrollmentStatus.Active),
                ActiveCourses = student.CourseEnrollments.Count(enrollment =>
                    enrollment.Status == EnrollmentStatus.Active)
            })
            .ToListAsync(cancellationToken);

        var attendance = await context.AttendanceRecords
            .AsNoTracking()
            .Where(record => childIds.Contains(record.StudentId))
            .GroupBy(record => record.StudentId)
            .Select(group => new
            {
                StudentId = group.Key,
                Total = group.Count(),
                Attended = group.Count(record =>
                    record.Status == AttendanceStatus.Present || record.Status == AttendanceStatus.Late)
            })
            .ToListAsync(cancellationToken);

        var grades = await context.ExamResults
            .AsNoTracking()
            .Where(result => childIds.Contains(result.StudentId) && result.Exam.ResultsPublished)
            .GroupBy(result => result.StudentId)
            .Select(group => new { StudentId = group.Key, Average = group.Average(result => result.Percentage) })
            .ToListAsync(cancellationToken);

        var pendingHomework = await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(submission => childIds.Contains(submission.StudentId)
                && submission.Status == HomeworkSubmissionStatus.NotSubmitted
                && submission.Homework.DueAt >= utcNow)
            .GroupBy(submission => submission.StudentId)
            .Select(group => new { StudentId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var upcomingExams = await context.ExamResults
            .AsNoTracking()
            .Where(result => childIds.Contains(result.StudentId) && result.Exam.ExamDate >= utcNow)
            .GroupBy(result => result.StudentId)
            .Select(group => new { StudentId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return [.. basics.Select(child =>
        {
            var attendanceEntry = attendance.FirstOrDefault(entry => entry.StudentId == child.Id);

            return new ChildOverviewDto
            {
                StudentId = child.Id,
                DisplayName = child.DisplayName,
                ProfileImageUrl = child.ProfileImageUrl,
                GradeName = child.GradeName,
                ActiveGroupsCount = child.ActiveGroups,
                ActiveCoursesCount = child.ActiveCourses,
                AttendanceRate = attendanceEntry is null || attendanceEntry.Total == 0
                    ? 0m
                    : Math.Round((decimal)attendanceEntry.Attended / attendanceEntry.Total * 100m, 2,
                        MidpointRounding.AwayFromZero),
                AverageGradePercentage = Math.Round(
                    grades.FirstOrDefault(entry => entry.StudentId == child.Id)?.Average ?? 0m,
                    2,
                    MidpointRounding.AwayFromZero),
                PendingHomeworkCount = pendingHomework.FirstOrDefault(entry => entry.StudentId == child.Id)?.Count ?? 0,
                UpcomingExamsCount = upcomingExams.FirstOrDefault(entry => entry.StudentId == child.Id)?.Count ?? 0
            };
        })];
    }

    private async Task<IReadOnlyList<ScheduleSlotDto>> BuildScheduleAsync(
        IReadOnlyList<Guid> childIds,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var slots = await context.GroupSchedules
            .AsNoTracking()
            .Where(schedule => schedule.IsActive
                && schedule.Group.IsActive
                && schedule.Group.Enrollments.Any(enrollment =>
                    childIds.Contains(enrollment.StudentId) && enrollment.Status == EnrollmentStatus.Active))
            .OrderBy(schedule => schedule.DayOfWeek)
            .ThenBy(schedule => schedule.StartTime)
            .Select(Schedule.ScheduleProjections.ToSlot)
            .ToListAsync(cancellationToken);

        return Schedule.ScheduleProjections.WithNextOccurrences(slots, utcNow);
    }
}

/// <summary>Academic progress for one child.</summary>
public sealed record GetChildProgressQuery(Guid StudentId) : IRequest<ChildOverviewDto>;

public sealed class GetChildProgressQueryHandler(
    IApplicationDbContext context,
    IParentAccessGuard parentAccess,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetChildProgressQuery, ChildOverviewDto>
{
    public async Task<ChildOverviewDto> Handle(GetChildProgressQuery query, CancellationToken cancellationToken)
    {
        await parentAccess.EnsureLinkedAsync(query.StudentId, cancellationToken);

        var utcNow = dateTimeProvider.UtcNow;

        var basics = await context.StudentProfiles
            .AsNoTracking()
            .Where(student => student.Id == query.StudentId)
            .Select(student => new
            {
                student.Id,
                student.User.DisplayName,
                student.User.ProfileImageUrl,
                GradeName = student.Grade != null ? student.Grade.NameAr : null,
                ActiveGroups = student.GroupEnrollments.Count(enrollment =>
                    enrollment.Status == EnrollmentStatus.Active),
                ActiveCourses = student.CourseEnrollments.Count(enrollment =>
                    enrollment.Status == EnrollmentStatus.Active)
            })
            .FirstAsync(cancellationToken);

        var attendance = await context.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.StudentId == query.StudentId)
            .GroupBy(record => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Attended = group.Count(record =>
                    record.Status == AttendanceStatus.Present || record.Status == AttendanceStatus.Late)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var average = await context.ExamResults
            .AsNoTracking()
            .Where(result => result.StudentId == query.StudentId && result.Exam.ResultsPublished)
            .AverageAsync(result => (decimal?)result.Percentage, cancellationToken) ?? 0m;

        var pendingHomework = await context.HomeworkSubmissions
            .AsNoTracking()
            .CountAsync(
                submission => submission.StudentId == query.StudentId
                    && submission.Status == HomeworkSubmissionStatus.NotSubmitted
                    && submission.Homework.DueAt >= utcNow,
                cancellationToken);

        return new ChildOverviewDto
        {
            StudentId = basics.Id,
            DisplayName = basics.DisplayName,
            ProfileImageUrl = basics.ProfileImageUrl,
            GradeName = basics.GradeName,
            ActiveGroupsCount = basics.ActiveGroups,
            ActiveCoursesCount = basics.ActiveCourses,
            AttendanceRate = attendance is null || attendance.Total == 0
                ? 0m
                : Math.Round((decimal)attendance.Attended / attendance.Total * 100m, 2, MidpointRounding.AwayFromZero),
            AverageGradePercentage = Math.Round(average, 2, MidpointRounding.AwayFromZero),
            PendingHomeworkCount = pendingHomework
        };
    }
}
