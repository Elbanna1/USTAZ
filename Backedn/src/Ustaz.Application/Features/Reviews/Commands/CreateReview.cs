using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Reviews.Dtos;
using Ustaz.Application.Features.Reviews.Services;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Events;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Reviews.Commands;

/// <summary>
/// Writes a review. A student may only review a teacher, group or course they actually
/// enrolled in, and only once per target.
/// </summary>
public sealed record CreateReviewCommand : IRequest<ReviewDto>
{
    public Guid? TeacherId { get; init; }

    public Guid? GroupId { get; init; }

    public Guid? CourseId { get; init; }

    public int Rating { get; init; }

    public string? Comment { get; init; }
}

public sealed class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator()
    {
        RuleFor(command => command.Rating).RatingValue();
        RuleFor(command => command.Comment).MaximumLength(DomainRules.DescriptionMaxLength);

        RuleFor(command => command)
            .Must(command => command.TeacherId.HasValue || command.GroupId.HasValue || command.CourseId.HasValue)
            .WithMessage("Specify the teacher, group or course you are reviewing.")
            .WithName("TeacherId");

        RuleFor(command => command)
            .Must(command => !(command.GroupId.HasValue && command.CourseId.HasValue))
            .WithMessage("A review targets either a group or a course, not both.")
            .WithName("GroupId");
    }
}

public sealed class CreateReviewCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IRatingRecalculator ratingRecalculator,
    INotificationDispatcher notifications,
    ICacheService cache) : IRequestHandler<CreateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(CreateReviewCommand command, CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();
        var utcNow = dateTimeProvider.UtcNow;

        var teacherId = await ResolveTeacherAndAuthoriseAsync(command, studentId, cancellationToken);

        var duplicate = await context.Reviews.AnyAsync(
            review => review.StudentId == studentId
                && review.TeacherId == teacherId
                && review.GroupId == command.GroupId
                && review.CourseId == command.CourseId,
            cancellationToken);

        if (duplicate)
        {
            throw new ConflictException("You have already reviewed this. Edit your existing review instead.");
        }

        var review = new Review
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            TeacherId = teacherId,
            GroupId = command.GroupId,
            CourseId = command.CourseId,
            Rating = command.Rating,
            Comment = command.Comment?.Trim(),
            // Reviews from verified enrollments are trusted immediately; admins can still moderate.
            IsApproved = true,
            ApprovedAt = utcNow,
            CreatedAt = utcNow
        };

        review.EnsureRatingIsValid();
        review.RaiseDomainEvent(new ReviewCreatedEvent(
            review.Id, teacherId, studentId, command.GroupId, command.CourseId, command.Rating));

        await context.Reviews.AddAsync(review, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await ratingRecalculator.RecalculateAsync(teacherId, command.GroupId, command.CourseId, cancellationToken);

        var teacherUserId = await context.TeacherProfiles
            .Where(profile => profile.Id == teacherId)
            .Select(profile => profile.UserId)
            .FirstAsync(cancellationToken);

        var reviewerName = await context.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == studentId)
            .Select(profile => profile.User.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "طالب";

        await notifications.SendAsync(
            teacherUserId,
            new NotificationRequest
            {
                Event = NotificationEvents.ReviewReceived,
                ReferenceId = review.Id,
                Values = new Dictionary<string, string>
                {
                    ["student"] = reviewerName,
                    ["rating"] = command.Rating.ToString()
                }
            },
            cancellationToken);

        await InvalidateCachesAsync(teacherId, command, cancellationToken);

        return await ReviewQueryHelper.LoadAsync(context, review.Id, studentId, cancellationToken);
    }

    /// <summary>
    /// Resolves the teacher the review belongs to and enforces that the student has a real
    /// relationship with the target.
    /// </summary>
    private async Task<Guid> ResolveTeacherAndAuthoriseAsync(
        CreateReviewCommand command,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        if (command.GroupId.HasValue)
        {
            var group = await context.Groups
                .AsNoTracking()
                .Where(entity => entity.Id == command.GroupId.Value)
                .Select(entity => new { entity.TeacherId })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Group", command.GroupId.Value);

            var enrolled = await context.GroupEnrollments.AnyAsync(
                enrollment => enrollment.GroupId == command.GroupId.Value
                    && enrollment.StudentId == studentId
                    && enrollment.Status != EnrollmentStatus.Cancelled,
                cancellationToken);

            if (!enrolled)
            {
                throw new BusinessRuleException(
                    "REVIEW_WITHOUT_ENROLLMENT",
                    "You can only review a group you have enrolled in.");
            }

            return group.TeacherId;
        }

        if (command.CourseId.HasValue)
        {
            var course = await context.Courses
                .AsNoTracking()
                .Where(entity => entity.Id == command.CourseId.Value)
                .Select(entity => new { entity.TeacherId })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Course", command.CourseId.Value);

            var enrolled = await context.CourseEnrollments.AnyAsync(
                enrollment => enrollment.CourseId == command.CourseId.Value
                    && enrollment.StudentId == studentId
                    && enrollment.Status != EnrollmentStatus.Cancelled,
                cancellationToken);

            if (!enrolled)
            {
                throw new BusinessRuleException(
                    "REVIEW_WITHOUT_ENROLLMENT",
                    "You can only review a course you have enrolled in.");
            }

            return course.TeacherId;
        }

        var teacherId = command.TeacherId!.Value;

        if (!await context.TeacherProfiles.AnyAsync(profile => profile.Id == teacherId, cancellationToken))
        {
            throw new NotFoundException("Teacher", teacherId);
        }

        var hasRelationship = await context.GroupEnrollments.AnyAsync(
            enrollment => enrollment.StudentId == studentId
                && enrollment.Group.TeacherId == teacherId
                && enrollment.Status != EnrollmentStatus.Cancelled,
            cancellationToken);

        hasRelationship = hasRelationship || await context.CourseEnrollments.AnyAsync(
            enrollment => enrollment.StudentId == studentId
                && enrollment.Course.TeacherId == teacherId
                && enrollment.Status != EnrollmentStatus.Cancelled,
            cancellationToken);

        if (!hasRelationship)
        {
            throw new BusinessRuleException(
                "REVIEW_WITHOUT_ENROLLMENT",
                "You can only review a teacher whose group or course you have enrolled in.");
        }

        return teacherId;
    }

    private async Task InvalidateCachesAsync(Guid teacherId, CreateReviewCommand command, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(CacheKeys.TeacherProfile(teacherId), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);

        if (command.GroupId.HasValue)
        {
            await cache.RemoveAsync(CacheKeys.GroupDetails(command.GroupId.Value), cancellationToken);
        }

        if (command.CourseId.HasValue)
        {
            await cache.RemoveAsync(CacheKeys.CourseDetails(command.CourseId.Value), cancellationToken);
        }
    }
}
