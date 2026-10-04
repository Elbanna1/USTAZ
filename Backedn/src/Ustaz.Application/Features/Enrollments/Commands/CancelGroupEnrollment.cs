using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Events;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Enrollments.Commands;

/// <summary>
/// Cancels a group enrollment. The student who owns it, the owning teacher and admins may do this.
/// </summary>
public sealed record CancelGroupEnrollmentCommand : IRequest<Unit>
{
    public Guid EnrollmentId { get; init; }

    public string? Reason { get; init; }
}

public sealed class CancelGroupEnrollmentCommandValidator : AbstractValidator<CancelGroupEnrollmentCommand>
{
    public CancelGroupEnrollmentCommandValidator()
    {
        RuleFor(command => command.EnrollmentId).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(DomainRules.ShortTextMaxLength);
    }
}

public sealed class CancelGroupEnrollmentCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    ICacheService cache) : IRequestHandler<CancelGroupEnrollmentCommand, Unit>
{
    public async Task<Unit> Handle(CancelGroupEnrollmentCommand command, CancellationToken cancellationToken)
    {
        var enrollment = await context.GroupEnrollments
            .Include(entity => entity.Group)
            .FirstOrDefaultAsync(entity => entity.Id == command.EnrollmentId, cancellationToken)
            ?? throw new NotFoundException("GroupEnrollment", command.EnrollmentId);

        var isStudentOwner = currentUser.StudentId is not null && currentUser.StudentId == enrollment.StudentId;
        var isTeacherOwner = currentUser.TeacherId is not null && currentUser.TeacherId == enrollment.Group.TeacherId;

        if (!isStudentOwner && !isTeacherOwner && !currentUser.IsAdmin)
        {
            throw new ForbiddenAccessException("You can only cancel your own enrollments.");
        }

        var utcNow = dateTimeProvider.UtcNow;
        var wasOccupyingSeat = enrollment.IsOccupyingSeat;

        enrollment.Cancel(utcNow, command.Reason?.Trim());

        if (wasOccupyingSeat)
        {
            enrollment.Group.DecrementEnrollment(utcNow);

            var teacher = await context.TeacherProfiles
                .FirstAsync(profile => profile.Id == enrollment.Group.TeacherId, cancellationToken);

            teacher.StudentsCount = Math.Max(0, teacher.StudentsCount - 1);
            teacher.UpdatedAt = utcNow;
        }

        enrollment.RaiseDomainEvent(new GroupEnrollmentCancelledEvent(
            enrollment.Id, enrollment.GroupId, enrollment.StudentId, enrollment.Group.TeacherId));

        await context.SaveChangesAsync(cancellationToken);

        // Tell the other party, whoever initiated the cancellation.
        if (isStudentOwner)
        {
            var teacherUserId = await context.TeacherProfiles
                .Where(profile => profile.Id == enrollment.Group.TeacherId)
                .Select(profile => profile.UserId)
                .FirstAsync(cancellationToken);

            var studentName = await context.StudentProfiles
                .AsNoTracking()
                .Where(profile => profile.Id == enrollment.StudentId)
                .Select(profile => profile.User.DisplayName)
                .FirstOrDefaultAsync(cancellationToken) ?? "طالب";

            await notifications.SendAsync(
                teacherUserId,
                new NotificationRequest
                {
                    Event = NotificationEvents.GroupEnrollmentCancelled,
                    TenantId = enrollment.Group.TenantId,
                    ReferenceId = enrollment.Id,
                    ActionUrl = "/teacher/groups/" + enrollment.GroupId,
                    Values = new Dictionary<string, string>
                    {
                        ["group"] = enrollment.Group.Name,
                        ["student"] = studentName
                    }
                },
                cancellationToken);
        }
        else
        {
            var studentUserId = await context.StudentProfiles
                .Where(profile => profile.Id == enrollment.StudentId)
                .Select(profile => profile.UserId)
                .FirstAsync(cancellationToken);

            await notifications.SendAsync(
                studentUserId,
                new NotificationRequest
                {
                    Event = NotificationEvents.GroupEnrollmentCancelled,
                    TenantId = enrollment.Group.TenantId,
                    ReferenceId = enrollment.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["group"] = enrollment.Group.Name
                    }
                },
                cancellationToken);
        }

        await cache.RemoveAsync(CacheKeys.GroupDetails(enrollment.GroupId), cancellationToken);
        await cache.RemoveAsync(CacheKeys.TeacherDashboard(enrollment.Group.TeacherId), cancellationToken);
        await cache.RemoveAsync(CacheKeys.StudentDashboard(enrollment.StudentId), cancellationToken);

        return Unit.Value;
    }
}
