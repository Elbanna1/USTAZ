using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Application.Features.Teachers.Queries;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Teachers.Commands;

/// <summary>
/// Teacher action: ask the review team to verify the profile. This is the entry point the
/// verification workflow previously lacked — registration leaves a teacher unsubmitted, and
/// a rejected teacher can fix their profile and come back through here.
/// </summary>
public sealed record SubmitTeacherVerificationCommand : IRequest<TeacherVerificationDto>;

public sealed class SubmitTeacherVerificationCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    ICacheService cache,
    IMediator mediator) : IRequestHandler<SubmitTeacherVerificationCommand, TeacherVerificationDto>
{
    public async Task<TeacherVerificationDto> Handle(
        SubmitTeacherVerificationCommand command,
        CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();

        // Subjects and grades are part of the readiness rule, so they must be loaded for the
        // entity to judge itself rather than trusting a count passed in from outside.
        var teacher = await context.TeacherProfiles
            .Include(profile => profile.TeacherSubjects)
            .Include(profile => profile.TeacherGrades)
            .FirstOrDefaultAsync(profile => profile.Id == teacherId, cancellationToken)
            ?? throw new NotFoundException("Teacher", teacherId);

        // The entity owns the rule: it decides whether this transition is legal and throws a
        // coded business-rule error the client already knows how to present.
        teacher.SubmitForVerification(dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        await notifications.SendAsync(
            teacher.UserId,
            new NotificationRequest
            {
                Event = NotificationEvents.TeacherVerificationSubmitted,
                TenantId = teacher.TenantId,
                ReferenceId = teacher.Id
            },
            cancellationToken);

        await cache.RemoveAsync(CacheKeys.TeacherProfile(teacher.Id), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);

        return await mediator.Send(new GetTeacherVerificationQuery(), cancellationToken);
    }
}
