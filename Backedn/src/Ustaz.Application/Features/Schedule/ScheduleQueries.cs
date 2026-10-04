using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Schedule;

internal static class ScheduleProjections
{
    public static Expression<Func<GroupSchedule, ScheduleSlotDto>> ToSlot =>
        schedule => new ScheduleSlotDto
        {
            Id = schedule.Id,
            GroupId = schedule.GroupId,
            GroupName = schedule.Group.Name,
            GroupSlug = schedule.Group.Slug,
            TeacherId = schedule.Group.TeacherId,
            TeacherName = schedule.Group.Teacher.User.DisplayName,
            SubjectId = schedule.Group.SubjectId,
            SubjectNameAr = schedule.Group.Subject.NameAr,
            SubjectNameEn = schedule.Group.Subject.NameEn,
            DayOfWeek = schedule.DayOfWeek,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            Location = schedule.Location,
            MeetingUrl = schedule.MeetingUrl,
            IsActive = schedule.IsActive
        };

    /// <summary>Adds the next concrete occurrence and orders the slots by it.</summary>
    public static IReadOnlyList<ScheduleSlotDto> WithNextOccurrences(
        IEnumerable<ScheduleSlotDto> slots,
        DateTime fromUtc) =>
        [.. slots
            .Select(slot => slot with { NextOccurrenceUtc = NextOccurrence(slot.DayOfWeek, slot.StartTime, fromUtc) })
            .OrderBy(slot => slot.NextOccurrenceUtc)];

    public static DateTime NextOccurrence(DayOfWeek day, TimeOnly startTime, DateTime fromUtc)
    {
        var daysAhead = ((int)day - (int)fromUtc.DayOfWeek + 7) % 7;
        var candidate = fromUtc.Date.AddDays(daysAhead).Add(startTime.ToTimeSpan());

        if (candidate < fromUtc)
        {
            candidate = candidate.AddDays(7);
        }

        return DateTime.SpecifyKind(candidate, DateTimeKind.Utc);
    }
}

/// <summary>Public schedule browser, filterable by teacher, subject, grade or weekday.</summary>
public sealed record GetPublicScheduleQuery : IRequest<IReadOnlyList<ScheduleSlotDto>>
{
    public Guid? TeacherId { get; init; }

    public Guid? SubjectId { get; init; }

    public Guid? GradeId { get; init; }

    public DayOfWeek? DayOfWeek { get; init; }

    public TeachingMode? TeachingMode { get; init; }

    /// <summary>
    /// Hard cap on the slots returned. The platform-wide timetable grows without bound as
    /// workspaces are added, so this endpoint never streams the whole table.
    /// </summary>
    public int Take { get; init; } = DefaultTake;

    public const int DefaultTake = 100;

    public const int MaxTake = 200;
}

public sealed class GetPublicScheduleQueryHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetPublicScheduleQuery, IReadOnlyList<ScheduleSlotDto>>
{
    public async Task<IReadOnlyList<ScheduleSlotDto>> Handle(
        GetPublicScheduleQuery query,
        CancellationToken cancellationToken)
    {
        var slots = await context.GroupSchedules
            .AsNoTracking()
            .Where(schedule => schedule.IsActive && schedule.Group.IsPublished && schedule.Group.IsActive)
            .WhereIf(query.TeacherId.HasValue, schedule => schedule.Group.TeacherId == query.TeacherId!.Value)
            .WhereIf(query.SubjectId.HasValue, schedule => schedule.Group.SubjectId == query.SubjectId!.Value)
            .WhereIf(query.GradeId.HasValue, schedule => schedule.Group.GradeId == query.GradeId!.Value)
            .WhereIf(query.DayOfWeek.HasValue, schedule => schedule.DayOfWeek == query.DayOfWeek!.Value)
            .WhereIf(query.TeachingMode.HasValue, schedule => schedule.Group.TeachingMode == query.TeachingMode!.Value)
            .OrderBy(schedule => schedule.DayOfWeek)
            .ThenBy(schedule => schedule.StartTime)
            .ThenBy(schedule => schedule.Id)
            .Take(Math.Clamp(query.Take, 1, GetPublicScheduleQuery.MaxTake))
            .Select(ScheduleProjections.ToSlot)
            .ToListAsync(cancellationToken);

        return ScheduleProjections.WithNextOccurrences(slots, dateTimeProvider.UtcNow);
    }
}

/// <summary>The weekly timetable of the calling teacher.</summary>
public sealed record GetTeacherScheduleQuery : IRequest<IReadOnlyList<ScheduleSlotDto>>
{
    public Guid? TeacherId { get; init; }

    public bool IncludeUnpublished { get; init; } = true;
}

public sealed class GetTeacherScheduleQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetTeacherScheduleQuery, IReadOnlyList<ScheduleSlotDto>>
{
    public async Task<IReadOnlyList<ScheduleSlotDto>> Handle(
        GetTeacherScheduleQuery query,
        CancellationToken cancellationToken)
    {
        // Own schedule when no id is supplied; another teacher's public schedule otherwise.
        var teacherId = query.TeacherId ?? currentUser.RequireTeacherId();
        var isSelf = currentUser.TeacherId == teacherId || currentUser.IsAdmin;
        var includeUnpublished = query.IncludeUnpublished && isSelf;

        var slots = await context.GroupSchedules
            .AsNoTracking()
            .Where(schedule => schedule.Group.TeacherId == teacherId)
            .WhereIf(!includeUnpublished, schedule =>
                schedule.IsActive && schedule.Group.IsPublished && schedule.Group.IsActive)
            .OrderBy(schedule => schedule.DayOfWeek)
            .ThenBy(schedule => schedule.StartTime)
            .Select(ScheduleProjections.ToSlot)
            .ToListAsync(cancellationToken);

        return ScheduleProjections.WithNextOccurrences(slots, dateTimeProvider.UtcNow);
    }
}

/// <summary>The weekly timetable assembled from the calling student's active enrollments.</summary>
public sealed record GetStudentScheduleQuery : IRequest<IReadOnlyList<ScheduleSlotDto>>;

public sealed class GetStudentScheduleQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetStudentScheduleQuery, IReadOnlyList<ScheduleSlotDto>>
{
    public async Task<IReadOnlyList<ScheduleSlotDto>> Handle(
        GetStudentScheduleQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        var slots = await context.GroupSchedules
            .AsNoTracking()
            .Where(schedule => schedule.IsActive
                && schedule.Group.IsActive
                && schedule.Group.Enrollments.Any(enrollment =>
                    enrollment.StudentId == studentId && enrollment.Status == EnrollmentStatus.Active))
            .OrderBy(schedule => schedule.DayOfWeek)
            .ThenBy(schedule => schedule.StartTime)
            .Select(ScheduleProjections.ToSlot)
            .ToListAsync(cancellationToken);

        return ScheduleProjections.WithNextOccurrences(slots, dateTimeProvider.UtcNow);
    }
}

/// <summary>The next sessions for the caller (student timetable, or teacher timetable).</summary>
public sealed record GetUpcomingClassesQuery : IRequest<IReadOnlyList<ScheduleSlotDto>>
{
    public int Take { get; init; } = 5;
}

public sealed class GetUpcomingClassesQueryHandler(
    IMediator mediator,
    ICurrentUserService currentUser) : IRequestHandler<GetUpcomingClassesQuery, IReadOnlyList<ScheduleSlotDto>>
{
    public async Task<IReadOnlyList<ScheduleSlotDto>> Handle(
        GetUpcomingClassesQuery query,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(query.Take, 1, 50);

        IReadOnlyList<ScheduleSlotDto> slots = currentUser.StudentId is not null
            ? await mediator.Send(new GetStudentScheduleQuery(), cancellationToken)
            : currentUser.TeacherId is not null
                ? await mediator.Send(new GetTeacherScheduleQuery(), cancellationToken)
                : await mediator.Send(new GetPublicScheduleQuery(), cancellationToken);

        return [.. slots.Take(take)];
    }
}
