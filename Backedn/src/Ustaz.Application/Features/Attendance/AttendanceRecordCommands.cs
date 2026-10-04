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

public sealed record AttendanceEntryInput
{
    public Guid StudentId { get; init; }

    public AttendanceStatus Status { get; init; } = AttendanceStatus.Present;

    public int? MinutesLate { get; init; }

    public string? Note { get; init; }
}

/// <summary>
/// Records attendance for a whole session in one call. Existing rows are updated rather than
/// duplicated, so the teacher can correct the register safely.
/// </summary>
public sealed record RecordAttendanceCommand : IRequest<AttendanceSessionDto>
{
    public Guid SessionId { get; init; }

    public IReadOnlyList<AttendanceEntryInput> Entries { get; init; } = [];
}

public sealed class RecordAttendanceCommandValidator : AbstractValidator<RecordAttendanceCommand>
{
    public RecordAttendanceCommandValidator()
    {
        RuleFor(command => command.SessionId).NotEmpty();

        RuleFor(command => command.Entries)
            .NotEmpty().WithMessage("Provide at least one attendance entry.")
            .Must(entries => entries.Select(entry => entry.StudentId).Distinct().Count() == entries.Count)
            .WithMessage("Each student may appear only once.");

        RuleForEach(command => command.Entries).ChildRules(entry =>
        {
            entry.RuleFor(item => item.StudentId).NotEmpty();
            entry.RuleFor(item => item.Status).IsInEnum();
            entry.RuleFor(item => item.Note).MaximumLength(DomainRules.ShortTextMaxLength);
            entry.RuleFor(item => item.MinutesLate!.Value)
                .InclusiveBetween(0, 600)
                .When(item => item.MinutesLate.HasValue);
        });
    }
}

public sealed class RecordAttendanceCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    IAuditLogger auditLogger,
    IMediator mediator) : IRequestHandler<RecordAttendanceCommand, AttendanceSessionDto>
{
    public async Task<AttendanceSessionDto> Handle(
        RecordAttendanceCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AttendanceManage, cancellationToken: cancellationToken);

        await planLimits.EnsureFeatureAsync(access.TenantId, PlanFeatures.Attendance, cancellationToken);

        var session = await AttendanceHelper.LoadSessionAsync(
            context, tenantAuthorization, access, command.SessionId, includeRecords: true, cancellationToken);

        session.EnsureOpen();

        // Only students actually enrolled in this group may be marked.
        var enrolledIds = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.GroupId == session.GroupId
                && enrollment.Status != EnrollmentStatus.Cancelled)
            .Select(enrollment => enrollment.StudentId)
            .ToListAsync(cancellationToken);

        var stranger = command.Entries.FirstOrDefault(entry => !enrolledIds.Contains(entry.StudentId));
        if (stranger is not null)
        {
            throw new BusinessRuleException(
                "STUDENT_NOT_IN_GROUP",
                "One of the students is not enrolled in this group.");
        }

        var utcNow = dateTimeProvider.UtcNow;
        var touched = new List<AttendanceRecord>();

        foreach (var entry in command.Entries)
        {
            var record = session.Records.FirstOrDefault(item => item.StudentId == entry.StudentId);

            if (record is null)
            {
                record = new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = session.TenantId,
                    AttendanceSessionId = session.Id,
                    StudentId = entry.StudentId,
                    CreatedAt = utcNow
                };

                session.Records.Add(record);
            }
            else if (record.Status != entry.Status)
            {
                // A corrected status re-opens the notification, e.g. Absent -> Excused -> Absent.
                record.ParentNotified = false;
            }

            record.Status = entry.Status;
            record.MinutesLate = entry.Status == AttendanceStatus.Late ? entry.MinutesLate : null;
            record.Note = entry.Note?.Trim();
            record.RecordedByUserId = access.UserId;
            record.RecordedAt = utcNow;
            record.UpdatedAt = utcNow;

            touched.Add(record);
        }

        session.UpdatedAt = utcNow;
        await context.SaveChangesAsync(cancellationToken);

        await AttendanceHelper.NotifyAbsenceAsync(
            context, notifications, dateTimeProvider, session, touched, cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.AttendanceRecorded,
            nameof(AttendanceSession),
            session.Id,
            newValues: new { Entries = command.Entries.Count },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return await mediator.Send(new GetAttendanceSessionQuery(session.Id), cancellationToken);
    }
}

/// <summary>Corrects a single attendance row.</summary>
public sealed record UpdateAttendanceRecordCommand : IRequest<AttendanceRecordDto>
{
    public Guid RecordId { get; init; }

    public AttendanceStatus Status { get; init; }

    public int? MinutesLate { get; init; }

    public string? Note { get; init; }
}

public sealed class UpdateAttendanceRecordCommandValidator : AbstractValidator<UpdateAttendanceRecordCommand>
{
    public UpdateAttendanceRecordCommandValidator()
    {
        RuleFor(command => command.RecordId).NotEmpty();
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.Note).MaximumLength(DomainRules.ShortTextMaxLength);
    }
}

public sealed class UpdateAttendanceRecordCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    IAuditLogger auditLogger) : IRequestHandler<UpdateAttendanceRecordCommand, AttendanceRecordDto>
{
    public async Task<AttendanceRecordDto> Handle(
        UpdateAttendanceRecordCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AttendanceManage, cancellationToken: cancellationToken);

        var record = await context.AttendanceRecords
            .Include(entity => entity.Session)
            .FirstOrDefaultAsync(entity => entity.Id == command.RecordId, cancellationToken)
            ?? throw new NotFoundException("AttendanceRecord", command.RecordId);

        tenantAuthorization.EnsureSameTenant(access, record.TenantId);
        record.Session.EnsureOpen();

        var previous = record.Status;
        var utcNow = dateTimeProvider.UtcNow;

        if (previous != command.Status)
        {
            record.ParentNotified = false;
        }

        record.Status = command.Status;
        record.MinutesLate = command.Status == AttendanceStatus.Late ? command.MinutesLate : null;
        record.Note = command.Note?.Trim();
        record.RecordedByUserId = access.UserId;
        record.UpdatedAt = utcNow;

        await context.SaveChangesAsync(cancellationToken);

        await AttendanceHelper.NotifyAbsenceAsync(
            context, notifications, dateTimeProvider, record.Session, [record], cancellationToken);

        // A correction back to "present" is good news the student should see, and the
        // absence notification alone would leave them thinking they are still marked down.
        if (previous != command.Status && command.Status == AttendanceStatus.Present)
        {
            var studentUserId = await context.StudentProfiles
                .AsNoTracking()
                .Where(profile => profile.Id == record.StudentId)
                .Select(profile => profile.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            var groupName = await context.Groups
                .AsNoTracking()
                .Where(group => group.Id == record.Session.GroupId)
                .Select(group => group.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "الحصة";

            if (studentUserId != Guid.Empty)
            {
                await notifications.SendAsync(
                    studentUserId,
                    new NotificationRequest
                    {
                        Event = NotificationEvents.AttendanceUpdated,
                        TenantId = record.TenantId,
                        ReferenceId = record.Id,
                        Values = new Dictionary<string, string>
                        {
                            ["group"] = groupName,
                            ["status"] = "حاضر"
                        }
                    },
                    cancellationToken);
            }
        }

        await auditLogger.LogAsync(
            AuditActions.AttendanceUpdated,
            nameof(AttendanceRecord),
            record.Id,
            new { Status = previous },
            new { record.Status },
            access.TenantId,
            cancellationToken: cancellationToken);

        return await context.AttendanceRecords
            .AsNoTracking()
            .Where(entity => entity.Id == record.Id)
            .Select(AttendanceProjections.ToRecordDto)
            .FirstAsync(cancellationToken);
    }
}
