using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Courses.Dtos;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Courses.Queries;

/// <summary>
/// Full course page. Video URLs are only returned for preview lessons, enrolled students,
/// the owning teacher and admins.
/// </summary>
public sealed record GetCourseByIdQuery(Guid CourseId) : IRequest<CourseDetailsDto>;

public sealed class GetCourseByIdQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetCourseByIdQuery, CourseDetailsDto>
{
    public async Task<CourseDetailsDto> Handle(GetCourseByIdQuery query, CancellationToken cancellationToken)
    {
        var details = await context.Courses
            .AsNoTracking()
            .Where(course => course.Id == query.CourseId)
            .Select(CourseProjections.ToDetailsWithoutContent)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Course", query.CourseId);

        var teacherId = await context.Courses
            .AsNoTracking()
            .Where(course => course.Id == query.CourseId)
            .Select(course => course.TeacherId)
            .FirstAsync(cancellationToken);

        var isOwner = currentUser.TeacherId is not null && currentUser.TeacherId == teacherId;

        if (!details.IsPublished && !isOwner && !currentUser.IsAdmin)
        {
            throw new NotFoundException("Course", query.CourseId);
        }

        var studentId = currentUser.StudentId;

        var enrollment = studentId is null
            ? null
            : await context.CourseEnrollments
                .AsNoTracking()
                .Where(entity => entity.CourseId == query.CourseId
                    && entity.StudentId == studentId.Value
                    && entity.Status != EnrollmentStatus.Cancelled)
                .Select(entity => new { entity.ProgressPercentage })
                .FirstOrDefaultAsync(cancellationToken);

        var isEnrolled = enrollment is not null;
        var hasFullAccess = isEnrolled || isOwner || currentUser.IsAdmin;

        var sections = await context.CourseSections
            .AsNoTracking()
            .Where(section => section.CourseId == query.CourseId)
            .OrderBy(section => section.SortOrder)
            .Select(section => new CourseSectionDto
            {
                Id = section.Id,
                CourseId = section.CourseId,
                Title = section.Title,
                Description = section.Description,
                SortOrder = section.SortOrder,
                LessonsCount = section.Lessons.Count(lesson => lesson.IsPublished || isOwner),
                DurationInMinutes = section.Lessons
                    .Where(lesson => lesson.IsPublished || isOwner)
                    .Sum(lesson => lesson.DurationInMinutes),
                Lessons = section.Lessons
                    .Where(lesson => lesson.IsPublished || isOwner)
                    .OrderBy(lesson => lesson.SortOrder)
                    .Select(lesson => new CourseLessonDto
                    {
                        Id = lesson.Id,
                        SectionId = lesson.SectionId,
                        Title = lesson.Title,
                        Description = lesson.Description,
                        VideoUrl = hasFullAccess || lesson.IsPreview ? lesson.VideoUrl : null,
                        DurationInMinutes = lesson.DurationInMinutes,
                        SortOrder = lesson.SortOrder,
                        IsPreview = lesson.IsPreview,
                        IsPublished = lesson.IsPublished,
                        IsLocked = !hasFullAccess && !lesson.IsPreview
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return details with
        {
            Sections = sections,
            IsEnrolled = isEnrolled,
            IsOwner = isOwner,
            ProgressPercentage = enrollment?.ProgressPercentage ?? 0
        };
    }
}

/// <summary>Curriculum outline of a course (sections and their lessons).</summary>
public sealed record GetCourseSectionsQuery(Guid CourseId) : IRequest<IReadOnlyList<CourseSectionDto>>;

public sealed class GetCourseSectionsQueryHandler(IMediator mediator)
    : IRequestHandler<GetCourseSectionsQuery, IReadOnlyList<CourseSectionDto>>
{
    public async Task<IReadOnlyList<CourseSectionDto>> Handle(
        GetCourseSectionsQuery query,
        CancellationToken cancellationToken)
    {
        var course = await mediator.Send(new GetCourseByIdQuery(query.CourseId), cancellationToken);
        return course.Sections;
    }
}
