using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Security;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Calendar;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

/// <summary>What kind of thing an entry on the calendar is.</summary>
public enum CalendarEventType
{
    /// <summary>A recurring weekly class, expanded to a concrete occurrence.</summary>
    Class = 1,

    /// <summary>A published exam on its scheduled date.</summary>
    Exam = 2,

    /// <summary>A published homework assignment at its deadline.</summary>
    HomeworkDeadline = 3,

    /// <summary>A session the teacher actually held and took a register for.</summary>
    AttendanceSession = 4
}

/// <summary>
/// One entry on the calendar.
///
/// Every calendar entry is a projection of something that already exists — a weekly schedule,
/// an exam, a deadline, a register — so there is no separate event table to fall out of sync
/// with the thing it describes. <see cref="RelatedEntityType"/> and
/// <see cref="RelatedEntityId"/> point back at the source so the client can deep-link.
/// </summary>
public sealed record CalendarEventDto
{
    /// <summary>
    /// Stable within one response. A recurring class yields one entry per occurrence, so the id
    /// is the schedule id combined with the occurrence date rather than a row id.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    public CalendarEventType Type { get; init; }

    public string TypeName => Type.ToString();

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DateTime StartUtc { get; init; }

    public DateTime EndUtc { get; init; }

    /// <summary>True for an entry that marks a moment rather than occupying a span.</summary>
    public bool IsDeadline { get; init; }

    public Guid? GroupId { get; init; }

    public string? GroupName { get; init; }

    public Guid? SubjectId { get; init; }

    public string? SubjectNameAr { get; init; }

    public Guid? TeacherId { get; init; }

    public string? TeacherName { get; init; }

    public string? Location { get; init; }

    public string? MeetingUrl { get; init; }

    /// <summary>The entity this entry projects, e.g. <c>Exam</c> or <c>GroupSchedule</c>.</summary>
    public string RelatedEntityType { get; init; } = string.Empty;

    public Guid RelatedEntityId { get; init; }

    /// <summary>
    /// State the client can colour by: <c>Scheduled</c>, <c>Open</c>, <c>Closed</c>,
    /// <c>Overdue</c>, <c>Submitted</c>, <c>Completed</c>.
    /// </summary>
    public string Status { get; init; } = string.Empty;
}

public sealed record CalendarResponseDto
{
    public DateTime FromUtc { get; init; }

    public DateTime ToUtc { get; init; }

    /// <summary>
    /// The workspace's configured zone. Stored times are UTC; this is what the client should
    /// render them in so the calendar matches the timetable the teacher set.
    /// </summary>
    public string TimeZoneId { get; init; } = "Egypt Standard Time";

    public int TotalEvents { get; init; }

    public IReadOnlyList<CalendarEventDto> Events { get; init; } = [];
}

// ---------------------------------------------------------------------------
// Query
// ---------------------------------------------------------------------------

/// <summary>
/// The caller's calendar over a date range, merging classes, exams, homework deadlines and
/// held sessions into one ordered list.
///
/// Who sees what is decided from the caller's role, never from a parameter: a student gets the
/// groups they are actually enrolled in, a guardian gets one confirmed child, and a workspace
/// member gets the workspace. There is no id a student can pass to widen their own view.
/// </summary>
public sealed record GetCalendarQuery : IRequest<CalendarResponseDto>
{
    public DateTime From { get; init; }

    public DateTime To { get; init; }

    /// <summary>Restrict to one group. Ignored when the caller has no access to it.</summary>
    public Guid? GroupId { get; init; }

    /// <summary>Restrict to these kinds. Empty means every kind.</summary>
    public IReadOnlyList<CalendarEventType> Types { get; init; } = [];

    /// <summary>A guardian names the child whose calendar they want.</summary>
    public Guid? StudentId { get; init; }

    /// <summary>Widest range one request may span, to bound the occurrence expansion.</summary>
    public const int MaxRangeDays = 180;
}

public sealed class GetCalendarQueryValidator : AbstractValidator<GetCalendarQuery>
{
    public GetCalendarQueryValidator()
    {
        RuleFor(query => query.From).NotEmpty().WithMessage("A 'from' date is required.");
        RuleFor(query => query.To).NotEmpty().WithMessage("A 'to' date is required.");

        RuleFor(query => query)
            .Must(query => query.From <= query.To)
            .WithMessage("The 'from' date must not be after the 'to' date.")
            .WithName(nameof(GetCalendarQuery.From));

        RuleFor(query => query)
            .Must(query => (query.To.Date - query.From.Date).TotalDays <= GetCalendarQuery.MaxRangeDays)
            .WithMessage("A calendar range cannot be longer than " + GetCalendarQuery.MaxRangeDays + " days.")
            .WithName(nameof(GetCalendarQuery.To));

        RuleForEach(query => query.Types).IsInEnum();
    }
}

public sealed class GetCalendarQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    ITenantAuthorizationService tenantAuthorization,
    IParentAccessGuard parentAccess,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetCalendarQuery, CalendarResponseDto>
{
    public async Task<CalendarResponseDto> Handle(
        GetCalendarQuery query,
        CancellationToken cancellationToken)
    {
        var fromUtc = DateTime.SpecifyKind(query.From.Date, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(query.To.Date, DateTimeKind.Utc).AddDays(1);

        var scope = await ResolveScopeAsync(query, cancellationToken);
        var wanted = query.Types.Count == 0 ? null : query.Types.ToHashSet();

        var events = new List<CalendarEventDto>();

        if (Includes(wanted, CalendarEventType.Class))
        {
            events.AddRange(await LoadClassesAsync(scope, fromUtc, toUtc, cancellationToken));
        }

        if (Includes(wanted, CalendarEventType.Exam))
        {
            events.AddRange(await LoadExamsAsync(scope, fromUtc, toUtc, cancellationToken));
        }

        if (Includes(wanted, CalendarEventType.HomeworkDeadline))
        {
            events.AddRange(await LoadHomeworkAsync(scope, fromUtc, toUtc, cancellationToken));
        }

        if (Includes(wanted, CalendarEventType.AttendanceSession))
        {
            events.AddRange(await LoadSessionsAsync(scope, fromUtc, toUtc, cancellationToken));
        }

        var ordered = events
            .OrderBy(entry => entry.StartUtc)
            .ThenBy(entry => entry.Type)
            .ThenBy(entry => entry.Title, StringComparer.Ordinal)
            .ToList();

        return new CalendarResponseDto
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            TimeZoneId = scope.TimeZoneId,
            TotalEvents = ordered.Count,
            Events = ordered
        };
    }

    private static bool Includes(HashSet<CalendarEventType>? wanted, CalendarEventType type) =>
        wanted is null || wanted.Contains(type);

    // -----------------------------------------------------------------------
    // Scope and authorisation
    // -----------------------------------------------------------------------

    /// <summary>
    /// Works out which groups the caller may see, from their role. A student is resolved to
    /// their own active enrollments; a guardian to one confirmed child's; a workspace member to
    /// the whole workspace.
    /// </summary>
    private async Task<CalendarScope> ResolveScopeAsync(
        GetCalendarQuery query,
        CancellationToken cancellationToken)
    {
        // A guardian asking about a child.
        if (currentUser.ParentId is not null && query.StudentId is { } childId)
        {
            await parentAccess.EnsureLinkedAsync(childId, cancellationToken);

            return await ForStudentAsync(childId, query.GroupId, cancellationToken);
        }

        if (currentUser.StudentId is { } studentId)
        {
            return await ForStudentAsync(studentId, query.GroupId, cancellationToken);
        }

        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.ScheduleView, cancellationToken: cancellationToken);

        var groupIds = await context.Groups
            .AsNoTracking()
            .Where(group => group.TenantId == access.TenantId)
            .WhereIf(query.GroupId.HasValue, group => group.Id == query.GroupId!.Value)
            .Select(group => group.Id)
            .ToListAsync(cancellationToken);

        return new CalendarScope
        {
            TenantId = access.TenantId,
            GroupIds = groupIds,
            StudentId = null,
            // Only a caller-supplied filter narrows exams and homework; the full workspace list
            // is for expanding class occurrences, not for restricting anything.
            IsGroupFiltered = query.GroupId.HasValue,
            TimeZoneId = await TimeZoneForAsync(access.TenantId, cancellationToken)
        };
    }

    private async Task<CalendarScope> ForStudentAsync(
        Guid studentId,
        Guid? groupFilter,
        CancellationToken cancellationToken)
    {
        var enrolled = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId
                && enrollment.Status == EnrollmentStatus.Active)
            .Select(enrollment => new { enrollment.GroupId, enrollment.Group.TenantId })
            .ToListAsync(cancellationToken);

        // A group filter the student is not enrolled in narrows to nothing rather than
        // widening their view, so a guessed id leaks no timetable.
        var groupIds = enrolled
            .Select(entry => entry.GroupId)
            .Where(id => groupFilter is null || id == groupFilter.Value)
            .Distinct()
            .ToList();

        var tenantId = enrolled.Select(entry => entry.TenantId).FirstOrDefault();

        return new CalendarScope
        {
            TenantId = tenantId == Guid.Empty ? null : tenantId,
            GroupIds = groupIds,
            StudentId = studentId,
            IsGroupFiltered = true,
            TimeZoneId = tenantId == Guid.Empty
                ? "Egypt Standard Time"
                : await TimeZoneForAsync(tenantId, cancellationToken)
        };
    }

    private async Task<string> TimeZoneForAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await context.TenantSettings
            .AsNoTracking()
            .Where(settings => settings.TenantId == tenantId)
            .Select(settings => settings.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken)
        ?? "Egypt Standard Time";

    // -----------------------------------------------------------------------
    // Classes
    // -----------------------------------------------------------------------

    /// <summary>
    /// Expands each active weekly slot into the concrete occurrences that fall in the range.
    /// A slot is only expanded inside its group's own run, so a timetable does not appear
    /// before the group starts or after it ends.
    /// </summary>
    private async Task<List<CalendarEventDto>> LoadClassesAsync(
        CalendarScope scope,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        if (scope.GroupIds.Count == 0)
        {
            return [];
        }

        var slots = await context.GroupSchedules
            .AsNoTracking()
            .Where(schedule => schedule.IsActive && scope.GroupIds.Contains(schedule.GroupId))
            .Select(schedule => new
            {
                schedule.Id,
                schedule.GroupId,
                GroupName = schedule.Group.Name,
                schedule.Group.TeacherId,
                TeacherName = schedule.Group.Teacher.User.DisplayName,
                schedule.Group.SubjectId,
                SubjectNameAr = schedule.Group.Subject.NameAr,
                schedule.Group.StartDate,
                schedule.Group.EndDate,
                schedule.Group.IsActive,
                schedule.DayOfWeek,
                schedule.StartTime,
                schedule.EndTime,
                schedule.Location,
                schedule.MeetingUrl
            })
            .ToListAsync(cancellationToken);

        var events = new List<CalendarEventDto>();

        foreach (var slot in slots)
        {
            if (!slot.IsActive)
            {
                continue;
            }

            // Start on the first matching weekday at or after the range start.
            var offset = ((int)slot.DayOfWeek - (int)fromUtc.DayOfWeek + 7) % 7;
            var cursor = fromUtc.Date.AddDays(offset);

            for (; cursor < toUtc; cursor = cursor.AddDays(7))
            {
                var start = DateTime.SpecifyKind(cursor.Add(slot.StartTime.ToTimeSpan()), DateTimeKind.Utc);

                if (start < fromUtc || start >= toUtc)
                {
                    continue;
                }

                if (start.Date < slot.StartDate.Date)
                {
                    continue;
                }

                if (slot.EndDate is { } endDate && start.Date > endDate.Date)
                {
                    break;
                }

                // A slot that ends before it starts runs past midnight into the next day.
                var end = slot.EndTime > slot.StartTime
                    ? DateTime.SpecifyKind(cursor.Add(slot.EndTime.ToTimeSpan()), DateTimeKind.Utc)
                    : DateTime.SpecifyKind(cursor.AddDays(1).Add(slot.EndTime.ToTimeSpan()), DateTimeKind.Utc);

                events.Add(new CalendarEventDto
                {
                    Id = "class:" + slot.Id + ":" + start.ToString("yyyyMMdd"),
                    Type = CalendarEventType.Class,
                    Title = slot.GroupName,
                    StartUtc = start,
                    EndUtc = end,
                    IsDeadline = false,
                    GroupId = slot.GroupId,
                    GroupName = slot.GroupName,
                    SubjectId = slot.SubjectId,
                    SubjectNameAr = slot.SubjectNameAr,
                    TeacherId = slot.TeacherId,
                    TeacherName = slot.TeacherName,
                    Location = slot.Location,
                    MeetingUrl = slot.MeetingUrl,
                    RelatedEntityType = "GroupSchedule",
                    RelatedEntityId = slot.Id,
                    Status = "Scheduled"
                });
            }
        }

        return events;
    }

    // -----------------------------------------------------------------------
    // Exams
    // -----------------------------------------------------------------------

    private async Task<List<CalendarEventDto>> LoadExamsAsync(
        CalendarScope scope,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var query = context.Exams
            .AsNoTracking()
            .Where(exam => exam.ExamDate >= fromUtc && exam.ExamDate < toUtc);

        if (scope.StudentId is { } studentId)
        {
            // A student sees a published exam for a group they are in. An exam with no group is
            // not on anyone's personal calendar because nothing says who sits it.
            query = query.Where(exam => exam.IsPublished
                && exam.GroupId != null
                && scope.GroupIds.Contains(exam.GroupId.Value));
        }
        else
        {
            query = query
                .Where(exam => exam.TenantId == scope.TenantId)
                .WhereIf(
                    scope.IsGroupFiltered,
                    exam => exam.GroupId != null && scope.GroupIds.Contains(exam.GroupId.Value));
        }

        var exams = await query
            .Select(exam => new
            {
                exam.Id,
                exam.Title,
                exam.Description,
                exam.ExamDate,
                exam.DurationInMinutes,
                exam.GroupId,
                GroupName = exam.Group != null ? exam.Group.Name : null,
                exam.SubjectId,
                SubjectNameAr = exam.Subject.NameAr,
                exam.TeacherId,
                TeacherName = exam.Teacher.User.DisplayName,
                exam.IsPublished,
                exam.IsOnline
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. exams.Select(exam => new CalendarEventDto
            {
                Id = "exam:" + exam.Id,
                Type = CalendarEventType.Exam,
                Title = exam.Title,
                Description = exam.Description,
                StartUtc = exam.ExamDate,
                // A zero-duration exam still needs a non-empty span to render as a block.
                EndUtc = exam.ExamDate.AddMinutes(exam.DurationInMinutes > 0 ? exam.DurationInMinutes : 60),
                IsDeadline = false,
                GroupId = exam.GroupId,
                GroupName = exam.GroupName,
                SubjectId = exam.SubjectId,
                SubjectNameAr = exam.SubjectNameAr,
                TeacherId = exam.TeacherId,
                TeacherName = exam.TeacherName,
                MeetingUrl = null,
                RelatedEntityType = "Exam",
                RelatedEntityId = exam.Id,
                Status = exam.IsPublished ? "Scheduled" : "Draft"
            })
        ];
    }

    // -----------------------------------------------------------------------
    // Homework deadlines
    // -----------------------------------------------------------------------

    private async Task<List<CalendarEventDto>> LoadHomeworkAsync(
        CalendarScope scope,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var query = context.Homeworks
            .AsNoTracking()
            .Where(homework => homework.DueAt >= fromUtc && homework.DueAt < toUtc);

        if (scope.StudentId is not null)
        {
            query = query.Where(homework => homework.IsPublished
                && scope.GroupIds.Contains(homework.GroupId));
        }
        else
        {
            query = query
                .Where(homework => homework.TenantId == scope.TenantId)
                .WhereIf(scope.IsGroupFiltered, homework => scope.GroupIds.Contains(homework.GroupId));
        }

        var studentId = scope.StudentId;

        var items = await query
            .Select(homework => new
            {
                homework.Id,
                homework.Title,
                homework.Description,
                homework.DueAt,
                homework.GroupId,
                GroupName = homework.Group.Name,
                homework.SubjectId,
                SubjectNameAr = homework.Subject != null ? homework.Subject.NameAr : null,
                homework.TeacherId,
                TeacherName = homework.Teacher.User.DisplayName,
                homework.IsPublished,
                homework.ClosedAt,
                // Only meaningful on a student's own calendar; null elsewhere.
                MyStatus = studentId == null
                    ? null
                    : context.HomeworkSubmissions
                        .Where(submission => submission.HomeworkId == homework.Id
                            && submission.StudentId == studentId.Value)
                        .Select(submission => (HomeworkSubmissionStatus?)submission.Status)
                        .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var utcNow = dateTimeProvider.UtcNow;

        return
        [
            .. items.Select(homework => new CalendarEventDto
            {
                Id = "homework:" + homework.Id,
                Type = CalendarEventType.HomeworkDeadline,
                Title = homework.Title,
                Description = homework.Description,
                // A deadline is a moment, so both ends are the same instant and IsDeadline
                // tells the client to render a marker rather than a block.
                StartUtc = homework.DueAt,
                EndUtc = homework.DueAt,
                IsDeadline = true,
                GroupId = homework.GroupId,
                GroupName = homework.GroupName,
                SubjectId = homework.SubjectId,
                SubjectNameAr = homework.SubjectNameAr,
                TeacherId = homework.TeacherId,
                TeacherName = homework.TeacherName,
                RelatedEntityType = "Homework",
                RelatedEntityId = homework.Id,
                Status = DescribeHomework(
                    homework.IsPublished, homework.ClosedAt, homework.DueAt, homework.MyStatus, utcNow)
            })
        ];
    }

    private static string DescribeHomework(
        bool isPublished,
        DateTime? closedAt,
        DateTime dueAt,
        HomeworkSubmissionStatus? myStatus,
        DateTime utcNow)
    {
        if (!isPublished)
        {
            return "Draft";
        }

        // On a student's calendar their own standing is the most useful thing to colour by.
        if (myStatus is { } status && status != HomeworkSubmissionStatus.NotSubmitted)
        {
            return status.ToString();
        }

        if (closedAt is not null)
        {
            return "Closed";
        }

        return dueAt < utcNow ? "Overdue" : "Open";
    }

    // -----------------------------------------------------------------------
    // Sessions actually held
    // -----------------------------------------------------------------------

    private async Task<List<CalendarEventDto>> LoadSessionsAsync(
        CalendarScope scope,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var query = context.AttendanceSessions
            .AsNoTracking()
            .Where(session => session.SessionDate >= fromUtc && session.SessionDate < toUtc);

        if (scope.StudentId is not null)
        {
            query = query.Where(session => scope.GroupIds.Contains(session.GroupId));
        }
        else
        {
            query = query
                .Where(session => session.TenantId == scope.TenantId)
                .WhereIf(scope.IsGroupFiltered, session => scope.GroupIds.Contains(session.GroupId));
        }

        var sessions = await query
            .Select(session => new
            {
                session.Id,
                session.Title,
                session.Notes,
                session.SessionDate,
                session.StartTime,
                session.EndTime,
                session.Status,
                session.GroupId,
                GroupName = session.Group.Name,
                session.Group.SubjectId,
                SubjectNameAr = session.Group.Subject.NameAr,
                session.Group.TeacherId,
                TeacherName = session.Group.Teacher.User.DisplayName
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. sessions.Select(session =>
            {
                var date = DateTime.SpecifyKind(session.SessionDate.Date, DateTimeKind.Utc);

                var start = session.StartTime is { } startTime
                    ? date.Add(startTime.ToTimeSpan())
                    : date;

                var end = session.EndTime is { } endTime
                    ? date.Add(endTime.ToTimeSpan())
                    : start.AddHours(1);

                return new CalendarEventDto
                {
                    Id = "session:" + session.Id,
                    Type = CalendarEventType.AttendanceSession,
                    Title = string.IsNullOrWhiteSpace(session.Title) ? session.GroupName : session.Title,
                    Description = session.Notes,
                    StartUtc = start,
                    EndUtc = end > start ? end : start.AddHours(1),
                    IsDeadline = false,
                    GroupId = session.GroupId,
                    GroupName = session.GroupName,
                    SubjectId = session.SubjectId,
                    SubjectNameAr = session.SubjectNameAr,
                    TeacherId = session.TeacherId,
                    TeacherName = session.TeacherName,
                    RelatedEntityType = "AttendanceSession",
                    RelatedEntityId = session.Id,
                    Status = session.Status.ToString()
                };
            })
        ];
    }

    /// <summary>Which groups the caller may see, and on whose behalf.</summary>
    private sealed record CalendarScope
    {
        /// <summary>Null only for a student who is enrolled nowhere.</summary>
        public Guid? TenantId { get; init; }

        public List<Guid> GroupIds { get; init; } = [];

        /// <summary>Set when the calendar belongs to one student, which personalises statuses.</summary>
        public Guid? StudentId { get; init; }

        public string TimeZoneId { get; init; } = "Egypt Standard Time";

        /// <summary>
        /// True when the group list is a real restriction rather than the whole workspace. A
        /// staff request with no group filter should not be narrowed by it.
        /// </summary>
        public bool IsGroupFiltered { get; init; }
    }
}
