using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Admin.Dtos;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Dashboard.Dtos;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Application.Features.Teachers.Queries;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Admin.Queries;

/// <summary>Platform-wide counters and trends for the admin home screen.</summary>
public sealed record GetAdminDashboardQuery : IRequest<AdminDashboardDto>
{
    public int Months { get; init; } = 12;
}

public sealed class GetAdminDashboardQueryHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    public async Task<AdminDashboardDto> Handle(GetAdminDashboardQuery query, CancellationToken cancellationToken)
    {
        var months = Math.Clamp(query.Months, 1, 36);
        var utcNow = dateTimeProvider.UtcNow;
        var windowStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));

        var totalUsers = await context.Users.AsNoTracking().CountAsync(cancellationToken);

        var teacherStats = await context.TeacherProfiles
            .AsNoTracking()
            .GroupBy(teacher => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Verified = group.Count(teacher => teacher.IsVerified),
                Pending = group.Count(teacher =>
                    teacher.VerificationStatus == VerificationStatus.Pending
                    || teacher.VerificationStatus == VerificationStatus.UnderReview)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalStudents = await context.StudentProfiles.AsNoTracking().CountAsync(cancellationToken);

        var groupStats = await context.Groups
            .AsNoTracking()
            .GroupBy(group => 1)
            .Select(g => new { Total = g.Count(), Published = g.Count(group => group.IsPublished) })
            .FirstOrDefaultAsync(cancellationToken);

        var courseStats = await context.Courses
            .AsNoTracking()
            .GroupBy(course => 1)
            .Select(g => new { Total = g.Count(), Published = g.Count(course => course.IsPublished) })
            .FirstOrDefaultAsync(cancellationToken);

        var groupEnrollmentStats = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Status != EnrollmentStatus.Cancelled)
            .GroupBy(enrollment => 1)
            .Select(g => new { Count = g.Count(), Revenue = g.Sum(e => (decimal?)e.FinalPrice) ?? 0m })
            .FirstOrDefaultAsync(cancellationToken);

        var courseEnrollmentStats = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Status != EnrollmentStatus.Cancelled)
            .GroupBy(enrollment => 1)
            .Select(g => new { Count = g.Count(), Revenue = g.Sum(e => (decimal?)e.FinalPrice) ?? 0m })
            .FirstOrDefaultAsync(cancellationToken);

        var reviewStats = await context.Reviews
            .AsNoTracking()
            .GroupBy(review => 1)
            .Select(g => new { Total = g.Count(), Pending = g.Count(review => !review.IsApproved) })
            .FirstOrDefaultAsync(cancellationToken);

        var newInquiries = await context.Inquiries
            .AsNoTracking()
            .CountAsync(inquiry => inquiry.Status == InquiryStatus.New, cancellationToken);

        var recentTeachers = await context.TeacherProfiles
            .AsNoTracking()
            .OrderByDescending(teacher => teacher.CreatedAt)
            .Take(5)
            .Select(TeacherProjections.ToListItem)
            .ToListAsync(cancellationToken);

        var revenueTrend = await BuildRevenueTrendAsync(windowStart, months, cancellationToken);

        return new AdminDashboardDto
        {
            TotalUsers = totalUsers,
            TotalTeachers = teacherStats?.Total ?? 0,
            VerifiedTeachers = teacherStats?.Verified ?? 0,
            PendingVerifications = teacherStats?.Pending ?? 0,
            TotalStudents = totalStudents,
            TotalGroups = groupStats?.Total ?? 0,
            PublishedGroups = groupStats?.Published ?? 0,
            TotalCourses = courseStats?.Total ?? 0,
            PublishedCourses = courseStats?.Published ?? 0,
            TotalGroupEnrollments = groupEnrollmentStats?.Count ?? 0,
            TotalCourseEnrollments = courseEnrollmentStats?.Count ?? 0,
            TotalReviews = reviewStats?.Total ?? 0,
            PendingReviews = reviewStats?.Pending ?? 0,
            NewInquiries = newInquiries,
            PlatformRevenue = (groupEnrollmentStats?.Revenue ?? 0m) + (courseEnrollmentStats?.Revenue ?? 0m),
            RevenueTrend = revenueTrend,
            RecentTeachers = recentTeachers
        };
    }

    private async Task<List<RevenuePointDto>> BuildRevenueTrendAsync(
        DateTime windowStart,
        int months,
        CancellationToken cancellationToken)
    {
        var groupBuckets = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Status != EnrollmentStatus.Cancelled && enrollment.EnrolledAt >= windowStart)
            .GroupBy(enrollment => new { enrollment.EnrolledAt.Year, enrollment.EnrolledAt.Month })
            .Select(group => new { group.Key.Year, group.Key.Month, Revenue = group.Sum(e => e.FinalPrice), Count = group.Count() })
            .ToListAsync(cancellationToken);

        var courseBuckets = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Status != EnrollmentStatus.Cancelled && enrollment.EnrolledAt >= windowStart)
            .GroupBy(enrollment => new { enrollment.EnrolledAt.Year, enrollment.EnrolledAt.Month })
            .Select(group => new { group.Key.Year, group.Key.Month, Revenue = group.Sum(e => e.FinalPrice), Count = group.Count() })
            .ToListAsync(cancellationToken);

        var points = new List<RevenuePointDto>(months);

        for (var offset = 0; offset < months; offset++)
        {
            var cursor = windowStart.AddMonths(offset);
            var groupBucket = groupBuckets.FirstOrDefault(b => b.Year == cursor.Year && b.Month == cursor.Month);
            var courseBucket = courseBuckets.FirstOrDefault(b => b.Year == cursor.Year && b.Month == cursor.Month);

            var groupRevenue = groupBucket?.Revenue ?? 0m;
            var courseRevenue = courseBucket?.Revenue ?? 0m;

            points.Add(new RevenuePointDto
            {
                Year = cursor.Year,
                Month = cursor.Month,
                Label = cursor.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                GroupRevenue = groupRevenue,
                CourseRevenue = courseRevenue,
                TotalRevenue = groupRevenue + courseRevenue,
                EnrollmentsCount = (groupBucket?.Count ?? 0) + (courseBucket?.Count ?? 0)
            });
        }

        return points;
    }
}

public sealed record AdminUserDto
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? PhoneNumber { get; init; }

    public string? City { get; init; }

    public bool IsActive { get; init; }

    public bool EmailConfirmed { get; init; }

    public bool IsVerified { get; init; }

    public Guid? TeacherId { get; init; }

    public Guid? StudentId { get; init; }

    public string? GradeName { get; init; }

    public string? SchoolName { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? LastLoginAt { get; init; }
}

/// <summary>Admin listing of student accounts.</summary>
public sealed record GetAdminStudentsQuery : PagedRequest, IRequest<PaginatedList<AdminUserDto>>
{
    public bool? IsActive { get; init; }

    public Guid? GradeId { get; init; }
}

public sealed class GetAdminStudentsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAdminStudentsQuery, PaginatedList<AdminUserDto>>
{
    public Task<PaginatedList<AdminUserDto>> Handle(GetAdminStudentsQuery query, CancellationToken cancellationToken)
    {
        var search = query.Search?.Trim();

        return context.StudentProfiles
            .AsNoTracking()
            .WhereIf(query.IsActive.HasValue, student => student.User.IsActive == query.IsActive!.Value)
            .WhereIf(query.GradeId.HasValue, student => student.GradeId == query.GradeId!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(search), student =>
                student.User.DisplayName.Contains(search!)
                || (student.User.Email != null && student.User.Email.Contains(search!)))
            .OrderByDescending(student => student.CreatedAt)
            .Select(student => new AdminUserDto
            {
                Id = student.UserId,
                Email = student.User.Email ?? string.Empty,
                DisplayName = student.User.DisplayName,
                PhoneNumber = student.PhoneNumber ?? student.User.PhoneNumber,
                City = student.City,
                IsActive = student.User.IsActive,
                EmailConfirmed = student.User.EmailConfirmed,
                StudentId = student.Id,
                GradeName = student.Grade != null ? student.Grade.NameAr : null,
                SchoolName = student.SchoolName,
                CreatedAt = student.CreatedAt,
                LastLoginAt = student.User.LastLoginAt
            })
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>Admin listing of teacher accounts, including unverified ones.</summary>
public sealed record GetAdminTeachersQuery : PagedRequest, IRequest<PaginatedList<AdminTeacherListItemDto>>
{
    public VerificationStatus? VerificationStatus { get; init; }

    public bool? IsActive { get; init; }

    public bool? IsFeatured { get; init; }

    /// <summary>Puts the people who have waited longest at the top of the review queue.</summary>
    public bool OldestWaitingFirst { get; init; }
}

public sealed class GetAdminTeachersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAdminTeachersQuery, PaginatedList<AdminTeacherListItemDto>>
{
    public Task<PaginatedList<AdminTeacherListItemDto>> Handle(
        GetAdminTeachersQuery query,
        CancellationToken cancellationToken)
    {
        var search = query.Search?.Trim();

        var teachers = context.TeacherProfiles
            .AsNoTracking()
            .WhereIf(query.VerificationStatus.HasValue,
                teacher => teacher.VerificationStatus == query.VerificationStatus!.Value)
            .WhereIf(query.IsActive.HasValue, teacher => teacher.User.IsActive == query.IsActive!.Value)
            .WhereIf(query.IsFeatured.HasValue, teacher => teacher.IsFeatured == query.IsFeatured!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(search), teacher =>
                teacher.User.DisplayName.Contains(search!)
                || (teacher.User.Email != null && teacher.User.Email.Contains(search!)));

        // A review queue is worked oldest-first; every other listing reads newest-first.
        teachers = query.OldestWaitingFirst
            ? teachers.OrderBy(teacher => teacher.VerificationSubmittedAt ?? teacher.CreatedAt)
            : teachers.OrderByDescending(teacher => teacher.CreatedAt);

        return teachers
            .Select(teacher => new AdminTeacherListItemDto
            {
                Id = teacher.Id,
                UserId = teacher.UserId,
                DisplayName = teacher.User.DisplayName,
                Email = teacher.User.Email,
                Headline = teacher.Headline,
                City = teacher.City,
                YearsOfExperience = teacher.YearsOfExperience,
                StudentsCount = teacher.StudentsCount,
                Rating = teacher.Rating,
                ReviewCount = teacher.ReviewCount,
                IsFeatured = teacher.IsFeatured,
                IsActive = teacher.User.IsActive,
                IsVerified = teacher.IsVerified,
                VerificationStatus = teacher.VerificationStatus,
                VerificationSubmittedAt = teacher.VerificationSubmittedAt,
                VerifiedAt = teacher.VerifiedAt,
                VerificationNotes = teacher.VerificationNotes,
                GroupsCount = teacher.Groups.Count,
                CoursesCount = teacher.Courses.Count,
                CreatedAt = teacher.CreatedAt
            })
            .ToPaginatedListAsync(query, cancellationToken);
    }
}
