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

public sealed record EnrollInCourseCommand : IRequest<CourseEnrollmentDto>
{
    public Guid CourseId { get; init; }

    public string? DiscountCode { get; init; }
}

public sealed class EnrollInCourseCommandValidator : AbstractValidator<EnrollInCourseCommand>
{
    public EnrollInCourseCommandValidator()
    {
        RuleFor(command => command.CourseId).NotEmpty();
        RuleFor(command => command.DiscountCode).MaximumLength(50);
    }
}

public sealed class EnrollInCourseCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    ICacheService cache,
    ITenantProvisioningService tenantProvisioning) : IRequestHandler<EnrollInCourseCommand, CourseEnrollmentDto>
{
    public async Task<CourseEnrollmentDto> Handle(EnrollInCourseCommand command, CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();
        var utcNow = dateTimeProvider.UtcNow;

        var course = await context.Courses
            .FirstOrDefaultAsync(entity => entity.Id == command.CourseId, cancellationToken)
            ?? throw new NotFoundException("Course", command.CourseId);

        if (!course.IsPublished)
        {
            throw new BusinessRuleException("COURSE_NOT_PUBLISHED", "This course is not available for enrollment yet.");
        }

        var alreadyEnrolled = await context.CourseEnrollments.AnyAsync(
            enrollment => enrollment.CourseId == course.Id
                && enrollment.StudentId == studentId
                && enrollment.Status != EnrollmentStatus.Cancelled,
            cancellationToken);

        if (alreadyEnrolled)
        {
            throw new ConflictException("You are already enrolled in this course.");
        }

        // Buying a course makes the student a member of the workspace, exactly as joining a
        // group does; the tenant-scoped features all read that membership.
        await tenantProvisioning.EnsureStudentBelongsToTenantAsync(
            course.TenantId, studentId, cancellationToken);

        var price = course.Price;
        Discount? discount = null;
        var discountAmount = 0m;

        if (!string.IsNullOrWhiteSpace(command.DiscountCode))
        {
            var normalized = command.DiscountCode.Trim().ToUpperInvariant();

            discount = await context.Discounts
                .FirstOrDefaultAsync(
                    entity => entity.TeacherId == course.TeacherId && entity.Code == normalized,
                    cancellationToken)
                ?? throw new NotFoundException("This discount code does not exist for this teacher.");

            discount.EnsureRedeemable(utcNow, price);
            discountAmount = discount.CalculateDiscountAmount(price);
            discount.Redeem(utcNow, price);
        }

        var enrollment = new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            Status = EnrollmentStatus.Active,
            EnrolledAt = utcNow,
            PricePaid = price,
            DiscountAmount = discountAmount,
            FinalPrice = Math.Round(price - discountAmount, 2, MidpointRounding.AwayFromZero),
            DiscountId = discount?.Id,
            DiscountCode = discount?.Code,
            ProgressPercentage = 0,
            CreatedAt = utcNow
        };

        course.StudentCount++;
        course.UpdatedAt = utcNow;

        enrollment.RaiseDomainEvent(new StudentEnrolledInCourseEvent(
            enrollment.Id, course.Id, studentId, course.TeacherId, course.Title));

        await context.CourseEnrollments.AddAsync(enrollment, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var teacherUserId = await context.TeacherProfiles
            .Where(profile => profile.Id == course.TeacherId)
            .Select(profile => profile.UserId)
            .FirstAsync(cancellationToken);

        var studentName = await context.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == studentId)
            .Select(profile => profile.User.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "طالب";

        await notifications.SendAsync(
            teacherUserId,
            new NotificationRequest
            {
                Event = NotificationEvents.CourseStudentEnrolled,
                TenantId = course.TenantId,
                ReferenceId = enrollment.Id,
                Values = new Dictionary<string, string>
                {
                    ["student"] = studentName,
                    ["course"] = course.Title,
                    ["courseId"] = course.Id.ToString()
                }
            },
            cancellationToken);

        await cache.RemoveAsync(CacheKeys.CourseDetails(course.Id), cancellationToken);
        await cache.RemoveAsync(CacheKeys.TeacherDashboard(course.TeacherId), cancellationToken);
        await cache.RemoveAsync(CacheKeys.StudentDashboard(studentId), cancellationToken);

        return await context.CourseEnrollments
            .AsNoTracking()
            .Where(entity => entity.Id == enrollment.Id)
            .Select(EnrollmentProjections.ToCourseDto)
            .FirstAsync(cancellationToken);
    }
}

/// <summary>Cancels a course enrollment. The owning student, the teacher or an admin may do this.</summary>
public sealed record CancelCourseEnrollmentCommand : IRequest<Unit>
{
    public Guid EnrollmentId { get; init; }

    public string? Reason { get; init; }
}

public sealed class CancelCourseEnrollmentCommandValidator : AbstractValidator<CancelCourseEnrollmentCommand>
{
    public CancelCourseEnrollmentCommandValidator()
    {
        RuleFor(command => command.EnrollmentId).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(DomainRules.ShortTextMaxLength);
    }
}

public sealed class CancelCourseEnrollmentCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache) : IRequestHandler<CancelCourseEnrollmentCommand, Unit>
{
    public async Task<Unit> Handle(CancelCourseEnrollmentCommand command, CancellationToken cancellationToken)
    {
        var enrollment = await context.CourseEnrollments
            .Include(entity => entity.Course)
            .FirstOrDefaultAsync(entity => entity.Id == command.EnrollmentId, cancellationToken)
            ?? throw new NotFoundException("CourseEnrollment", command.EnrollmentId);

        var isStudentOwner = currentUser.StudentId is not null && currentUser.StudentId == enrollment.StudentId;
        var isTeacherOwner = currentUser.TeacherId is not null && currentUser.TeacherId == enrollment.Course.TeacherId;

        if (!isStudentOwner && !isTeacherOwner && !currentUser.IsAdmin)
        {
            throw new ForbiddenAccessException("You can only cancel your own enrollments.");
        }

        var utcNow = dateTimeProvider.UtcNow;
        var wasActive = enrollment.Status != EnrollmentStatus.Cancelled;

        enrollment.Cancel(utcNow, command.Reason?.Trim());

        if (wasActive)
        {
            enrollment.Course.StudentCount = Math.Max(0, enrollment.Course.StudentCount - 1);
            enrollment.Course.UpdatedAt = utcNow;
        }

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(CacheKeys.CourseDetails(enrollment.CourseId), cancellationToken);
        await cache.RemoveAsync(CacheKeys.StudentDashboard(enrollment.StudentId), cancellationToken);
        await cache.RemoveAsync(CacheKeys.TeacherDashboard(enrollment.Course.TeacherId), cancellationToken);

        return Unit.Value;
    }
}

/// <summary>Updates the caller's watch progress for a course.</summary>
public sealed record UpdateCourseProgressCommand : IRequest<CourseEnrollmentDto>
{
    public Guid EnrollmentId { get; init; }

    public int ProgressPercentage { get; init; }
}

public sealed class UpdateCourseProgressCommandValidator : AbstractValidator<UpdateCourseProgressCommand>
{
    public UpdateCourseProgressCommandValidator()
    {
        RuleFor(command => command.EnrollmentId).NotEmpty();
        RuleFor(command => command.ProgressPercentage).InclusiveBetween(0, 100);
    }
}

public sealed class UpdateCourseProgressCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<UpdateCourseProgressCommand, CourseEnrollmentDto>
{
    public async Task<CourseEnrollmentDto> Handle(
        UpdateCourseProgressCommand command,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        var enrollment = await context.CourseEnrollments
            .FirstOrDefaultAsync(entity => entity.Id == command.EnrollmentId, cancellationToken)
            ?? throw new NotFoundException("CourseEnrollment", command.EnrollmentId);

        if (enrollment.StudentId != studentId)
        {
            throw new ForbiddenAccessException("You can only update your own progress.");
        }

        enrollment.UpdateProgress(command.ProgressPercentage, dateTimeProvider.UtcNow);
        await context.SaveChangesAsync(cancellationToken);

        return await context.CourseEnrollments
            .AsNoTracking()
            .Where(entity => entity.Id == enrollment.Id)
            .Select(EnrollmentProjections.ToCourseDto)
            .FirstAsync(cancellationToken);
    }
}
