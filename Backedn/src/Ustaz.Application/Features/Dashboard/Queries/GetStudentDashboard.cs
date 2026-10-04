using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Courses.Queries;
using Ustaz.Application.Features.Dashboard.Dtos;
using Ustaz.Application.Features.Groups.Queries;
using Ustaz.Application.Features.Notifications;
using Ustaz.Application.Features.Schedule;
using Ustaz.Application.Features.Teachers.Queries;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Dashboard.Queries;

/// <summary>Everything the student home screen renders, in a single round trip.</summary>
public sealed record GetStudentDashboardQuery : IRequest<StudentDashboardDto>;

public sealed class GetStudentDashboardQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IMediator mediator) : IRequestHandler<GetStudentDashboardQuery, StudentDashboardDto>
{
    private const int PreviewSize = 5;

    public async Task<StudentDashboardDto> Handle(
        GetStudentDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();
        var userId = currentUser.RequireUserId();

        var activeGroups = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId && enrollment.Status == EnrollmentStatus.Active)
            .OrderByDescending(enrollment => enrollment.EnrolledAt)
            .Select(enrollment => enrollment.Group)
            .Take(PreviewSize)
            .Select(GroupProjections.ToListItem)
            .ToListAsync(cancellationToken);

        var activeCourses = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId && enrollment.Status == EnrollmentStatus.Active)
            .OrderByDescending(enrollment => enrollment.LastAccessedAt ?? enrollment.EnrolledAt)
            .Select(enrollment => enrollment.Course)
            .Take(PreviewSize)
            .Select(CourseProjections.ToListItem)
            .ToListAsync(cancellationToken);

        var savedTeachers = await context.TeacherFavorites
            .AsNoTracking()
            .Where(favorite => favorite.StudentId == studentId)
            .OrderByDescending(favorite => favorite.CreatedAt)
            .Select(favorite => favorite.Teacher)
            .Take(PreviewSize)
            .Select(TeacherProjections.ToListItem)
            .ToListAsync(cancellationToken);

        var recentNotifications = await context.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(PreviewSize)
            .Select(notification => new NotificationDto
            {
                Id = notification.Id,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type,
                IsRead = notification.IsRead,
                ActionUrl = notification.ActionUrl,
                ReferenceId = notification.ReferenceId,
                CreatedAt = notification.CreatedAt,
                ReadAt = notification.ReadAt
            })
            .ToListAsync(cancellationToken);

        var groupStats = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId)
            .GroupBy(enrollment => 1)
            .Select(group => new
            {
                Active = group.Count(enrollment => enrollment.Status == EnrollmentStatus.Active),
                Spent = group.Where(enrollment => enrollment.Status != EnrollmentStatus.Cancelled)
                    .Sum(enrollment => (decimal?)enrollment.FinalPrice) ?? 0m
            })
            .FirstOrDefaultAsync(cancellationToken);

        var courseStats = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId)
            .GroupBy(enrollment => 1)
            .Select(group => new
            {
                Active = group.Count(enrollment => enrollment.Status == EnrollmentStatus.Active),
                Completed = group.Count(enrollment => enrollment.Status == EnrollmentStatus.Completed),
                Spent = group.Where(enrollment => enrollment.Status != EnrollmentStatus.Cancelled)
                    .Sum(enrollment => (decimal?)enrollment.FinalPrice) ?? 0m,
                AverageProgress = group.Where(enrollment => enrollment.Status != EnrollmentStatus.Cancelled)
                    .Average(enrollment => (double?)enrollment.ProgressPercentage) ?? 0d
            })
            .FirstOrDefaultAsync(cancellationToken);

        var savedTeachersCount = await context.TeacherFavorites
            .AsNoTracking()
            .CountAsync(favorite => favorite.StudentId == studentId, cancellationToken);

        var unreadNotifications = await context.Notifications
            .AsNoTracking()
            .CountAsync(notification => notification.UserId == userId && !notification.IsRead, cancellationToken);

        var upcomingClasses = await mediator.Send(new GetUpcomingClassesQuery { Take = PreviewSize }, cancellationToken);

        return new StudentDashboardDto
        {
            ActiveGroupsCount = groupStats?.Active ?? 0,
            ActiveCoursesCount = courseStats?.Active ?? 0,
            CompletedCoursesCount = courseStats?.Completed ?? 0,
            SavedTeachersCount = savedTeachersCount,
            UnreadNotificationsCount = unreadNotifications,
            TotalSpent = (groupStats?.Spent ?? 0m) + (courseStats?.Spent ?? 0m),
            AverageCourseProgress = (int)Math.Round(courseStats?.AverageProgress ?? 0d),
            ActiveGroups = activeGroups,
            ActiveCourses = activeCourses,
            UpcomingClasses = upcomingClasses,
            SavedTeachers = savedTeachers,
            RecentNotifications = recentNotifications
        };
    }
}

/// <summary>Per-course learning progress for the calling student.</summary>
public sealed record GetStudentProgressQuery : IRequest<IReadOnlyList<CourseProgressDto>>;

public sealed class GetStudentProgressQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetStudentProgressQuery, IReadOnlyList<CourseProgressDto>>
{
    public async Task<IReadOnlyList<CourseProgressDto>> Handle(
        GetStudentProgressQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        return await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId && enrollment.Status != EnrollmentStatus.Cancelled)
            .OrderByDescending(enrollment => enrollment.LastAccessedAt ?? enrollment.EnrolledAt)
            .Select(enrollment => new CourseProgressDto
            {
                EnrollmentId = enrollment.Id,
                CourseId = enrollment.CourseId,
                CourseTitle = enrollment.Course.Title,
                ThumbnailUrl = enrollment.Course.ThumbnailUrl,
                ProgressPercentage = enrollment.ProgressPercentage,
                LessonsCount = enrollment.Course.LessonsCount,
                DurationInMinutes = enrollment.Course.DurationInMinutes,
                LastAccessedAt = enrollment.LastAccessedAt,
                Teacher = new Shared.Dtos.TeacherSummaryDto
                {
                    Id = enrollment.Course.Teacher.Id,
                    UserId = enrollment.Course.Teacher.UserId,
                    DisplayName = enrollment.Course.Teacher.User.DisplayName,
                    ProfileImageUrl = enrollment.Course.Teacher.ProfileImageUrl,
                    Rating = enrollment.Course.Teacher.Rating,
                    ReviewCount = enrollment.Course.Teacher.ReviewCount,
                    IsVerified = enrollment.Course.Teacher.IsVerified
                }
            })
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Recent enrollments, reviews and notifications for the calling student.</summary>
public sealed record GetStudentRecentActivityQuery : IRequest<IReadOnlyList<ActivityItemDto>>
{
    public int Take { get; init; } = 10;
}

public sealed class GetStudentRecentActivityQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetStudentRecentActivityQuery, IReadOnlyList<ActivityItemDto>>
{
    public async Task<IReadOnlyList<ActivityItemDto>> Handle(
        GetStudentRecentActivityQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();
        var take = Math.Clamp(query.Take, 1, 50);

        var groupActivity = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId)
            .OrderByDescending(enrollment => enrollment.EnrolledAt)
            .Take(take)
            .Select(enrollment => new ActivityItemDto
            {
                Type = "GroupEnrollment",
                Title = "اشتراك في مجموعة",
                Description = enrollment.Group.Name,
                ReferenceId = enrollment.Id,
                ActionUrl = "/groups/" + enrollment.GroupId,
                OccurredAt = enrollment.EnrolledAt
            })
            .ToListAsync(cancellationToken);

        var courseActivity = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId)
            .OrderByDescending(enrollment => enrollment.EnrolledAt)
            .Take(take)
            .Select(enrollment => new ActivityItemDto
            {
                Type = "CourseEnrollment",
                Title = "اشتراك في كورس",
                Description = enrollment.Course.Title,
                ReferenceId = enrollment.Id,
                ActionUrl = "/courses/" + enrollment.CourseId,
                OccurredAt = enrollment.EnrolledAt
            })
            .ToListAsync(cancellationToken);

        var reviewActivity = await context.Reviews
            .AsNoTracking()
            .Where(review => review.StudentId == studentId)
            .OrderByDescending(review => review.CreatedAt)
            .Take(take)
            .Select(review => new ActivityItemDto
            {
                Type = "Review",
                Title = "تقييم",
                Description = "قمت بتقييم " + review.Teacher.User.DisplayName + " بـ " + review.Rating + " نجوم.",
                ReferenceId = review.Id,
                ActionUrl = "/teachers/" + review.TeacherId,
                OccurredAt = review.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. groupActivity
                .Concat(courseActivity)
                .Concat(reviewActivity)
                .OrderByDescending(item => item.OccurredAt)
                .Take(take)
        ];
    }
}
