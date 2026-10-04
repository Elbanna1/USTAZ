using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Features.Enrollments.Dtos;
using Ustaz.Application.Features.Enrollments.Queries;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Events;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Enrollments.Commands;

/// <summary>
/// Enrolls a student into one of the workspace's groups on their behalf. Centres register
/// walk-in students at the desk, so the platform cannot assume every enrollment starts with
/// the learner tapping "join".
/// </summary>
public sealed record EnrollStudentInGroupCommand : IRequest<GroupEnrollmentDto>
{
    public Guid GroupId { get; init; }

    public Guid StudentId { get; init; }

    /// <summary>Agreed monthly fee when it differs from the group's list price.</summary>
    public decimal? MonthlyPrice { get; init; }

    public string? Notes { get; init; }
}

public sealed class EnrollStudentInGroupCommandValidator : AbstractValidator<EnrollStudentInGroupCommand>
{
    public EnrollStudentInGroupCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.StudentId).NotEmpty();
        RuleFor(command => command.Notes).MaximumLength(DomainRules.ShortTextMaxLength);

        RuleFor(command => command.MonthlyPrice!.Value)
            .InclusiveBetween(DomainRules.MinPrice, DomainRules.MaxPrice)
            .When(command => command.MonthlyPrice.HasValue);
    }
}

public sealed class EnrollStudentInGroupCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    ITenantProvisioningService tenantProvisioning,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    ICacheService cache,
    IAuditLogger auditLogger) : IRequestHandler<EnrollStudentInGroupCommand, GroupEnrollmentDto>
{
    public async Task<GroupEnrollmentDto> Handle(
        EnrollStudentInGroupCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.StudentsManage, cancellationToken: cancellationToken);

        var group = await context.Groups
            .FirstOrDefaultAsync(entity => entity.Id == command.GroupId, cancellationToken)
            ?? throw new NotFoundException("Group", command.GroupId);

        // The group has to belong to the caller's workspace: this is what stops a member of
        // one centre from filling another centre's group.
        tenantAuthorization.EnsureSameTenant(access, group.TenantId);

        var student = await context.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == command.StudentId)
            .Select(profile => new { profile.Id, profile.UserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("StudentProfile", command.StudentId);

        var alreadyEnrolled = await context.GroupEnrollments.AnyAsync(
            enrollment => enrollment.GroupId == group.Id
                && enrollment.StudentId == student.Id
                && (enrollment.Status == EnrollmentStatus.Active || enrollment.Status == EnrollmentStatus.Pending),
            cancellationToken);

        if (alreadyEnrolled)
        {
            throw new ConflictException("This student is already enrolled in this group.");
        }

        var utcNow = dateTimeProvider.UtcNow;

        // Registers the learner against the workspace and charges the plan's student quota.
        await tenantProvisioning.EnsureStudentBelongsToTenantAsync(group.TenantId, student.Id, cancellationToken);

        var price = command.MonthlyPrice ?? group.MonthlyPrice;

        // Staff may fill a group that is still a draft; the seat count is enforced either way.
        group.IncrementEnrollment(utcNow, requirePublished: false);

        var enrollment = new GroupEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            GroupId = group.Id,
            Status = EnrollmentStatus.Active,
            EnrolledAt = utcNow,
            PricePaid = price,
            DiscountAmount = 0m,
            FinalPrice = price,
            CreatedAt = utcNow
        };

        var teacher = await context.TeacherProfiles
            .FirstAsync(profile => profile.Id == group.TeacherId, cancellationToken);

        teacher.StudentsCount++;
        teacher.UpdatedAt = utcNow;

        enrollment.RaiseDomainEvent(new StudentEnrolledInGroupEvent(
            enrollment.Id, group.Id, student.Id, group.TeacherId, group.Name));

        await context.GroupEnrollments.AddAsync(enrollment, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await notifications.SendAsync(
            student.UserId,
            new NotificationRequest
            {
                Event = NotificationEvents.StudentAddedToGroup,
                TenantId = group.TenantId,
                ReferenceId = enrollment.Id,
                Values = new Dictionary<string, string> { ["group"] = group.Name }
            },
            cancellationToken);

        // Guardians follow their child's enrollments too.
        var guardianUserIds = await context.ParentStudentLinks
            .AsNoTracking()
            .Where(link => link.StudentId == student.Id && link.IsConfirmed)
            .Select(link => link.Parent.UserId)
            .ToListAsync(cancellationToken);

        if (guardianUserIds.Count > 0)
        {
            var studentName = await context.Users
                .AsNoTracking()
                .Where(user => user.Id == student.UserId)
                .Select(user => user.DisplayName)
                .FirstAsync(cancellationToken);

            await notifications.SendManyAsync(
                guardianUserIds,
                new NotificationRequest
                {
                    Event = NotificationEvents.ChildEnrolled,
                    TenantId = group.TenantId,
                    ReferenceId = enrollment.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["student"] = studentName,
                        ["studentId"] = student.Id.ToString(),
                        ["group"] = group.Name
                    }
                },
                cancellationToken);
        }

        await auditLogger.LogAsync(
            AuditActions.StudentEnrolled,
            nameof(GroupEnrollment),
            enrollment.Id,
            newValues: new { enrollment.GroupId, enrollment.StudentId, enrollment.FinalPrice },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        await cache.RemoveAsync(CacheKeys.GroupDetails(group.Id), cancellationToken);
        await cache.RemoveAsync(CacheKeys.TeacherDashboard(group.TeacherId), cancellationToken);
        await cache.RemoveAsync(CacheKeys.StudentDashboard(student.Id), cancellationToken);

        return await context.GroupEnrollments
            .AsNoTracking()
            .Where(entity => entity.Id == enrollment.Id)
            .Select(EnrollmentProjections.ToGroupDto)
            .FirstAsync(cancellationToken);
    }
}
