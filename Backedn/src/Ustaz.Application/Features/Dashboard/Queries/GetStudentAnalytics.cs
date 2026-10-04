using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Dashboard.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Dashboard.Queries;

/// <summary>
/// One student's academic standing.
///
/// Three callers, three authorisation paths, one implementation: a student reading their own
/// record needs nothing beyond being that student; a guardian must hold a confirmed link to
/// the child; a workspace member must hold <c>Students.View</c> and the student must belong to
/// that workspace. Anything else is a 404, so an id cannot be probed for existence.
/// </summary>
public sealed record GetStudentAnalyticsQuery : IRequest<StudentAnalyticsDto>, IHasAnalyticsRange
{
    /// <summary>Omit to read the calling student's own record.</summary>
    public Guid? StudentId { get; init; }

    public AnalyticsPeriod Period { get; init; } = AnalyticsPeriod.Last30Days;

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }

    /// <summary>How many subjects to return in the strong and weak lists.</summary>
    public int Take { get; init; } = 3;
}

public sealed class GetStudentAnalyticsQueryValidator : AbstractValidator<GetStudentAnalyticsQuery>
{
    public GetStudentAnalyticsQueryValidator()
    {
        this.ApplyAnalyticsRangeRules();
        RuleFor(query => query.Take).InclusiveBetween(1, 20);
    }
}

public sealed class GetStudentAnalyticsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    ITenantAuthorizationService tenantAuthorization,
    IParentAccessGuard parentAccess,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetStudentAnalyticsQuery, StudentAnalyticsDto>
{
    /// <summary>
    /// How the headline score is composed. Exams dominate, homework counts, attendance is a
    /// tie-breaker. Missing components drop out and the rest are renormalised, so a student with
    /// no exams yet is judged on what they have actually done.
    /// </summary>
    private const decimal ExamWeight = 0.5m;
    private const decimal HomeworkWeight = 0.3m;
    private const decimal AttendanceWeight = 0.2m;

    /// <summary>A subject needs this many marks before it is called strong or weak.</summary>
    private const int MinimumMarksForSubjectVerdict = 2;

    /// <summary>At or above this average a subject is a strength.</summary>
    private const decimal StrongSubjectThreshold = 75m;

    /// <summary>Below this average a subject is a weakness.</summary>
    private const decimal WeakSubjectThreshold = 60m;

    public async Task<StudentAnalyticsDto> Handle(
        GetStudentAnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = await ResolveAndAuthoriseAsync(query.StudentId, cancellationToken);

        var studentName = await context.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == studentId)
            .Select(profile => profile.User.DisplayName)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Student", studentId);

        var utcNow = dateTimeProvider.UtcNow;
        var range = AnalyticsRange.Resolve(query.Period, query.From, query.To, utcNow);
        var take = Math.Clamp(query.Take, 1, 20);

        var examAverage = await ExamAverageAsync(studentId, range, cancellationToken);
        var homeworkAverage = await HomeworkAverageAsync(studentId, range, cancellationToken);
        var attendance = await AttendancePercentageAsync(studentId, range, cancellationToken);
        var subjects = await SubjectBreakdownAsync(studentId, range, cancellationToken);

        return new StudentAnalyticsDto
        {
            StudentId = studentId,
            StudentName = studentName,
            Window = AnalyticsWindowDto.From(range),
            OverallScore = Blend(examAverage, homeworkAverage, attendance),
            ExamAverage = examAverage,
            HomeworkAverage = homeworkAverage,
            AttendancePercentage = attendance,
            CourseCompletion = await CourseCompletionAsync(studentId, cancellationToken),
            LessonCompletion = await LessonCompletionAsync(studentId, cancellationToken),
            Engagement = await EngagementAsync(studentId, range, utcNow, cancellationToken),
            Workload = await WorkloadAsync(studentId, utcNow, cancellationToken),
            SubjectBreakdown = subjects,
            StrongSubjects =
            [
                .. subjects
                    .Where(subject => subject.MarkCount >= MinimumMarksForSubjectVerdict
                        && subject.AveragePercentage >= StrongSubjectThreshold)
                    .OrderByDescending(subject => subject.AveragePercentage)
                    .Take(take)
            ],
            WeakSubjects =
            [
                .. subjects
                    .Where(subject => subject.MarkCount >= MinimumMarksForSubjectVerdict
                        && subject.AveragePercentage < WeakSubjectThreshold)
                    .OrderBy(subject => subject.AveragePercentage)
                    .Take(take)
            ]
        };
    }

    // -----------------------------------------------------------------------
    // Authorisation
    // -----------------------------------------------------------------------

    private async Task<Guid> ResolveAndAuthoriseAsync(
        Guid? requestedStudentId,
        CancellationToken cancellationToken)
    {
        var ownStudentId = currentUser.StudentId;

        // No id supplied: the caller must be a student, reading themselves.
        if (requestedStudentId is null)
        {
            return ownStudentId ?? throw new ForbiddenAccessException(
                "Specify a student, or sign in as one.");
        }

        var studentId = requestedStudentId.Value;

        if (ownStudentId == studentId)
        {
            return studentId;
        }

        // A guardian may read a child they hold a confirmed link to.
        if (currentUser.ParentId is not null)
        {
            await parentAccess.EnsureLinkedAsync(studentId, cancellationToken);

            return studentId;
        }

        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.StudentsView, cancellationToken: cancellationToken);

        // Membership of this workspace is what makes the student readable, so it is checked
        // against the link table rather than inferred from the student existing at all.
        var inWorkspace = await context.TenantStudents
            .AsNoTracking()
            .AnyAsync(
                link => link.TenantId == access.TenantId && link.StudentId == studentId,
                cancellationToken);

        if (!inWorkspace)
        {
            throw new NotFoundException("Student", studentId);
        }

        return studentId;
    }

    // -----------------------------------------------------------------------
    // Academic averages
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mean exam percentage across both storage shapes. Absentees are excluded: a missed exam
    /// is an attendance problem, and folding a zero in would misreport ability.
    /// </summary>
    private async Task<decimal?> ExamAverageAsync(
        Guid studentId,
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var offline = await context.ExamResults
            .AsNoTracking()
            .Where(result => result.StudentId == studentId
                && !result.IsAbsent
                && result.Exam.ExamDate >= range.FromUtc
                && result.Exam.ExamDate < range.ToUtc)
            .GroupBy(result => 1)
            .Select(group => new { Count = group.Count(), Sum = group.Sum(result => result.Percentage) })
            .FirstOrDefaultAsync(cancellationToken);

        var online = await context.ExamAttempts
            .AsNoTracking()
            .Where(attempt => attempt.StudentId == studentId
                && attempt.Status == ExamAttemptStatus.Graded
                && attempt.SubmittedAt != null
                && attempt.SubmittedAt >= range.FromUtc
                && attempt.SubmittedAt < range.ToUtc)
            .GroupBy(attempt => 1)
            .Select(group => new { Count = group.Count(), Sum = group.Sum(attempt => attempt.Percentage) })
            .FirstOrDefaultAsync(cancellationToken);

        var count = (offline?.Count ?? 0) + (online?.Count ?? 0);

        return count == 0 ? null : Round(((offline?.Sum ?? 0m) + (online?.Sum ?? 0m)) / count);
    }

    private async Task<decimal?> HomeworkAverageAsync(
        Guid studentId,
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var graded = await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(submission => submission.StudentId == studentId
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

        return graded is null or { Count: 0 } ? null : Round(graded.Sum / graded.Count);
    }

    private async Task<decimal?> AttendancePercentageAsync(
        Guid studentId,
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var records = await context.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.StudentId == studentId
                && record.Session.SessionDate >= range.FromUtc
                && record.Session.SessionDate < range.ToUtc)
            .GroupBy(record => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Attended = group.Count(record => record.Status != AttendanceStatus.Absent)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return records is null or { Count: 0 } ? null : Percentage(records.Attended, records.Count);
    }

    /// <summary>
    /// Combines whichever components exist, renormalising the weights over them. With no
    /// component at all the result is null rather than zero.
    /// </summary>
    private static decimal? Blend(decimal? exams, decimal? homework, decimal? attendance)
    {
        var weighted = 0m;
        var weight = 0m;

        if (exams is { } examValue)
        {
            weighted += examValue * ExamWeight;
            weight += ExamWeight;
        }

        if (homework is { } homeworkValue)
        {
            weighted += homeworkValue * HomeworkWeight;
            weight += HomeworkWeight;
        }

        if (attendance is { } attendanceValue)
        {
            weighted += attendanceValue * AttendanceWeight;
            weight += AttendanceWeight;
        }

        return weight == 0m ? null : Round(weighted / weight);
    }

    // -----------------------------------------------------------------------
    // Completion
    // -----------------------------------------------------------------------

    private async Task<decimal?> CourseCompletionAsync(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var enrollments = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId
                && enrollment.Status != EnrollmentStatus.Cancelled)
            .GroupBy(enrollment => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Completed = group.Count(entity => entity.Status == EnrollmentStatus.Completed)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return enrollments is null or { Count: 0 }
            ? null
            : Percentage(enrollments.Completed, enrollments.Count);
    }

    /// <summary>
    /// Completed lessons over the published lessons of every course the student is enrolled in.
    /// Published is the right denominator: a draft lesson is not work the student can do.
    /// </summary>
    private async Task<decimal?> LessonCompletionAsync(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var courseIds = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId
                && enrollment.Status != EnrollmentStatus.Cancelled)
            .Select(enrollment => enrollment.CourseId)
            .ToListAsync(cancellationToken);

        if (courseIds.Count == 0)
        {
            return null;
        }

        var totalLessons = await context.CourseLessons
            .AsNoTracking()
            .CountAsync(
                lesson => lesson.IsPublished && courseIds.Contains(lesson.Section.CourseId),
                cancellationToken);

        if (totalLessons == 0)
        {
            return null;
        }

        var completed = await context.CourseLessonProgress
            .AsNoTracking()
            .CountAsync(
                progress => progress.StudentId == studentId
                    && progress.IsCompleted
                    && courseIds.Contains(progress.CourseId),
                cancellationToken);

        return Percentage(completed, totalLessons);
    }

    // -----------------------------------------------------------------------
    // Engagement
    // -----------------------------------------------------------------------

    private async Task<StudentEngagementDto> EngagementAsync(
        Guid studentId,
        AnalyticsRange range,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        // The streak has to look further back than the requested window, so activity days are
        // read over a fixed trailing year rather than over the window.
        var streakHorizon = utcNow.Date.AddDays(-365);

        var activityDays = await LoadActivityDaysAsync(studentId, streakHorizon, cancellationToken);

        var lastActivity = await LastActivityAtAsync(studentId, cancellationToken);

        var watchedSeconds = await context.CourseLessonProgress
            .AsNoTracking()
            .Where(progress => progress.StudentId == studentId)
            .SumAsync(progress => (long?)progress.WatchedSeconds, cancellationToken) ?? 0L;

        var lessonsInWindow = await context.CourseLessonProgress
            .AsNoTracking()
            .CountAsync(
                progress => progress.StudentId == studentId
                    && progress.IsCompleted
                    && progress.CompletedAt != null
                    && progress.CompletedAt >= range.FromUtc
                    && progress.CompletedAt < range.ToUtc,
                cancellationToken);

        var windowFrom = DateOnly.FromDateTime(range.IsAllTime ? streakHorizon : range.FromUtc);
        var windowTo = DateOnly.FromDateTime(range.ToUtc);

        return new StudentEngagementDto
        {
            LearningStreakDays = CurrentStreak(activityDays, DateOnly.FromDateTime(utcNow.Date)),
            LongestStreakDays = LongestStreak(activityDays),
            ActiveDaysInWindow = activityDays.Count(day => day >= windowFrom && day < windowTo),
            LastActivityAt = lastActivity,
            LessonsCompletedInWindow = lessonsInWindow,
            TotalLearningMinutes = (int)(watchedSeconds / 60L)
        };
    }

    /// <summary>
    /// The distinct calendar days the student did something, over a trailing year. Four sources
    /// are read and merged in memory: they live in different tables with different date columns,
    /// and a year of one student's activity is a small set.
    /// </summary>
    private async Task<SortedSet<DateOnly>> LoadActivityDaysAsync(
        Guid studentId,
        DateTime since,
        CancellationToken cancellationToken)
    {
        var lessonDays = await context.CourseLessonProgress
            .AsNoTracking()
            .Where(progress => progress.StudentId == studentId && progress.LastAccessedAt >= since)
            .Select(progress => progress.LastAccessedAt)
            .ToListAsync(cancellationToken);

        var homeworkDays = await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(submission => submission.StudentId == studentId
                && submission.SubmittedAt != null
                && submission.SubmittedAt >= since)
            .Select(submission => submission.SubmittedAt!.Value)
            .ToListAsync(cancellationToken);

        var examDays = await context.ExamAttempts
            .AsNoTracking()
            .Where(attempt => attempt.StudentId == studentId && attempt.StartedAt >= since)
            .Select(attempt => attempt.StartedAt)
            .ToListAsync(cancellationToken);

        var attendanceDays = await context.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.StudentId == studentId
                && record.Status != AttendanceStatus.Absent
                && record.Session.SessionDate >= since)
            .Select(record => record.Session.SessionDate)
            .ToListAsync(cancellationToken);

        var days = new SortedSet<DateOnly>();

        foreach (var timestamp in lessonDays
            .Concat(homeworkDays)
            .Concat(examDays)
            .Concat(attendanceDays))
        {
            days.Add(DateOnly.FromDateTime(timestamp));
        }

        return days;
    }

    private async Task<DateTime?> LastActivityAtAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var lesson = await context.CourseLessonProgress
            .AsNoTracking()
            .Where(progress => progress.StudentId == studentId)
            .MaxAsync(progress => (DateTime?)progress.LastAccessedAt, cancellationToken);

        var homework = await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(submission => submission.StudentId == studentId)
            .MaxAsync(submission => submission.SubmittedAt, cancellationToken);

        var exam = await context.ExamAttempts
            .AsNoTracking()
            .Where(attempt => attempt.StudentId == studentId)
            .MaxAsync(attempt => (DateTime?)attempt.StartedAt, cancellationToken);

        return new[] { lesson, homework, exam }
            .Where(value => value.HasValue)
            .OrderByDescending(value => value)
            .FirstOrDefault();
    }

    /// <summary>
    /// Consecutive active days ending today or yesterday. Yesterday still counts so a student
    /// who has not opened the app yet this morning does not lose the run they built; anything
    /// older breaks it.
    /// </summary>
    private static int CurrentStreak(SortedSet<DateOnly> days, DateOnly today)
    {
        if (days.Count == 0)
        {
            return 0;
        }

        var cursor = days.Contains(today)
            ? today
            : days.Contains(today.AddDays(-1)) ? today.AddDays(-1) : (DateOnly?)null;

        if (cursor is null)
        {
            return 0;
        }

        var streak = 0;
        var day = cursor.Value;

        while (days.Contains(day))
        {
            streak++;
            day = day.AddDays(-1);
        }

        return streak;
    }

    private static int LongestStreak(SortedSet<DateOnly> days)
    {
        var longest = 0;
        var run = 0;
        DateOnly? previous = null;

        foreach (var day in days)
        {
            run = previous is { } last && last.AddDays(1) == day ? run + 1 : 1;
            previous = day;
            longest = Math.Max(longest, run);
        }

        return longest;
    }

    // -----------------------------------------------------------------------
    // Workload
    // -----------------------------------------------------------------------

    private async Task<StudentWorkloadDto> WorkloadAsync(
        Guid studentId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var courses = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId
                && enrollment.Status != EnrollmentStatus.Cancelled)
            .GroupBy(enrollment => 1)
            .Select(group => new
            {
                Active = group.Count(entity => entity.Status == EnrollmentStatus.Active),
                Completed = group.Count(entity => entity.Status == EnrollmentStatus.Completed)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Homework the student can still be held to: published, for a group they are in, and
        // with nothing handed in.
        var outstanding = context.Homeworks
            .AsNoTracking()
            .Where(homework => homework.IsPublished
                && context.GroupEnrollments.Any(enrollment =>
                    enrollment.GroupId == homework.GroupId
                    && enrollment.StudentId == studentId
                    && enrollment.Status == EnrollmentStatus.Active)
                && !context.HomeworkSubmissions.Any(submission =>
                    submission.HomeworkId == homework.Id
                    && submission.StudentId == studentId
                    && submission.Status != HomeworkSubmissionStatus.NotSubmitted));

        return new StudentWorkloadDto
        {
            ActiveGroups = await context.GroupEnrollments
                .AsNoTracking()
                .CountAsync(
                    enrollment => enrollment.StudentId == studentId
                        && enrollment.Status == EnrollmentStatus.Active,
                    cancellationToken),
            ActiveCourses = courses?.Active ?? 0,
            CompletedCourses = courses?.Completed ?? 0,
            OverdueHomework = await outstanding.CountAsync(
                homework => homework.DueAt < utcNow, cancellationToken),
            PendingHomework = await outstanding.CountAsync(
                homework => homework.DueAt >= utcNow, cancellationToken),
            UpcomingExams = await context.Exams
                .AsNoTracking()
                .CountAsync(
                    exam => exam.IsPublished
                        && exam.ExamDate >= utcNow
                        && exam.GroupId != null
                        && context.GroupEnrollments.Any(enrollment =>
                            enrollment.GroupId == exam.GroupId
                            && enrollment.StudentId == studentId
                            && enrollment.Status == EnrollmentStatus.Active),
                    cancellationToken)
        };
    }

    // -----------------------------------------------------------------------
    // Subjects
    // -----------------------------------------------------------------------

    /// <summary>
    /// Per-subject exam performance, merging offline marks and online sittings. Homework has no
    /// mandatory subject, so it is left out rather than attributed to a guess.
    /// </summary>
    private async Task<List<StudentSubjectStandingDto>> SubjectBreakdownAsync(
        Guid studentId,
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var offline = await context.ExamResults
            .AsNoTracking()
            .Where(result => result.StudentId == studentId
                && !result.IsAbsent
                && result.Exam.ExamDate >= range.FromUtc
                && result.Exam.ExamDate < range.ToUtc)
            .GroupBy(result => new
            {
                result.Exam.SubjectId,
                result.Exam.Subject.NameAr,
                result.Exam.Subject.NameEn
            })
            .Select(group => new SubjectSlice
            {
                SubjectId = group.Key.SubjectId,
                NameAr = group.Key.NameAr,
                NameEn = group.Key.NameEn,
                Count = group.Count(),
                Sum = group.Sum(result => result.Percentage),
                Passed = group.Count(result => result.IsPassed)
            })
            .ToListAsync(cancellationToken);

        var online = await context.ExamAttempts
            .AsNoTracking()
            .Where(attempt => attempt.StudentId == studentId
                && attempt.Status == ExamAttemptStatus.Graded
                && attempt.SubmittedAt != null
                && attempt.SubmittedAt >= range.FromUtc
                && attempt.SubmittedAt < range.ToUtc)
            .GroupBy(attempt => new
            {
                attempt.Exam.SubjectId,
                attempt.Exam.Subject.NameAr,
                attempt.Exam.Subject.NameEn
            })
            .Select(group => new SubjectSlice
            {
                SubjectId = group.Key.SubjectId,
                NameAr = group.Key.NameAr,
                NameEn = group.Key.NameEn,
                Count = group.Count(),
                Sum = group.Sum(attempt => attempt.Percentage),
                Passed = group.Count(attempt => attempt.IsPassed)
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. offline
                .Concat(online)
                .GroupBy(slice => slice.SubjectId)
                .Select(group =>
                {
                    var count = group.Sum(slice => slice.Count);
                    var first = group.First();

                    return new StudentSubjectStandingDto
                    {
                        SubjectId = group.Key,
                        SubjectNameAr = first.NameAr,
                        SubjectNameEn = first.NameEn,
                        MarkCount = count,
                        AveragePercentage = count == 0
                            ? 0m
                            : Round(group.Sum(slice => slice.Sum) / count),
                        PassedCount = group.Sum(slice => slice.Passed),
                        PassRate = Percentage(group.Sum(slice => slice.Passed), count)
                    };
                })
                .OrderByDescending(entry => entry.AveragePercentage)
        ];
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Percentage(int part, int total) =>
        total == 0 ? 0m : Round((decimal)part / total * 100m);

    /// <summary>One subject's marks from one of the two storage shapes, before they are merged.</summary>
    private sealed record SubjectSlice
    {
        public Guid SubjectId { get; init; }

        public string NameAr { get; init; } = string.Empty;

        public string NameEn { get; init; } = string.Empty;

        public int Count { get; init; }

        public decimal Sum { get; init; }

        public int Passed { get; init; }
    }
}
