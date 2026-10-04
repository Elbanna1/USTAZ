using Ustaz.Application.Common.Models;

namespace Ustaz.Application.Features.Dashboard.Dtos;

/// <summary>The window a set of figures was computed over, echoed back so the UI can label it.</summary>
public sealed record AnalyticsWindowDto
{
    public AnalyticsPeriod Period { get; init; }

    public string PeriodName => Period.ToString();

    /// <summary>Inclusive start. Null for <c>AllTime</c>, which has no start.</summary>
    public DateTime? FromUtc { get; init; }

    /// <summary>Exclusive end.</summary>
    public DateTime ToUtc { get; init; }

    public static AnalyticsWindowDto From(AnalyticsRange range) => new()
    {
        Period = range.Period,
        FromUtc = range.IsAllTime ? null : range.FromUtc,
        ToUtc = range.ToUtc
    };
}

/// <summary>
/// A count alongside the same count for the preceding window of equal length, so the UI can
/// show a trend arrow without a second request. <see cref="ChangePercentage"/> is null when
/// there is no baseline to compare against.
/// </summary>
public sealed record TrendDto
{
    public decimal Current { get; init; }

    /// <summary>Null for <c>AllTime</c>, which has no preceding window.</summary>
    public decimal? Previous { get; init; }

    public decimal? Change => Previous is null ? null : Current - Previous;

    public decimal? ChangePercentage => Previous is null or 0m
        ? null
        : Math.Round((Current - Previous.Value) / Previous.Value * 100m, 2, MidpointRounding.AwayFromZero);

    public static TrendDto Of(decimal current, decimal? previous) =>
        new() { Current = current, Previous = previous };
}

/// <summary>
/// The workspace KPI strip for a chosen date window: who is learning, what has been published,
/// how the cohort is performing and what it earned. Complements <c>/teacher/analytics</c>,
/// which returns the long-run monthly trend series.
/// </summary>
public sealed record TeacherAnalyticsOverviewDto
{
    public AnalyticsWindowDto Window { get; init; } = new();

    public TeacherStudentMetricsDto Students { get; init; } = new();

    public TeacherCatalogueMetricsDto Catalogue { get; init; } = new();

    public TeacherTeachingMetricsDto Teaching { get; init; } = new();

    public TeacherPerformanceMetricsDto Performance { get; init; } = new();

    public TeacherRevenueMetricsDto Revenue { get; init; } = new();
}

public sealed record TeacherStudentMetricsDto
{
    /// <summary>Distinct students ever enrolled in the workspace, whatever the window.</summary>
    public int Total { get; init; }

    /// <summary>
    /// Students with at least one recorded action inside the window: a lesson watched, work
    /// handed in, an exam sat, or presence marked. Absence of activity is not absence of a
    /// student, so this is deliberately narrower than <see cref="Total"/>.
    /// </summary>
    public TrendDto Active { get; init; } = new();

    /// <summary>Students whose first enrollment anywhere in the workspace fell in the window.</summary>
    public TrendDto New { get; init; } = new();

    /// <summary>Enrollments cancelled inside the window.</summary>
    public TrendDto Churned { get; init; } = new();
}

public sealed record TeacherCatalogueMetricsDto
{
    public int Groups { get; init; }

    public int PublishedGroups { get; init; }

    public int Courses { get; init; }

    public int PublishedCourses { get; init; }

    /// <summary>Published lessons across every course in the workspace.</summary>
    public int PublishedLessons { get; init; }

    /// <summary>Distinct subjects the workspace actually teaches.</summary>
    public int Subjects { get; init; }
}

public sealed record TeacherTeachingMetricsDto
{
    /// <summary>Exams published in the window.</summary>
    public TrendDto ExamsPublished { get; init; } = new();

    /// <summary>Homework published in the window.</summary>
    public TrendDto HomeworkPublished { get; init; } = new();

    /// <summary>Attendance sessions held in the window.</summary>
    public TrendDto AttendanceSessions { get; init; } = new();

    /// <summary>Lessons marked complete by students in the window.</summary>
    public TrendDto LessonsCompleted { get; init; } = new();

    /// <summary>Submissions still waiting to be marked, at this instant rather than in the window.</summary>
    public int HomeworkAwaitingGrading { get; init; }

    /// <summary>Online sittings with an essay still waiting, at this instant.</summary>
    public int ExamAttemptsAwaitingGrading { get; init; }
}

public sealed record TeacherPerformanceMetricsDto
{
    /// <summary>Mean exam percentage in the window; absentees excluded. Null with no marks.</summary>
    public decimal? ExamAveragePercentage { get; init; }

    public decimal? ExamPassRate { get; init; }

    /// <summary>Mean homework percentage across submissions graded in the window.</summary>
    public decimal? HomeworkAveragePercentage { get; init; }

    /// <summary>Share of homework published in the window that was handed in at all.</summary>
    public decimal? HomeworkSubmissionRate { get; init; }

    /// <summary>Present or late, over every attendance record in the window.</summary>
    public decimal? AttendanceRate { get; init; }

    /// <summary>Share of course enrollments that have reached Completed, all time.</summary>
    public decimal? CourseCompletionRate { get; init; }

    /// <summary>Mean progress percentage across active course enrollments, all time.</summary>
    public decimal? AverageCourseProgress { get; init; }
}

public sealed record TeacherRevenueMetricsDto
{
    /// <summary>Enrollment revenue booked in the window, group and course combined.</summary>
    public TrendDto Total { get; init; } = new();

    public decimal GroupRevenue { get; init; }

    public decimal CourseRevenue { get; init; }

    public TrendDto Enrollments { get; init; } = new();

    /// <summary>Mean revenue per enrollment in the window.</summary>
    public decimal? AverageOrderValue { get; init; }

    public string Currency { get; init; } = "EGP";
}
