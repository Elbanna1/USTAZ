using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Courses.Dtos;
using Ustaz.Application.Features.Courses.Queries;
using Ustaz.Application.Features.Groups.Commands;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Courses.Commands;

public sealed record UpdateCourseCommand : IRequest<CourseDetailsDto>
{
    public Guid CourseId { get; init; }

    public Guid? SubjectId { get; init; }

    public Guid? GradeId { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? ThumbnailUrl { get; init; }

    public decimal? Price { get; init; }

    public decimal? OriginalPrice { get; init; }

    public int? DiscountPercentage { get; init; }
}

public sealed class UpdateCourseCommandValidator : AbstractValidator<UpdateCourseCommand>
{
    public UpdateCourseCommandValidator()
    {
        RuleFor(command => command.CourseId).NotEmpty();
        RuleFor(command => command.Title).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.ThumbnailUrl).OptionalHttpUrl();
        RuleFor(command => command.Price!.Value).Money().When(command => command.Price.HasValue);
        RuleFor(command => command.OriginalPrice!.Value).Money().When(command => command.OriginalPrice.HasValue);

        RuleFor(command => command.DiscountPercentage!.Value)
            .InclusiveBetween(0, DomainRules.MaxPercentageDiscount)
            .When(command => command.DiscountPercentage.HasValue);
    }
}

public sealed class UpdateCourseCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IUrlGuard urlGuard,
    ICacheService cache,
    IMediator mediator) : IRequestHandler<UpdateCourseCommand, CourseDetailsDto>
{
    public async Task<CourseDetailsDto> Handle(UpdateCourseCommand command, CancellationToken cancellationToken)
    {
        var course = await context.Courses
            .FirstOrDefaultAsync(entity => entity.Id == command.CourseId, cancellationToken)
            ?? throw new NotFoundException("Course", command.CourseId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, course.TeacherId);

        var subjectId = command.SubjectId ?? course.SubjectId;
        var gradeId = command.GradeId ?? course.GradeId;

        if (subjectId != course.SubjectId || gradeId != course.GradeId)
        {
            await GroupRules.EnsureTeacherTeachesAsync(context, course.TeacherId, subjectId, gradeId, cancellationToken);
            course.SubjectId = subjectId;
            course.GradeId = gradeId;
        }

        if (command.Title is not null)
        {
            course.Title = command.Title.Trim();
        }

        if (command.Description is not null)
        {
            course.Description = command.Description.Trim();
        }

        if (command.ThumbnailUrl is not null)
        {
            course.ThumbnailUrl = string.IsNullOrWhiteSpace(command.ThumbnailUrl)
                ? null
                : urlGuard.EnsureSafeHttpUrl(command.ThumbnailUrl, "Thumbnail URL");
        }

        if (command.Price.HasValue)
        {
            course.Price = command.Price.Value;
        }

        if (command.OriginalPrice.HasValue)
        {
            course.OriginalPrice = command.OriginalPrice.Value;
        }

        if (command.DiscountPercentage.HasValue)
        {
            course.DiscountPercentage = command.DiscountPercentage.Value;
        }

        if (course.OriginalPrice.HasValue && course.OriginalPrice.Value < course.Price)
        {
            throw new BusinessRuleException(
                "INVALID_PRICING",
                "The original price must be greater than or equal to the current price.");
        }

        course.UpdatedAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(CacheKeys.CourseDetails(course.Id), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.CoursesTag, cancellationToken);

        return await mediator.Send(new GetCourseByIdQuery(course.Id), cancellationToken);
    }
}
