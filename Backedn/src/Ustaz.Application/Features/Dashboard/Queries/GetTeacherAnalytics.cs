using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Dashboard.Dtos;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Dashboard.Queries;

/// <summary>Revenue, student growth and group performance for the calling teacher.</summary>
public sealed record GetTeacherAnalyticsQuery : IRequest<TeacherAnalyticsDto>
{
    /// <summary>How many months of history to include in the trend series.</summary>
    public int Months { get; init; } = 12;
}

public sealed class GetTeacherAnalyticsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetTeacherAnalyticsQuery, TeacherAnalyticsDto>
{
    public async Task<TeacherAnalyticsDto> Handle(
        GetTeacherAnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();
        var months = Math.Clamp(query.Months, 1, 36);
        var utcNow = dateTimeProvider.UtcNow;

        var windowStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(-(months - 1));

        var monthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var revenueTrend = await BuildRevenueTrendAsync(teacherId, windowStart, months, cancellationToken);
        var studentGrowth = await BuildStudentGrowthAsync(teacherId, windowStart, months, cancellationToken);
        var groupPerformance = await BuildGroupPerformanceAsync(teacherId, cancellationToken);

        var profile = await context.TeacherProfiles
            .AsNoTracking()
            .Where(teacher => teacher.Id == teacherId)
            .Select(teacher => new { teacher.Rating, teacher.ReviewCount })
            .FirstAsync(cancellationToken);

        var totalStudents = await CountDistinctStudentsAsync(teacherId, null, cancellationToken);
        var newStudentsThisMonth = await CountDistinctStudentsAsync(teacherId, monthStart, cancellationToken);

        var totalGroups = await context.Groups.AsNoTracking()
            .CountAsync(group => group.TeacherId == teacherId, cancellationToken);

        var totalCourses = await context.Courses.AsNoTracking()
            .CountAsync(course => course.TeacherId == teacherId, cancellationToken);

        var inquiryCount = await context.Inquiries.AsNoTracking()
            .CountAsync(inquiry => inquiry.TeacherId == teacherId, cancellationToken);

        var convertedInquiries = await context.Inquiries.AsNoTracking()
            .CountAsync(inquiry => inquiry.TeacherId == teacherId && inquiry.Status == InquiryStatus.Converted, cancellationToken);

        return new TeacherAnalyticsDto
        {
            TotalRevenue = revenueTrend.Sum(point => point.TotalRevenue),
            MonthlyRevenue = revenueTrend.LastOrDefault()?.TotalRevenue ?? 0m,
            TotalStudents = totalStudents,
            NewStudentsThisMonth = newStudentsThisMonth,
            AverageRating = profile.Rating,
            TotalReviews = profile.ReviewCount,
            TotalGroups = totalGroups,
            TotalCourses = totalCourses,
            ConversionPercentage = inquiryCount == 0 ? 0 : convertedInquiries * 100 / inquiryCount,
            RevenueTrend = revenueTrend,
            StudentGrowth = studentGrowth,
            GroupPerformance = groupPerformance
        };
    }

    private async Task<List<RevenuePointDto>> BuildRevenueTrendAsync(
        Guid teacherId,
        DateTime windowStart,
        int months,
        CancellationToken cancellationToken)
    {
        var groupBuckets = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Group.TeacherId == teacherId
                && enrollment.Status != EnrollmentStatus.Cancelled
                && enrollment.EnrolledAt >= windowStart)
            .GroupBy(enrollment => new { enrollment.EnrolledAt.Year, enrollment.EnrolledAt.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Revenue = group.Sum(enrollment => enrollment.FinalPrice),
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var courseBuckets = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Course.TeacherId == teacherId
                && enrollment.Status != EnrollmentStatus.Cancelled
                && enrollment.EnrolledAt >= windowStart)
            .GroupBy(enrollment => new { enrollment.EnrolledAt.Year, enrollment.EnrolledAt.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Revenue = group.Sum(enrollment => enrollment.FinalPrice),
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var points = new List<RevenuePointDto>(months);

        // Months with no activity still appear, so the chart has no gaps.
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

    private async Task<List<StudentGrowthPointDto>> BuildStudentGrowthAsync(
        Guid teacherId,
        DateTime windowStart,
        int months,
        CancellationToken cancellationToken)
    {
        var enrollments = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Group.TeacherId == teacherId && enrollment.Status != EnrollmentStatus.Cancelled)
            .Select(enrollment => new { enrollment.StudentId, enrollment.EnrolledAt })
            .Union(context.CourseEnrollments
                .AsNoTracking()
                .Where(enrollment => enrollment.Course.TeacherId == teacherId && enrollment.Status != EnrollmentStatus.Cancelled)
                .Select(enrollment => new { enrollment.StudentId, enrollment.EnrolledAt }))
            .ToListAsync(cancellationToken);

        // A student counts once, on the date they first joined anything of this teacher.
        var firstSeen = enrollments
            .GroupBy(entry => entry.StudentId)
            .Select(group => group.Min(entry => entry.EnrolledAt))
            .ToList();

        var points = new List<StudentGrowthPointDto>(months);
        var cumulative = firstSeen.Count(date => date < windowStart);

        for (var offset = 0; offset < months; offset++)
        {
            var cursor = windowStart.AddMonths(offset);
            var next = cursor.AddMonths(1);

            var newStudents = firstSeen.Count(date => date >= cursor && date < next);
            cumulative += newStudents;

            points.Add(new StudentGrowthPointDto
            {
                Year = cursor.Year,
                Month = cursor.Month,
                Label = cursor.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                NewStudents = newStudents,
                CumulativeStudents = cumulative
            });
        }

        return points;
    }

    private Task<List<GroupPerformanceDto>> BuildGroupPerformanceAsync(
        Guid teacherId,
        CancellationToken cancellationToken) =>
        context.Groups
            .AsNoTracking()
            .Where(group => group.TeacherId == teacherId)
            .OrderByDescending(group => group.EnrolledStudentsCount)
            .Select(group => new GroupPerformanceDto
            {
                GroupId = group.Id,
                Name = group.Name,
                Capacity = group.Capacity,
                EnrolledStudentsCount = group.EnrolledStudentsCount,
                FillRatePercentage = group.Capacity == 0 ? 0 : group.EnrolledStudentsCount * 100 / group.Capacity,
                MonthlyRevenue = group.EnrolledStudentsCount * group.MonthlyPrice,
                Rating = group.Rating,
                ReviewCount = group.ReviewCount,
                IsPublished = group.IsPublished
            })
            .ToListAsync(cancellationToken);

    private Task<int> CountDistinctStudentsAsync(Guid teacherId, DateTime? since, CancellationToken cancellationToken)
    {
        var groupStudents = context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Group.TeacherId == teacherId && enrollment.Status != EnrollmentStatus.Cancelled)
            .Where(enrollment => since == null || enrollment.EnrolledAt >= since)
            .Select(enrollment => enrollment.StudentId);

        var courseStudents = context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Course.TeacherId == teacherId && enrollment.Status != EnrollmentStatus.Cancelled)
            .Where(enrollment => since == null || enrollment.EnrolledAt >= since)
            .Select(enrollment => enrollment.StudentId);

        return groupStudents.Union(courseStudents).CountAsync(cancellationToken);
    }
}
