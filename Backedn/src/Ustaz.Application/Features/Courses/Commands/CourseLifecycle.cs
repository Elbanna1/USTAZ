using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Common.Security;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Courses.Commands;

/// <summary>Publishes a course. Requires a verified teacher, a description and playable content.</summary>
public sealed record PublishCourseCommand(Guid CourseId) : IRequest<Unit>;

public sealed class PublishCourseCommandValidator : AbstractValidator<PublishCourseCommand>
{
    public PublishCourseCommandValidator() => RuleFor(command => command.CourseId).NotEmpty();
}

public sealed class PublishCourseCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache,
    INotificationDispatcher notifications) : IRequestHandler<PublishCourseCommand, Unit>
{
    public async Task<Unit> Handle(PublishCourseCommand command, CancellationToken cancellationToken)
    {
        var course = await context.Courses
            .Include(entity => entity.Teacher)
            .Include(entity => entity.Sections)
                .ThenInclude(section => section.Lessons)
            .FirstOrDefaultAsync(entity => entity.Id == command.CourseId, cancellationToken)
            ?? throw new NotFoundException("Course", command.CourseId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, course.TeacherId);

        course.Publish(dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        await notifications.SendAsync(
            course.Teacher.UserId,
            new NotificationRequest
            {
                Event = NotificationEvents.CoursePublished,
                TenantId = course.TenantId,
                ReferenceId = course.Id,
                DeduplicationKey = "course-published:" + course.Id,
                Values = new Dictionary<string, string>
                {
                    ["course"] = course.Title,
                    ["courseId"] = course.Id.ToString()
                }
            },
            cancellationToken);

        await cache.RemoveAsync(CacheKeys.CourseDetails(course.Id), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.CoursesTag, cancellationToken);

        return Unit.Value;
    }
}

public sealed record UnpublishCourseCommand(Guid CourseId) : IRequest<Unit>;

public sealed class UnpublishCourseCommandValidator : AbstractValidator<UnpublishCourseCommand>
{
    public UnpublishCourseCommandValidator() => RuleFor(command => command.CourseId).NotEmpty();
}

public sealed class UnpublishCourseCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache,
    INotificationDispatcher notifications) : IRequestHandler<UnpublishCourseCommand, Unit>
{
    public async Task<Unit> Handle(UnpublishCourseCommand command, CancellationToken cancellationToken)
    {
        var course = await context.Courses
            .FirstOrDefaultAsync(entity => entity.Id == command.CourseId, cancellationToken)
            ?? throw new NotFoundException("Course", command.CourseId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, course.TeacherId);

        course.Unpublish(dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        var teacherUserId = await context.TeacherProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == course.TeacherId)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (teacherUserId != Guid.Empty)
        {
            await notifications.SendAsync(
                teacherUserId,
                new NotificationRequest
                {
                    Event = NotificationEvents.CourseUnpublished,
                    TenantId = course.TenantId,
                    ReferenceId = course.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["course"] = course.Title,
                        ["courseId"] = course.Id.ToString()
                    }
                },
                cancellationToken);
        }

        await cache.RemoveAsync(CacheKeys.CourseDetails(course.Id), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.CoursesTag, cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Deletes a course. Courses that students already bought are unpublished rather than removed
/// so purchase history survives.
/// </summary>
public sealed record DeleteCourseCommand(Guid CourseId) : IRequest<Unit>;

public sealed class DeleteCourseCommandValidator : AbstractValidator<DeleteCourseCommand>
{
    public DeleteCourseCommandValidator() => RuleFor(command => command.CourseId).NotEmpty();
}

public sealed class DeleteCourseCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IMediaService mediaService,
    ICacheService cache) : IRequestHandler<DeleteCourseCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCourseCommand command, CancellationToken cancellationToken)
    {
        var course = await context.Courses
            .FirstOrDefaultAsync(entity => entity.Id == command.CourseId, cancellationToken)
            ?? throw new NotFoundException("Course", command.CourseId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, course.TeacherId);

        var hasActiveEnrollments = await context.CourseEnrollments.AnyAsync(
            enrollment => enrollment.CourseId == course.Id && enrollment.Status != EnrollmentStatus.Cancelled,
            cancellationToken);

        if (hasActiveEnrollments)
        {
            course.IsPublished = false;
            course.UpdatedAt = dateTimeProvider.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            throw new ConflictException(
                "This course has enrolled students, so it was unpublished instead of deleted. "
                + "Students keep access to the content they paid for.");
        }

        // Inquiries outlive the course they were about, so detach them first: the
        // foreign key is deliberately non-cascading.
        var relatedInquiries = await context.Inquiries
            .Where(inquiry => inquiry.CourseId == course.Id)
            .ToListAsync(cancellationToken);

        foreach (var inquiry in relatedInquiries)
        {
            inquiry.CourseId = null;
        }

        var thumbnailPublicId = course.ThumbnailPublicId;
        context.Courses.Remove(course);
        await context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(thumbnailPublicId))
        {
            await mediaService.DeleteAsync(thumbnailPublicId, cancellationToken);
        }

        await cache.RemoveAsync(CacheKeys.CourseDetails(course.Id), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.CoursesTag, cancellationToken);

        return Unit.Value;
    }
}
