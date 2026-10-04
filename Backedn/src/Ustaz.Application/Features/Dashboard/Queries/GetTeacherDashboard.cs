using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Dashboard.Dtos;
using Ustaz.Application.Features.Schedule;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Dashboard.Queries;

/// <summary>Everything the teacher home screen renders, in a single round trip.</summary>
public sealed record GetTeacherDashboardQuery : IRequest<TeacherDashboardDto>;

public sealed class GetTeacherDashboardQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IMediator mediator) : IRequestHandler<GetTeacherDashboardQuery, TeacherDashboardDto>
{
    private const int PreviewSize = 5;

    public async Task<TeacherDashboardDto> Handle(
        GetTeacherDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();
        var userId = currentUser.RequireUserId();
        var utcNow = dateTimeProvider.UtcNow;
        var monthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var profile = await context.TeacherProfiles
            .AsNoTracking()
            .Where(teacher => teacher.Id == teacherId)
            .Select(teacher => new { teacher.Rating, teacher.ReviewCount, teacher.IsVerified, teacher.StudentsCount })
            .FirstAsync(cancellationToken);

        var groupStats = await context.Groups
            .AsNoTracking()
            .Where(group => group.TeacherId == teacherId)
            .GroupBy(group => 1)
            .Select(g => new
            {
                Active = g.Count(group => group.IsPublished && group.IsActive),
                Drafts = g.Count(group => !group.IsPublished)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var publishedCourses = await context.Courses
            .AsNoTracking()
            .CountAsync(course => course.TeacherId == teacherId && course.IsPublished, cancellationToken);

        var groupRevenue = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Group.TeacherId == teacherId && enrollment.Status != EnrollmentStatus.Cancelled)
            .GroupBy(enrollment => 1)
            .Select(g => new
            {
                Total = g.Sum(enrollment => (decimal?)enrollment.FinalPrice) ?? 0m,
                Month = g.Where(enrollment => enrollment.EnrolledAt >= monthStart)
                    .Sum(enrollment => (decimal?)enrollment.FinalPrice) ?? 0m
            })
            .FirstOrDefaultAsync(cancellationToken);

        var courseRevenue = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Course.TeacherId == teacherId && enrollment.Status != EnrollmentStatus.Cancelled)
            .GroupBy(enrollment => 1)
            .Select(g => new
            {
                Total = g.Sum(enrollment => (decimal?)enrollment.FinalPrice) ?? 0m,
                Month = g.Where(enrollment => enrollment.EnrolledAt >= monthStart)
                    .Sum(enrollment => (decimal?)enrollment.FinalPrice) ?? 0m
            })
            .FirstOrDefaultAsync(cancellationToken);

        var distinctStudents = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Group.TeacherId == teacherId && enrollment.Status != EnrollmentStatus.Cancelled)
            .Select(enrollment => enrollment.StudentId)
            .Union(context.CourseEnrollments
                .AsNoTracking()
                .Where(enrollment => enrollment.Course.TeacherId == teacherId && enrollment.Status != EnrollmentStatus.Cancelled)
                .Select(enrollment => enrollment.StudentId))
            .CountAsync(cancellationToken);

        var newInquiries = await context.Inquiries
            .AsNoTracking()
            .CountAsync(inquiry => inquiry.TeacherId == teacherId && inquiry.Status == InquiryStatus.New, cancellationToken);

        var unreadNotifications = await context.Notifications
            .AsNoTracking()
            .CountAsync(notification => notification.UserId == userId && !notification.IsRead, cancellationToken);

        var topGroups = await context.Groups
            .AsNoTracking()
            .Where(group => group.TeacherId == teacherId)
            .OrderByDescending(group => group.EnrolledStudentsCount)
            .Take(PreviewSize)
            .Select(group => new GroupPerformanceDto
            {
                GroupId = group.Id,
                Name = group.Name,
                Capacity = group.Capacity,
                EnrolledStudentsCount = group.EnrolledStudentsCount,
                FillRatePercentage = group.Capacity == 0
                    ? 0
                    : group.EnrolledStudentsCount * 100 / group.Capacity,
                MonthlyRevenue = group.EnrolledStudentsCount * group.MonthlyPrice,
                Rating = group.Rating,
                ReviewCount = group.ReviewCount,
                IsPublished = group.IsPublished
            })
            .ToListAsync(cancellationToken);

        var recentActivity = await LoadRecentActivityAsync(teacherId, cancellationToken);
        var upcomingClasses = await mediator.Send(new GetUpcomingClassesQuery { Take = PreviewSize }, cancellationToken);

        return new TeacherDashboardDto
        {
            TotalStudents = distinctStudents,
            ActiveGroupsCount = groupStats?.Active ?? 0,
            DraftGroupsCount = groupStats?.Drafts ?? 0,
            PublishedCoursesCount = publishedCourses,
            MonthlyRevenue = (groupRevenue?.Month ?? 0m) + (courseRevenue?.Month ?? 0m),
            TotalRevenue = (groupRevenue?.Total ?? 0m) + (courseRevenue?.Total ?? 0m),
            AverageRating = profile.Rating,
            ReviewCount = profile.ReviewCount,
            NewInquiriesCount = newInquiries,
            UnreadNotificationsCount = unreadNotifications,
            IsVerified = profile.IsVerified,
            UpcomingClasses = upcomingClasses,
            TopGroups = topGroups,
            RecentActivity = recentActivity
        };
    }

    private async Task<IReadOnlyList<ActivityItemDto>> LoadRecentActivityAsync(
        Guid teacherId,
        CancellationToken cancellationToken)
    {
        var enrollments = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Group.TeacherId == teacherId)
            .OrderByDescending(enrollment => enrollment.EnrolledAt)
            .Take(PreviewSize)
            .Select(enrollment => new ActivityItemDto
            {
                Type = "GroupEnrollment",
                Title = "اشتراك جديد",
                Description = enrollment.Student.User.DisplayName + " انضم إلى " + enrollment.Group.Name,
                ReferenceId = enrollment.Id,
                ActionUrl = "/teacher/groups/" + enrollment.GroupId,
                OccurredAt = enrollment.EnrolledAt
            })
            .ToListAsync(cancellationToken);

        var reviews = await context.Reviews
            .AsNoTracking()
            .Where(review => review.TeacherId == teacherId)
            .OrderByDescending(review => review.CreatedAt)
            .Take(PreviewSize)
            .Select(review => new ActivityItemDto
            {
                Type = "Review",
                Title = "تقييم جديد",
                Description = review.Student.User.DisplayName + " قيّمك بـ " + review.Rating + " نجوم",
                ReferenceId = review.Id,
                ActionUrl = "/teacher/reviews",
                OccurredAt = review.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var inquiries = await context.Inquiries
            .AsNoTracking()
            .Where(inquiry => inquiry.TeacherId == teacherId)
            .OrderByDescending(inquiry => inquiry.CreatedAt)
            .Take(PreviewSize)
            .Select(inquiry => new ActivityItemDto
            {
                Type = "Inquiry",
                Title = "استفسار جديد",
                Description = inquiry.Name + " أرسل استفساراً",
                ReferenceId = inquiry.Id,
                ActionUrl = "/teacher/inquiries/" + inquiry.Id,
                OccurredAt = inquiry.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. enrollments
                .Concat(reviews)
                .Concat(inquiries)
                .OrderByDescending(item => item.OccurredAt)
                .Take(10)
        ];
    }
}
