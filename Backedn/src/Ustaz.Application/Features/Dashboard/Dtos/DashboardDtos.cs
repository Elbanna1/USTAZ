using Ustaz.Application.Features.Courses.Dtos;
using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Application.Features.Notifications;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Application.Features.Teachers.Dtos;

namespace Ustaz.Application.Features.Dashboard.Dtos;

public sealed record StudentDashboardDto
{
    public int ActiveGroupsCount { get; init; }

    public int ActiveCoursesCount { get; init; }

    public int CompletedCoursesCount { get; init; }

    public int SavedTeachersCount { get; init; }

    public int UnreadNotificationsCount { get; init; }

    public decimal TotalSpent { get; init; }

    public int AverageCourseProgress { get; init; }

    public IReadOnlyList<GroupListItemDto> ActiveGroups { get; init; } = [];

    public IReadOnlyList<CourseListItemDto> ActiveCourses { get; init; } = [];

    public IReadOnlyList<ScheduleSlotDto> UpcomingClasses { get; init; } = [];

    public IReadOnlyList<TeacherListItemDto> SavedTeachers { get; init; } = [];

    public IReadOnlyList<NotificationDto> RecentNotifications { get; init; } = [];
}

public sealed record CourseProgressDto
{
    public Guid EnrollmentId { get; init; }

    public Guid CourseId { get; init; }

    public string CourseTitle { get; init; } = string.Empty;

    public string? ThumbnailUrl { get; init; }

    public int ProgressPercentage { get; init; }

    public int LessonsCount { get; init; }

    public int DurationInMinutes { get; init; }

    public DateTime? LastAccessedAt { get; init; }

    public TeacherSummaryDto Teacher { get; init; } = new();
}

public sealed record ActivityItemDto
{
    public string Type { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public Guid? ReferenceId { get; init; }

    public string? ActionUrl { get; init; }

    public DateTime OccurredAt { get; init; }
}

public sealed record TeacherDashboardDto
{
    public int TotalStudents { get; init; }

    public int ActiveGroupsCount { get; init; }

    public int PublishedCoursesCount { get; init; }

    public int DraftGroupsCount { get; init; }

    public decimal MonthlyRevenue { get; init; }

    public decimal TotalRevenue { get; init; }

    public decimal AverageRating { get; init; }

    public int ReviewCount { get; init; }

    public int NewInquiriesCount { get; init; }

    public int UnreadNotificationsCount { get; init; }

    public bool IsVerified { get; init; }

    public IReadOnlyList<ScheduleSlotDto> UpcomingClasses { get; init; } = [];

    public IReadOnlyList<GroupPerformanceDto> TopGroups { get; init; } = [];

    public IReadOnlyList<ActivityItemDto> RecentActivity { get; init; } = [];
}

public sealed record GroupPerformanceDto
{
    public Guid GroupId { get; init; }

    public string Name { get; init; } = string.Empty;

    public int Capacity { get; init; }

    public int EnrolledStudentsCount { get; init; }

    public int FillRatePercentage { get; init; }

    public decimal MonthlyRevenue { get; init; }

    public decimal Rating { get; init; }

    public int ReviewCount { get; init; }

    public bool IsPublished { get; init; }
}

public sealed record RevenuePointDto
{
    public int Year { get; init; }

    public int Month { get; init; }

    public string Label { get; init; } = string.Empty;

    public decimal GroupRevenue { get; init; }

    public decimal CourseRevenue { get; init; }

    public decimal TotalRevenue { get; init; }

    public int EnrollmentsCount { get; init; }
}

public sealed record StudentGrowthPointDto
{
    public int Year { get; init; }

    public int Month { get; init; }

    public string Label { get; init; } = string.Empty;

    public int NewStudents { get; init; }

    public int CumulativeStudents { get; init; }
}

public sealed record TeacherAnalyticsDto
{
    public decimal TotalRevenue { get; init; }

    public decimal MonthlyRevenue { get; init; }

    public int TotalStudents { get; init; }

    public int NewStudentsThisMonth { get; init; }

    public decimal AverageRating { get; init; }

    public int TotalReviews { get; init; }

    public int TotalGroups { get; init; }

    public int TotalCourses { get; init; }

    public int ConversionPercentage { get; init; }

    public IReadOnlyList<RevenuePointDto> RevenueTrend { get; init; } = [];

    public IReadOnlyList<StudentGrowthPointDto> StudentGrowth { get; init; } = [];

    public IReadOnlyList<GroupPerformanceDto> GroupPerformance { get; init; } = [];
}

public sealed record AdminDashboardDto
{
    public int TotalUsers { get; init; }

    public int TotalTeachers { get; init; }

    public int VerifiedTeachers { get; init; }

    public int PendingVerifications { get; init; }

    public int TotalStudents { get; init; }

    public int TotalGroups { get; init; }

    public int PublishedGroups { get; init; }

    public int TotalCourses { get; init; }

    public int PublishedCourses { get; init; }

    public int TotalGroupEnrollments { get; init; }

    public int TotalCourseEnrollments { get; init; }

    public int TotalReviews { get; init; }

    public int PendingReviews { get; init; }

    public int NewInquiries { get; init; }

    public decimal PlatformRevenue { get; init; }

    public IReadOnlyList<RevenuePointDto> RevenueTrend { get; init; } = [];

    public IReadOnlyList<TeacherListItemDto> RecentTeachers { get; init; } = [];
}
