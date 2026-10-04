using FluentValidation;
using MediatR;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Courses.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Courses.Commands;

public sealed record AddCourseLessonCommand : IRequest<CourseLessonDto>
{
    public Guid CourseId { get; init; }

    public Guid SectionId { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? VideoUrl { get; init; }

    public int DurationInMinutes { get; init; }

    public int SortOrder { get; init; }

    public bool IsPreview { get; init; }

    public bool IsPublished { get; init; } = true;
}

public sealed class AddCourseLessonCommandValidator : AbstractValidator<AddCourseLessonCommand>
{
    public AddCourseLessonCommandValidator()
    {
        RuleFor(command => command.CourseId).NotEmpty();
        RuleFor(command => command.SectionId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.VideoUrl).OptionalHttpUrl();
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);

        RuleFor(command => command.DurationInMinutes)
            .InclusiveBetween(0, 1440)
            .WithMessage("Lesson duration must be between 0 and 1440 minutes.");

        RuleFor(command => command.VideoUrl)
            .NotEmpty()
            .When(command => command.IsPublished)
            .WithMessage("A published lesson must have a video URL.");
    }
}

public sealed class AddCourseLessonCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IUrlGuard urlGuard,
    ICacheService cache,
    INotificationDispatcher notifications) : IRequestHandler<AddCourseLessonCommand, CourseLessonDto>
{
    public async Task<CourseLessonDto> Handle(AddCourseLessonCommand command, CancellationToken cancellationToken)
    {
        var course = await CourseOwnership.LoadOwnedCourseAsync(context, currentUser, command.CourseId, cancellationToken);

        var section = course.Sections.FirstOrDefault(entity => entity.Id == command.SectionId)
            ?? throw new NotFoundException("CourseSection", command.SectionId);

        if (!string.IsNullOrWhiteSpace(command.VideoUrl))
        {
            urlGuard.EnsureSafeHttpUrl(command.VideoUrl, "Video URL");
        }

        var lesson = new CourseLesson
        {
            Id = Guid.NewGuid(),
            SectionId = section.Id,
            Title = command.Title.Trim(),
            Description = command.Description?.Trim(),
            VideoUrl = command.VideoUrl?.Trim(),
            DurationInMinutes = command.DurationInMinutes,
            SortOrder = command.SortOrder > 0 ? command.SortOrder : section.Lessons.Count + 1,
            IsPreview = command.IsPreview,
            IsPublished = command.IsPublished,
            CreatedAt = dateTimeProvider.UtcNow
        };

        section.Lessons.Add(lesson);
        course.RecalculateContentTotals(dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.CourseDetails(course.Id), cancellationToken);

        // Only a published lesson is news; a draft is still the teacher's own business.
        if (lesson.IsPublished)
        {
            var students = await NotificationAudience.CourseStudentUserIdsAsync(
                context, course.Id, cancellationToken);

            await notifications.SendManyAsync(
                students,
                new NotificationRequest
                {
                    Event = NotificationEvents.CourseLessonAdded,
                    TenantId = course.TenantId,
                    ReferenceId = lesson.Id,
                    DeduplicationKey = "lesson-added:" + lesson.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["course"] = course.Title,
                        ["courseId"] = course.Id.ToString(),
                        ["lesson"] = lesson.Title
                    }
                },
                cancellationToken);
        }

        return ToDto(lesson);
    }

    internal static CourseLessonDto ToDto(CourseLesson lesson) => new()
    {
        Id = lesson.Id,
        SectionId = lesson.SectionId,
        Title = lesson.Title,
        Description = lesson.Description,
        VideoUrl = lesson.VideoUrl,
        DurationInMinutes = lesson.DurationInMinutes,
        SortOrder = lesson.SortOrder,
        IsPreview = lesson.IsPreview,
        IsPublished = lesson.IsPublished,
        IsLocked = false
    };
}

public sealed record UpdateCourseLessonCommand : IRequest<CourseLessonDto>
{
    public Guid CourseId { get; init; }

    public Guid LessonId { get; init; }

    public Guid? SectionId { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? VideoUrl { get; init; }

    public int? DurationInMinutes { get; init; }

    public int? SortOrder { get; init; }

    public bool? IsPreview { get; init; }

    public bool? IsPublished { get; init; }
}

public sealed class UpdateCourseLessonCommandValidator : AbstractValidator<UpdateCourseLessonCommand>
{
    public UpdateCourseLessonCommandValidator()
    {
        RuleFor(command => command.CourseId).NotEmpty();
        RuleFor(command => command.LessonId).NotEmpty();
        RuleFor(command => command.Title).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.VideoUrl).OptionalHttpUrl();

        RuleFor(command => command.DurationInMinutes!.Value)
            .InclusiveBetween(0, 1440)
            .When(command => command.DurationInMinutes.HasValue);
    }
}

public sealed class UpdateCourseLessonCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IUrlGuard urlGuard,
    ICacheService cache) : IRequestHandler<UpdateCourseLessonCommand, CourseLessonDto>
{
    public async Task<CourseLessonDto> Handle(UpdateCourseLessonCommand command, CancellationToken cancellationToken)
    {
        var course = await CourseOwnership.LoadOwnedCourseAsync(context, currentUser, command.CourseId, cancellationToken);

        var lesson = course.Sections
            .SelectMany(section => section.Lessons)
            .FirstOrDefault(entity => entity.Id == command.LessonId)
            ?? throw new NotFoundException("CourseLesson", command.LessonId);

        if (command.SectionId.HasValue && command.SectionId.Value != lesson.SectionId)
        {
            var target = course.Sections.FirstOrDefault(section => section.Id == command.SectionId.Value)
                ?? throw new NotFoundException("CourseSection", command.SectionId.Value);

            lesson.SectionId = target.Id;
        }

        if (command.Title is not null)
        {
            lesson.Title = command.Title.Trim();
        }

        if (command.Description is not null)
        {
            lesson.Description = command.Description.Trim();
        }

        if (command.VideoUrl is not null)
        {
            lesson.VideoUrl = string.IsNullOrWhiteSpace(command.VideoUrl)
                ? null
                : urlGuard.EnsureSafeHttpUrl(command.VideoUrl, "Video URL");
        }

        if (command.DurationInMinutes.HasValue)
        {
            lesson.DurationInMinutes = command.DurationInMinutes.Value;
        }

        if (command.SortOrder.HasValue)
        {
            lesson.SortOrder = command.SortOrder.Value;
        }

        if (command.IsPreview.HasValue)
        {
            lesson.IsPreview = command.IsPreview.Value;
        }

        if (command.IsPublished.HasValue)
        {
            lesson.IsPublished = command.IsPublished.Value;
        }

        if (lesson.IsPublished && string.IsNullOrWhiteSpace(lesson.VideoUrl))
        {
            throw new BusinessRuleException(
                "LESSON_WITHOUT_VIDEO",
                "A published lesson must have a video URL.");
        }

        lesson.UpdatedAt = dateTimeProvider.UtcNow;
        course.RecalculateContentTotals(dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.CourseDetails(course.Id), cancellationToken);

        return AddCourseLessonCommandHandler.ToDto(lesson);
    }
}

public sealed record DeleteCourseLessonCommand(Guid CourseId, Guid LessonId) : IRequest<Unit>;

public sealed class DeleteCourseLessonCommandValidator : AbstractValidator<DeleteCourseLessonCommand>
{
    public DeleteCourseLessonCommandValidator()
    {
        RuleFor(command => command.CourseId).NotEmpty();
        RuleFor(command => command.LessonId).NotEmpty();
    }
}

public sealed class DeleteCourseLessonCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IMediaService mediaService,
    ICacheService cache) : IRequestHandler<DeleteCourseLessonCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCourseLessonCommand command, CancellationToken cancellationToken)
    {
        var course = await CourseOwnership.LoadOwnedCourseAsync(context, currentUser, command.CourseId, cancellationToken);

        var section = course.Sections.FirstOrDefault(entity => entity.Lessons.Any(lesson => lesson.Id == command.LessonId))
            ?? throw new NotFoundException("CourseLesson", command.LessonId);

        var lesson = section.Lessons.First(entity => entity.Id == command.LessonId);

        var remainingPublished = course.Sections
            .SelectMany(entity => entity.Lessons)
            .Count(entity => entity.Id != lesson.Id
                && entity.IsPublished
                && !string.IsNullOrWhiteSpace(entity.VideoUrl));

        if (course.IsPublished && remainingPublished < DomainRules.MinPublishableLessonCount)
        {
            throw new BusinessRuleException(
                "COURSE_WOULD_HAVE_NO_CONTENT",
                "Deleting this lesson would leave the published course without content. Unpublish it first.");
        }

        var videoPublicId = lesson.VideoPublicId;

        context.CourseLessons.Remove(lesson);
        section.Lessons.Remove(lesson);
        course.RecalculateContentTotals(dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(videoPublicId))
        {
            await mediaService.DeleteAsync(videoPublicId, cancellationToken);
        }

        await cache.RemoveAsync(CacheKeys.CourseDetails(course.Id), cancellationToken);

        return Unit.Value;
    }
}
