using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Features.Attendance.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Attendance;

internal static class AttendanceProjections
{
    public static Expression<Func<AttendanceRecord, AttendanceRecordDto>> ToRecordDto =>
        record => new AttendanceRecordDto
        {
            Id = record.Id,
            AttendanceSessionId = record.AttendanceSessionId,
            StudentId = record.StudentId,
            StudentName = record.Student.User.DisplayName,
            StudentImageUrl = record.Student.User.ProfileImageUrl,
            Status = record.Status,
            MinutesLate = record.MinutesLate,
            Note = record.Note,
            RecordedAt = record.RecordedAt,
            GroupId = record.Session.GroupId,
            GroupName = record.Session.Group.Name,
            SessionDate = record.Session.SessionDate
        };

    public static Expression<Func<AttendanceSession, AttendanceSessionDto>> ToSessionDto =>
        session => new AttendanceSessionDto
        {
            Id = session.Id,
            TenantId = session.TenantId,
            GroupId = session.GroupId,
            GroupName = session.Group.Name,
            GroupScheduleId = session.GroupScheduleId,
            SessionDate = session.SessionDate,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Title = session.Title,
            Notes = session.Notes,
            Status = session.Status,
            CreatedAt = session.CreatedAt,
            TotalStudents = session.Records.Count,
            PresentCount = session.Records.Count(record => record.Status == AttendanceStatus.Present),
            AbsentCount = session.Records.Count(record => record.Status == AttendanceStatus.Absent),
            LateCount = session.Records.Count(record => record.Status == AttendanceStatus.Late),
            ExcusedCount = session.Records.Count(record => record.Status == AttendanceStatus.Excused)
        };
}

public sealed record GetAttendanceSessionQuery(Guid SessionId) : IRequest<AttendanceSessionDto>;

public sealed class GetAttendanceSessionQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetAttendanceSessionQuery, AttendanceSessionDto>
{
    public async Task<AttendanceSessionDto> Handle(
        GetAttendanceSessionQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AttendanceView, cancellationToken: cancellationToken);

        var session = await context.AttendanceSessions
            .AsNoTracking()
            .Where(entity => entity.Id == query.SessionId)
            .Select(AttendanceProjections.ToSessionDto)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("AttendanceSession", query.SessionId);

        tenantAuthorization.EnsureSameTenant(access, session.TenantId);

        var records = await context.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.AttendanceSessionId == query.SessionId)
            .OrderBy(record => record.Student.User.DisplayName)
            .Select(AttendanceProjections.ToRecordDto)
            .ToListAsync(cancellationToken);

        return session with { Records = records };
    }
}

public sealed record GetAttendanceSessionsQuery : PagedRequest, IRequest<PaginatedList<AttendanceSessionDto>>
{
    public Guid? GroupId { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }

    public AttendanceSessionStatus? Status { get; init; }
}

public sealed class GetAttendanceSessionsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetAttendanceSessionsQuery, PaginatedList<AttendanceSessionDto>>
{
    public async Task<PaginatedList<AttendanceSessionDto>> Handle(
        GetAttendanceSessionsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AttendanceView, cancellationToken: cancellationToken);

        return await context.AttendanceSessions
            .AsNoTracking()
            .Where(session => session.TenantId == access.TenantId)
            .WhereIf(query.GroupId.HasValue, session => session.GroupId == query.GroupId!.Value)
            .WhereIf(query.From.HasValue, session => session.SessionDate >= query.From!.Value)
            .WhereIf(query.To.HasValue, session => session.SessionDate <= query.To!.Value)
            .WhereIf(query.Status.HasValue, session => session.Status == query.Status!.Value)
            .OrderByDescending(session => session.SessionDate)
            .Select(AttendanceProjections.ToSessionDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>A student's attendance history, as the teacher sees it.</summary>
public sealed record GetStudentAttendanceQuery : PagedRequest, IRequest<PaginatedList<AttendanceRecordDto>>
{
    public Guid StudentId { get; init; }

    public Guid? GroupId { get; init; }

    public AttendanceStatus? Status { get; init; }
}

public sealed class GetStudentAttendanceQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetStudentAttendanceQuery, PaginatedList<AttendanceRecordDto>>
{
    public async Task<PaginatedList<AttendanceRecordDto>> Handle(
        GetStudentAttendanceQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AttendanceView, cancellationToken: cancellationToken);

        return await context.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.TenantId == access.TenantId && record.StudentId == query.StudentId)
            .WhereIf(query.GroupId.HasValue, record => record.Session.GroupId == query.GroupId!.Value)
            .WhereIf(query.Status.HasValue, record => record.Status == query.Status!.Value)
            .OrderByDescending(record => record.Session.SessionDate)
            .Select(AttendanceProjections.ToRecordDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>Attendance for the signed-in student, across every workspace they study in.</summary>
public sealed record GetMyAttendanceQuery : PagedRequest, IRequest<PaginatedList<AttendanceRecordDto>>
{
    public Guid? GroupId { get; init; }
}

public sealed class GetMyAttendanceQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetMyAttendanceQuery, PaginatedList<AttendanceRecordDto>>
{
    public Task<PaginatedList<AttendanceRecordDto>> Handle(
        GetMyAttendanceQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        return context.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.StudentId == studentId)
            .WhereIf(query.GroupId.HasValue, record => record.Session.GroupId == query.GroupId!.Value)
            .OrderByDescending(record => record.Session.SessionDate)
            .Select(AttendanceProjections.ToRecordDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>Attendance for one of the calling parent's children.</summary>
public sealed record GetChildAttendanceQuery : PagedRequest, IRequest<PaginatedList<AttendanceRecordDto>>
{
    public Guid StudentId { get; init; }
}

public sealed class GetChildAttendanceQueryHandler(
    IApplicationDbContext context,
    IParentAccessGuard parentAccess) : IRequestHandler<GetChildAttendanceQuery, PaginatedList<AttendanceRecordDto>>
{
    public async Task<PaginatedList<AttendanceRecordDto>> Handle(
        GetChildAttendanceQuery query,
        CancellationToken cancellationToken)
    {
        // Throws a 404 when the student is not a confirmed child of this guardian.
        await parentAccess.EnsureLinkedAsync(query.StudentId, cancellationToken);

        return await context.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.StudentId == query.StudentId)
            .OrderByDescending(record => record.Session.SessionDate)
            .Select(AttendanceProjections.ToRecordDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>Attendance statistics for the workspace, one group, or one student.</summary>
public sealed record GetAttendanceStatisticsQuery : IRequest<AttendanceStatisticsDto>
{
    public Guid? GroupId { get; init; }

    public Guid? StudentId { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}

public sealed class GetAttendanceStatisticsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetAttendanceStatisticsQuery, AttendanceStatisticsDto>
{
    public async Task<AttendanceStatisticsDto> Handle(
        GetAttendanceStatisticsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AttendanceView, cancellationToken: cancellationToken);

        var records = context.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.TenantId == access.TenantId)
            .WhereIf(query.GroupId.HasValue, record => record.Session.GroupId == query.GroupId!.Value)
            .WhereIf(query.StudentId.HasValue, record => record.StudentId == query.StudentId!.Value)
            .WhereIf(query.From.HasValue, record => record.Session.SessionDate >= query.From!.Value)
            .WhereIf(query.To.HasValue, record => record.Session.SessionDate <= query.To!.Value);

        var totals = await records
            .GroupBy(record => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Present = group.Count(record => record.Status == AttendanceStatus.Present),
                Absent = group.Count(record => record.Status == AttendanceStatus.Absent),
                Late = group.Count(record => record.Status == AttendanceStatus.Late),
                Excused = group.Count(record => record.Status == AttendanceStatus.Excused)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var byGroup = await records
            .GroupBy(record => new { record.Session.GroupId, record.Session.Group.Name })
            .Select(group => new AttendanceBreakdownDto
            {
                GroupId = group.Key.GroupId,
                GroupName = group.Key.Name,
                TotalSessions = group.Count(),
                Present = group.Count(record => record.Status == AttendanceStatus.Present),
                Absent = group.Count(record => record.Status == AttendanceStatus.Absent)
            })
            .ToListAsync(cancellationToken);

        var total = totals?.Total ?? 0;

        return new AttendanceStatisticsDto
        {
            TotalSessions = total,
            Present = totals?.Present ?? 0,
            Absent = totals?.Absent ?? 0,
            Late = totals?.Late ?? 0,
            Excused = totals?.Excused ?? 0,
            AttendanceRate = Rate((totals?.Present ?? 0) + (totals?.Late ?? 0), total),
            AbsenceRate = Rate(totals?.Absent ?? 0, total),
            ByGroup = [.. byGroup.Select(entry => entry with
            {
                AttendanceRate = Rate(entry.Present, entry.TotalSessions)
            })]
        };
    }

    private static decimal Rate(int part, int total) =>
        total == 0 ? 0m : Math.Round((decimal)part / total * 100m, 2, MidpointRounding.AwayFromZero);
}
