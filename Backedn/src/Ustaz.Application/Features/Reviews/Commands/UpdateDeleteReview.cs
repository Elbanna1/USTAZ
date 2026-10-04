using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Reviews.Dtos;
using Ustaz.Application.Features.Reviews.Services;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Events;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Reviews.Commands;

public sealed record UpdateReviewCommand : IRequest<ReviewDto>
{
    public Guid ReviewId { get; init; }

    public int? Rating { get; init; }

    public string? Comment { get; init; }
}

public sealed class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(command => command.ReviewId).NotEmpty();
        RuleFor(command => command.Rating!.Value).RatingValue().When(command => command.Rating.HasValue);
        RuleFor(command => command.Comment).MaximumLength(DomainRules.DescriptionMaxLength);
    }
}

public sealed class UpdateReviewCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IRatingRecalculator ratingRecalculator,
    ICacheService cache) : IRequestHandler<UpdateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(UpdateReviewCommand command, CancellationToken cancellationToken)
    {
        var review = await context.Reviews
            .FirstOrDefaultAsync(entity => entity.Id == command.ReviewId, cancellationToken)
            ?? throw new NotFoundException("Review", command.ReviewId);

        var isOwner = currentUser.StudentId is not null && currentUser.StudentId == review.StudentId;

        if (!isOwner && !currentUser.IsAdmin)
        {
            throw new ForbiddenAccessException("You can only edit your own reviews.");
        }

        if (command.Rating.HasValue)
        {
            review.Rating = command.Rating.Value;
            review.EnsureRatingIsValid();
        }

        if (command.Comment is not null)
        {
            review.Comment = command.Comment.Trim();
        }

        review.UpdatedAt = dateTimeProvider.UtcNow;
        review.RaiseDomainEvent(new ReviewUpdatedEvent(review.Id, review.TeacherId, review.GroupId, review.CourseId));

        await context.SaveChangesAsync(cancellationToken);
        await ratingRecalculator.RecalculateAsync(review.TeacherId, review.GroupId, review.CourseId, cancellationToken);

        await cache.RemoveAsync(CacheKeys.TeacherProfile(review.TeacherId), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);

        return await ReviewQueryHelper.LoadAsync(context, review.Id, currentUser.StudentId, cancellationToken);
    }
}

public sealed record DeleteReviewCommand(Guid ReviewId) : IRequest<Unit>;

public sealed class DeleteReviewCommandValidator : AbstractValidator<DeleteReviewCommand>
{
    public DeleteReviewCommandValidator() => RuleFor(command => command.ReviewId).NotEmpty();
}

public sealed class DeleteReviewCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IRatingRecalculator ratingRecalculator,
    ICacheService cache) : IRequestHandler<DeleteReviewCommand, Unit>
{
    public async Task<Unit> Handle(DeleteReviewCommand command, CancellationToken cancellationToken)
    {
        var review = await context.Reviews
            .FirstOrDefaultAsync(entity => entity.Id == command.ReviewId, cancellationToken)
            ?? throw new NotFoundException("Review", command.ReviewId);

        var isOwner = currentUser.StudentId is not null && currentUser.StudentId == review.StudentId;

        if (!isOwner && !currentUser.IsAdmin)
        {
            throw new ForbiddenAccessException("You can only delete your own reviews.");
        }

        var teacherId = review.TeacherId;
        var groupId = review.GroupId;
        var courseId = review.CourseId;

        context.Reviews.Remove(review);
        await context.SaveChangesAsync(cancellationToken);

        await ratingRecalculator.RecalculateAsync(teacherId, groupId, courseId, cancellationToken);

        await cache.RemoveAsync(CacheKeys.TeacherProfile(teacherId), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);

        return Unit.Value;
    }
}

/// <summary>Admin moderation: approve or hide a review.</summary>
public sealed record ModerateReviewCommand : IRequest<Unit>
{
    public Guid ReviewId { get; init; }

    public bool IsApproved { get; init; }

    public string? Note { get; init; }
}

public sealed class ModerateReviewCommandValidator : AbstractValidator<ModerateReviewCommand>
{
    public ModerateReviewCommandValidator()
    {
        RuleFor(command => command.ReviewId).NotEmpty();
        RuleFor(command => command.Note).MaximumLength(DomainRules.ShortTextMaxLength);

        RuleFor(command => command.Note)
            .NotEmpty()
            .When(command => !command.IsApproved)
            .WithMessage("Provide a reason when rejecting a review.");
    }
}

public sealed class ModerateReviewCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IRatingRecalculator ratingRecalculator,
    ICacheService cache) : IRequestHandler<ModerateReviewCommand, Unit>
{
    public async Task<Unit> Handle(ModerateReviewCommand command, CancellationToken cancellationToken)
    {
        var review = await context.Reviews
            .FirstOrDefaultAsync(entity => entity.Id == command.ReviewId, cancellationToken)
            ?? throw new NotFoundException("Review", command.ReviewId);

        var utcNow = dateTimeProvider.UtcNow;

        if (command.IsApproved)
        {
            review.Approve(utcNow, command.Note?.Trim());
        }
        else
        {
            review.Reject(utcNow, command.Note!.Trim());
        }

        await context.SaveChangesAsync(cancellationToken);
        await ratingRecalculator.RecalculateAsync(review.TeacherId, review.GroupId, review.CourseId, cancellationToken);

        await cache.RemoveAsync(CacheKeys.TeacherProfile(review.TeacherId), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);

        return Unit.Value;
    }
}
