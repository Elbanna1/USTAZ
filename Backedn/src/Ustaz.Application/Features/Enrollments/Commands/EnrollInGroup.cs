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

/// <summary>Subscribes the calling student to a group, honouring capacity and discount rules.</summary>
public sealed record EnrollInGroupCommand : IRequest<GroupEnrollmentDto>
{
    public Guid GroupId { get; init; }

    public string? DiscountCode { get; init; }
}

public sealed class EnrollInGroupCommandValidator : AbstractValidator<EnrollInGroupCommand>
{
    public EnrollInGroupCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.DiscountCode).MaximumLength(50);
    }
}

public sealed class EnrollInGroupCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    ICacheService cache,
    ITenantProvisioningService tenantProvisioning) : IRequestHandler<EnrollInGroupCommand, GroupEnrollmentDto>
{
    public async Task<GroupEnrollmentDto> Handle(EnrollInGroupCommand command, CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();
        var utcNow = dateTimeProvider.UtcNow;

        var group = await context.Groups
            .FirstOrDefaultAsync(entity => entity.Id == command.GroupId, cancellationToken)
            ?? throw new NotFoundException("Group", command.GroupId);

        var alreadyEnrolled = await context.GroupEnrollments.AnyAsync(
            enrollment => enrollment.GroupId == group.Id
                && enrollment.StudentId == studentId
                && (enrollment.Status == EnrollmentStatus.Active || enrollment.Status == EnrollmentStatus.Pending),
            cancellationToken);

        if (alreadyEnrolled)
        {
            throw new ConflictException("You are already enrolled in this group.");
        }

        // Throws when the group is inactive, unpublished or already full.
        group.EnsureHasCapacity();

        // Joining a group makes the student a member of the workspace. Everything tenant
        // scoped — announcements, messaging, tuition records, the plan's student quota —
        // keys off that membership, so it has to be established here.
        await tenantProvisioning.EnsureStudentBelongsToTenantAsync(
            group.TenantId, studentId, cancellationToken);

        var price = group.MonthlyPrice;
        var (discount, discountAmount) = await ResolveDiscountAsync(
            command.DiscountCode, group.TeacherId, price, utcNow, cancellationToken);

        var enrollment = new GroupEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            GroupId = group.Id,
            Status = EnrollmentStatus.Active,
            EnrolledAt = utcNow,
            PricePaid = price,
            DiscountAmount = discountAmount,
            FinalPrice = Math.Round(price - discountAmount, 2, MidpointRounding.AwayFromZero),
            DiscountId = discount?.Id,
            DiscountCode = discount?.Code,
            CreatedAt = utcNow
        };

        group.IncrementEnrollment(utcNow);
        discount?.Redeem(utcNow, price);

        var teacher = await context.TeacherProfiles
            .FirstAsync(profile => profile.Id == group.TeacherId, cancellationToken);
        teacher.StudentsCount++;
        teacher.UpdatedAt = utcNow;

        enrollment.RaiseDomainEvent(new StudentEnrolledInGroupEvent(
            enrollment.Id, group.Id, studentId, group.TeacherId, group.Name));

        await context.GroupEnrollments.AddAsync(enrollment, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var studentName = await context.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == studentId)
            .Select(profile => profile.User.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "طالب";

        await notifications.SendAsync(
            teacher.UserId,
            new NotificationRequest
            {
                Event = NotificationEvents.GroupEnrollmentCreated,
                TenantId = group.TenantId,
                ReferenceId = enrollment.Id,
                Values = new Dictionary<string, string>
                {
                    ["student"] = studentName,
                    ["group"] = group.Name,
                    ["groupId"] = group.Id.ToString()
                }
            },
            cancellationToken);

        await cache.RemoveAsync(CacheKeys.GroupDetails(group.Id), cancellationToken);
        await cache.RemoveAsync(CacheKeys.TeacherDashboard(group.TeacherId), cancellationToken);
        await cache.RemoveAsync(CacheKeys.StudentDashboard(studentId), cancellationToken);

        return await context.GroupEnrollments
            .AsNoTracking()
            .Where(entity => entity.Id == enrollment.Id)
            .Select(EnrollmentProjections.ToGroupDto)
            .FirstAsync(cancellationToken);
    }

    /// <summary>Resolves and validates a promo code against the owning teacher and the basket amount.</summary>
    private async Task<(Discount? Discount, decimal Amount)> ResolveDiscountAsync(
        string? code,
        Guid teacherId,
        decimal price,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return (null, 0m);
        }

        var normalized = code.Trim().ToUpperInvariant();

        var discount = await context.Discounts
            .FirstOrDefaultAsync(
                entity => entity.TeacherId == teacherId && entity.Code == normalized,
                cancellationToken)
            ?? throw new NotFoundException("This discount code does not exist for this teacher.");

        discount.EnsureRedeemable(utcNow, price);

        return (discount, discount.CalculateDiscountAmount(price));
    }
}
