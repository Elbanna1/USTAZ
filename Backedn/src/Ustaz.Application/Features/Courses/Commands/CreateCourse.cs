using FluentValidation;
using MediatR;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Courses.Dtos;
using Ustaz.Application.Features.Courses.Queries;
using Ustaz.Application.Features.Groups.Commands;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.ValueObjects;

namespace Ustaz.Application.Features.Courses.Commands;

public sealed record CreateCourseCommand : IRequest<CourseDetailsDto>
{
    public required Guid SubjectId { get; init; }

    public required Guid GradeId { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? ThumbnailUrl { get; init; }

    public decimal Price { get; init; }

    public decimal? OriginalPrice { get; init; }

    public int DiscountPercentage { get; init; }
}

public sealed class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseCommandValidator()
    {
        RuleFor(command => command.SubjectId).NotEmpty();
        RuleFor(command => command.GradeId).NotEmpty();

        RuleFor(command => command.Title)
            .NotEmpty().WithMessage("A course title is required.")
            .MaximumLength(DomainRules.NameMaxLength);

        RuleFor(command => command.Description).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.ThumbnailUrl).OptionalHttpUrl();
        RuleFor(command => command.Price).Money();

        RuleFor(command => command.OriginalPrice!.Value)
            .Money()
            .GreaterThanOrEqualTo(command => command.Price)
            .WithMessage("The original price must be greater than or equal to the current price.")
            .When(command => command.OriginalPrice.HasValue);

        RuleFor(command => command.DiscountPercentage)
            .InclusiveBetween(0, DomainRules.MaxPercentageDiscount);
    }
}

public sealed class CreateCourseCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IUrlGuard urlGuard,
    ICacheService cache,
    IPlanLimitService planLimits,
    IMediator mediator) : IRequestHandler<CreateCourseCommand, CourseDetailsDto>
{
    public async Task<CourseDetailsDto> Handle(CreateCourseCommand command, CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();

        await GroupRules.EnsureTeacherTeachesAsync(
            context, teacherId, command.SubjectId, command.GradeId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(command.ThumbnailUrl))
        {
            urlGuard.EnsureSafeHttpUrl(command.ThumbnailUrl, "Thumbnail URL");
        }

        var tenantId = await GroupRules.ResolveTenantIdAsync(context, teacherId, cancellationToken);
        await planLimits.EnsureQuotaAsync(tenantId, PlanQuota.Courses, 1, cancellationToken);

        var course = new Course
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TeacherId = teacherId,
            SubjectId = command.SubjectId,
            GradeId = command.GradeId,
            Title = command.Title.Trim(),
            Slug = Slug.CreateUnique(command.Title).Value,
            Description = command.Description?.Trim(),
            ThumbnailUrl = command.ThumbnailUrl?.Trim(),
            Price = command.Price,
            OriginalPrice = command.OriginalPrice,
            DiscountPercentage = command.DiscountPercentage,
            IsPublished = false,
            IsFeatured = false,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await context.Courses.AddAsync(course, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveByTagAsync(CacheKeys.CoursesTag, cancellationToken);

        return await mediator.Send(new GetCourseByIdQuery(course.Id), cancellationToken);
    }
}
