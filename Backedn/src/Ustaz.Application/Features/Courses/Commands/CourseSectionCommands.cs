using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Features.Courses.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Courses.Commands;

internal static class CourseOwnership
{
    public static async Task<Course> LoadOwnedCourseAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        Guid courseId,
        CancellationToken cancellationToken)
    {
        var course = await context.Courses
            .Include(entity => entity.Sections)
                .ThenInclude(section => section.Lessons)
            .FirstOrDefaultAsync(entity => entity.Id == courseId, cancellationToken)
            ?? throw new NotFoundException("Course", courseId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, course.TeacherId);
        return course;
    }
}

public sealed record AddCourseSectionCommand : IRequest<CourseSectionDto>
{
    public Guid CourseId { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public int SortOrder { get; init; }
}

public sealed class AddCourseSectionCommandValidator : AbstractValidator<AddCourseSectionCommand>
{
    public AddCourseSectionCommandValidator()
    {
        RuleFor(command => command.CourseId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class AddCourseSectionCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache) : IRequestHandler<AddCourseSectionCommand, CourseSectionDto>
{
    public async Task<CourseSectionDto> Handle(AddCourseSectionCommand command, CancellationToken cancellationToken)
    {
        var course = await CourseOwnership.LoadOwnedCourseAsync(context, currentUser, command.CourseId, cancellationToken);

        var section = new CourseSection
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = command.Title.Trim(),
            Description = command.Description?.Trim(),
            SortOrder = command.SortOrder > 0 ? command.SortOrder : course.Sections.Count + 1,
            CreatedAt = dateTimeProvider.UtcNow
        };

        course.Sections.Add(section);
        course.UpdatedAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.CourseDetails(course.Id), cancellationToken);

        return new CourseSectionDto
        {
            Id = section.Id,
            CourseId = section.CourseId,
            Title = section.Title,
            Description = section.Description,
            SortOrder = section.SortOrder
        };
    }
}

public sealed record UpdateCourseSectionCommand : IRequest<CourseSectionDto>
{
    public Guid CourseId { get; init; }

    public Guid SectionId { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public int? SortOrder { get; init; }
}

public sealed class UpdateCourseSectionCommandValidator : AbstractValidator<UpdateCourseSectionCommand>
{
    public UpdateCourseSectionCommandValidator()
    {
        RuleFor(command => command.CourseId).NotEmpty();
        RuleFor(command => command.SectionId).NotEmpty();
        RuleFor(command => command.Title).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.SortOrder!.Value).GreaterThanOrEqualTo(0).When(command => command.SortOrder.HasValue);
    }
}

public sealed class UpdateCourseSectionCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache) : IRequestHandler<UpdateCourseSectionCommand, CourseSectionDto>
{
    public async Task<CourseSectionDto> Handle(UpdateCourseSectionCommand command, CancellationToken cancellationToken)
    {
        var course = await CourseOwnership.LoadOwnedCourseAsync(context, currentUser, command.CourseId, cancellationToken);

        var section = course.Sections.FirstOrDefault(entity => entity.Id == command.SectionId)
            ?? throw new NotFoundException("CourseSection", command.SectionId);

        if (command.Title is not null)
        {
            section.Title = command.Title.Trim();
        }

        if (command.Description is not null)
        {
            section.Description = command.Description.Trim();
        }

        if (command.SortOrder.HasValue)
        {
            section.SortOrder = command.SortOrder.Value;
        }

        section.UpdatedAt = dateTimeProvider.UtcNow;
        course.UpdatedAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.CourseDetails(course.Id), cancellationToken);

        return new CourseSectionDto
        {
            Id = section.Id,
            CourseId = section.CourseId,
            Title = section.Title,
            Description = section.Description,
            SortOrder = section.SortOrder,
            LessonsCount = section.Lessons.Count,
            DurationInMinutes = section.Lessons.Sum(lesson => lesson.DurationInMinutes)
        };
    }
}

public sealed record DeleteCourseSectionCommand(Guid CourseId, Guid SectionId) : IRequest<Unit>;

public sealed class DeleteCourseSectionCommandValidator : AbstractValidator<DeleteCourseSectionCommand>
{
    public DeleteCourseSectionCommandValidator()
    {
        RuleFor(command => command.CourseId).NotEmpty();
        RuleFor(command => command.SectionId).NotEmpty();
    }
}

public sealed class DeleteCourseSectionCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache) : IRequestHandler<DeleteCourseSectionCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCourseSectionCommand command, CancellationToken cancellationToken)
    {
        var course = await CourseOwnership.LoadOwnedCourseAsync(context, currentUser, command.CourseId, cancellationToken);

        var section = course.Sections.FirstOrDefault(entity => entity.Id == command.SectionId)
            ?? throw new NotFoundException("CourseSection", command.SectionId);

        var remainingPublished = course.Sections
            .Where(entity => entity.Id != section.Id)
            .SelectMany(entity => entity.Lessons)
            .Count(lesson => lesson.IsPublished && !string.IsNullOrWhiteSpace(lesson.VideoUrl));

        if (course.IsPublished && remainingPublished < DomainRules.MinPublishableLessonCount)
        {
            throw new BusinessRuleException(
                "COURSE_WOULD_HAVE_NO_CONTENT",
                "Deleting this section would leave the published course without content. Unpublish it first.");
        }

        context.CourseSections.Remove(section);
        course.Sections.Remove(section);
        course.RecalculateContentTotals(dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.CourseDetails(course.Id), cancellationToken);

        return Unit.Value;
    }
}
