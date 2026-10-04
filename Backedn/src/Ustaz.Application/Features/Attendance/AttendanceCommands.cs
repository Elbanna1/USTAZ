using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Features.Attendance.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Attendance;

/// <summary>Shared loading and notification logic for the attendance handlers.</summary>
internal static class AttendanceHelper
{
    /// <summary>Loads a session, proving it belongs to the caller's workspace first.</summary>
    public static async Task<AttendanceSession> LoadSessionAsync(
        IApplicationDbContext context,
        ITenantAuthorizationService tenantAuthorization,
        TenantAccess access,
        Guid sessionId,
        bool includeRecords,
        CancellationToken cancellationToken)
    {
        var query = context.AttendanceSessions.AsQueryable();

        if (includeRecords)
        {
            query = query.Include(session => session.Records);
        }

        var session = await query.FirstOrDefaultAsync(session => session.Id == sessionId, cancellationToken)
            ?? throw new NotFoundException("AttendanceSession", sessionId);

        tenantAuthorization.EnsureSameTenant(access, session.TenantId);

        return session;
    }

    /// <summary>
    /// Tells the student and their confirmed guardians that an absence (or lateness) was
    /// recorded, honouring each recipient's notification preferences. The record carries a
    /// flag so a re-save never sends the same alert twice.
    /// </summary>
    public static async Task NotifyAbsenceAsync(
        IApplicationDbContext context,
        INotificationDispatcher notifications,
        IDateTimeProvider dateTimeProvider,
        AttendanceSession session,
        IReadOnlyCollection<AttendanceRecord> records,
        CancellationToken cancellationToken)
    {
        var pending = records
            .Where(record => record.CountsAsMissed && !record.ParentNotified)
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        var settings = await context.TenantSettings
            .AsNoTracking()
            .Where(entry => entry.TenantId == session.TenantId)
            .Select(entry => new { entry.NotifyParentsOnAbsence })
            .FirstOrDefaultAsync(cancellationToken);

        var notifyParents = settings?.NotifyParentsOnAbsence ?? true;

        var groupName = await context.Groups
            .AsNoTracking()
            .Where(group => group.Id == session.GroupId)
            .Select(group => group.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "المجموعة";

        var studentIds = pending.Select(record => record.StudentId).Distinct().ToList();

        var students = await context.StudentProfiles
            .AsNoTracking()
            .Where(student => studentIds.Contains(student.Id))
            .Select(student => new { student.Id, student.UserId, student.User.DisplayName })
            .ToListAsync(cancellationToken);

        var guardians = notifyParents
            ? await context.ParentStudentLinks
                .AsNoTracking()
                .Where(link => studentIds.Contains(link.StudentId) && link.IsConfirmed)
                .Select(link => new { link.StudentId, link.Parent.UserId })
                .ToListAsync(cancellationToken)
            : [];

        var utcNow = dateTimeProvider.UtcNow;

        foreach (var record in pending)
        {
            var student = students.FirstOrDefault(entry => entry.Id == record.StudentId);
            if (student is null)
            {
                continue;
            }

            var studentEvent = record.Status == AttendanceStatus.Absent
                ? NotificationEvents.AttendanceAbsent
                : NotificationEvents.AttendanceLate;

            // One alert per student per session, however many times the register is saved.
            await notifications.SendAsync(
                student.UserId,
                new NotificationRequest
                {
                    Event = studentEvent,
                    TenantId = session.TenantId,
                    ReferenceId = record.Id,
                    DeduplicationKey = studentEvent + ":" + session.Id,
                    Values = new Dictionary<string, string> { ["group"] = groupName }
                },
                cancellationToken);

            foreach (var guardian in guardians.Where(entry => entry.StudentId == record.StudentId))
            {
                await notifications.SendAsync(
                    guardian.UserId,
                    new NotificationRequest
                    {
                        Event = NotificationEvents.ChildAbsent,
                        TenantId = session.TenantId,
                        ReferenceId = record.Id,
                        DeduplicationKey = "child-absence:" + session.Id + ":" + record.StudentId,
                        Values = new Dictionary<string, string>
                        {
                            ["student"] = student.DisplayName,
                            ["studentId"] = record.StudentId.ToString(),
                            ["group"] = groupName
                        }
                    },
                    cancellationToken);
            }

            record.ParentNotified = true;
            record.ParentNotifiedAt = utcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Create a session
// ---------------------------------------------------------------------------
public sealed record CreateAttendanceSessionCommand : IRequest<AttendanceSessionDto>
{
    public Guid GroupId { get; init; }

    public Guid? GroupScheduleId { get; init; }

    public DateTime SessionDate { get; init; }

    public TimeOnly? StartTime { get; init; }

    public TimeOnly? EndTime { get; init; }

    public string? Title { get; init; }

    public string? Notes { get; init; }

    /// <summary>Pre-fills a Present record for every active student in the group.</summary>
    public bool PrefillPresent { get; init; } = true;
}

public sealed class CreateAttendanceSessionCommandValidator : AbstractValidator<CreateAttendanceSessionCommand>
{
    public CreateAttendanceSessionCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.SessionDate).NotEmpty();
        RuleFor(command => command.Title).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Notes).MaximumLength(DomainRules.ShortTextMaxLength);

        RuleFor(command => command.EndTime!.Value)
            .GreaterThan(command => command.StartTime!.Value)
            .When(command => command.StartTime.HasValue && command.EndTime.HasValue)
            .WithMessage("The session end time must be after its start time.");
    }
}

public sealed class CreateAttendanceSessionCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    IMediator mediator) : IRequestHandler<CreateAttendanceSessionCommand, AttendanceSessionDto>
{
    public async Task<AttendanceSessionDto> Handle(
        CreateAttendanceSessionCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AttendanceManage, cancellationToken: cancellationToken);

        await planLimits.EnsureFeatureAsync(access.TenantId, PlanFeatures.Attendance, cancellationToken);

        // The group must belong to this workspace: this is what blocks cross-tenant writes.
        var group = await context.Groups
            .AsNoTracking()
            .Where(entity => entity.Id == command.GroupId)
            .Select(entity => new { entity.Id, entity.TenantId, entity.Name })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Group", command.GroupId);

        tenantAuthorization.EnsureSameTenant(access, group.TenantId);

        var sessionDate = DateTime.SpecifyKind(command.SessionDate.Date, DateTimeKind.Utc);

        var duplicate = await context.AttendanceSessions.AnyAsync(
            session => session.GroupId == group.Id && session.SessionDate == sessionDate,
            cancellationToken);

        if (duplicate)
        {
            throw new ConflictException("An attendance register already exists for this group on that date.");
        }

        var utcNow = dateTimeProvider.UtcNow;

        var entity = new AttendanceSession
        {
            Id = Guid.NewGuid(),
            TenantId = access.TenantId,
            GroupId = group.Id,
            GroupScheduleId = command.GroupScheduleId,
            SessionDate = sessionDate,
            StartTime = command.StartTime,
            EndTime = command.EndTime,
            Title = command.Title?.Trim(),
            Notes = command.Notes?.Trim(),
            Status = AttendanceSessionStatus.Open,
            RecordedByUserId = access.UserId,
            CreatedAt = utcNow
        };

        if (command.PrefillPresent)
        {
            var studentIds = await context.GroupEnrollments
                .AsNoTracking()
                .Where(enrollment => enrollment.GroupId == group.Id && enrollment.Status == EnrollmentStatus.Active)
                .Select(enrollment => enrollment.StudentId)
                .ToListAsync(cancellationToken);

            foreach (var studentId in studentIds)
            {
                entity.Records.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = access.TenantId,
                    AttendanceSessionId = entity.Id,
                    StudentId = studentId,
                    Status = AttendanceStatus.Present,
                    RecordedByUserId = access.UserId,
                    RecordedAt = utcNow,
                    CreatedAt = utcNow
                });
            }
        }

        await context.AttendanceSessions.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return await mediator.Send(new GetAttendanceSessionQuery(entity.Id), cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Update a session
// ---------------------------------------------------------------------------
public sealed record UpdateAttendanceSessionCommand : IRequest<AttendanceSessionDto>
{
    public Guid SessionId { get; init; }

    public string? Title { get; init; }

    public string? Notes { get; init; }

    public TimeOnly? StartTime { get; init; }

    public TimeOnly? EndTime { get; init; }

    public AttendanceSessionStatus? Status { get; init; }
}

public sealed class UpdateAttendanceSessionCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IMediator mediator) : IRequestHandler<UpdateAttendanceSessionCommand, AttendanceSessionDto>
{
    public async Task<AttendanceSessionDto> Handle(
        UpdateAttendanceSessionCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AttendanceManage, cancellationToken: cancellationToken);

        var session = await AttendanceHelper.LoadSessionAsync(
            context, tenantAuthorization, access, command.SessionId, includeRecords: false, cancellationToken);

        if (command.Title is not null)
        {
            session.Title = command.Title.Trim();
        }

        if (command.Notes is not null)
        {
            session.Notes = command.Notes.Trim();
        }

        if (command.StartTime.HasValue)
        {
            session.StartTime = command.StartTime;
        }

        if (command.EndTime.HasValue)
        {
            session.EndTime = command.EndTime;
        }

        if (command.Status.HasValue)
        {
            if (command.Status.Value == AttendanceSessionStatus.Closed)
            {
                session.Close(dateTimeProvider.UtcNow);
            }
            else
            {
                session.Status = command.Status.Value;
                session.ClosedAt = null;
            }
        }

        session.UpdatedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return await mediator.Send(new GetAttendanceSessionQuery(session.Id), cancellationToken);
    }
}
