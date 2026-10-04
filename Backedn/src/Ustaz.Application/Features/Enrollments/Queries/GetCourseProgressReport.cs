using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Enrollments.Dtos;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Enrollments.Queries;

/// <summary>
/// The caller's lesson-by-lesson progress through one course. Only the student's own
/// enrollment is ever readable: the course id is resolved against their enrollment, so a
/// tampered id yields a 404 rather than another learner's record.
/// </summary>
public sealed record GetCourseProgressReportQuery(Guid CourseId) : IRequest<CourseProgressReportDto>;

public sealed class GetCourseProgressReportQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetCourseProgressReportQuery, CourseProgressReportDto>
{
    public async Task<CourseProgressReportDto> Handle(
        GetCourseProgressReportQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        var enrollment = await context.CourseEnrollments
            .AsNoTracking()
            .Where(entity => entity.CourseId == query.CourseId && entity.StudentId == studentId)
            .Select(entity => new
            {
                entity.Id,
                entity.CourseId,
                CourseTitle = entity.Course.Title,
                entity.Status,
                entity.ProgressPercentage,
                entity.CompletedAt,
                entity.LastAccessedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("CourseEnrollment", query.CourseId);

        if (enrollment.Status == EnrollmentStatus.Cancelled)
        {
            throw new ForbiddenAccessException("This enrollment has been cancelled.");
        }

        // The syllabus and this learner's progress rows are two flat reads. Projecting the
        // progress columns inline would emit one correlated subquery per column per lesson.
        var syllabus = await context.CourseLessons
            .AsNoTracking()
            .Where(lesson => lesson.Section.CourseId == query.CourseId && lesson.IsPublished)
            .OrderBy(lesson => lesson.Section.SortOrder)
            .ThenBy(lesson => lesson.SortOrder)
            .Select(lesson => new
            {
                lesson.Id,
                lesson.SectionId,
                SectionTitle = lesson.Section.Title,
                lesson.Title,
                lesson.SortOrder,
                lesson.DurationInMinutes,
                lesson.IsPreview
            })
            .ToListAsync(cancellationToken);

        var progressByLesson = await context.CourseLessonProgress
            .AsNoTracking()
            .Where(progress => progress.CourseEnrollmentId == enrollment.Id)
            .Select(progress => new
            {
                progress.LessonId,
                progress.IsCompleted,
                progress.WatchedSeconds,
                progress.CompletedAt,
                progress.LastAccessedAt
            })
            .ToDictionaryAsync(progress => progress.LessonId, cancellationToken);

        var lessons = syllabus
            .Select(lesson =>
            {
                progressByLesson.TryGetValue(lesson.Id, out var progress);

                return new LessonProgressItemDto
                {
                    LessonId = lesson.Id,
                    SectionId = lesson.SectionId,
                    SectionTitle = lesson.SectionTitle,
                    Title = lesson.Title,
                    SortOrder = lesson.SortOrder,
                    DurationInMinutes = lesson.DurationInMinutes,
                    IsPreview = lesson.IsPreview,
                    IsCompleted = progress?.IsCompleted ?? false,
                    WatchedSeconds = progress?.WatchedSeconds ?? 0,
                    CompletedAt = progress?.CompletedAt,
                    LastAccessedAt = progress?.LastAccessedAt
                };
            })
            .ToList();

        // Where the learner actually is: the first unfinished lesson in syllabus order, and
        // the one after it. Both are null once the course is finished.
        var currentLesson = lessons.FirstOrDefault(lesson => !lesson.IsCompleted);
        var currentIndex = currentLesson is null ? -1 : lessons.IndexOf(currentLesson);

        var nextLesson = currentIndex >= 0 && currentIndex + 1 < lessons.Count
            ? lessons[currentIndex + 1]
            : null;

        return new CourseProgressReportDto
        {
            CourseId = enrollment.CourseId,
            EnrollmentId = enrollment.Id,
            CourseTitle = enrollment.CourseTitle,
            TotalLessons = lessons.Count,
            CompletedLessons = lessons.Count(lesson => lesson.IsCompleted),
            ProgressPercentage = enrollment.ProgressPercentage,
            Status = enrollment.Status,
            CompletedAt = enrollment.CompletedAt,
            LastAccessedAt = enrollment.LastAccessedAt,
            CurrentLessonId = currentLesson?.LessonId,
            CurrentLessonTitle = currentLesson?.Title,
            NextLessonId = nextLesson?.LessonId,
            NextLessonTitle = nextLesson?.Title,
            WatchedSeconds = lessons.Sum(lesson => (long)lesson.WatchedSeconds),
            Lessons = lessons
        };
    }
}
