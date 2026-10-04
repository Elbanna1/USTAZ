namespace Ustaz.Application.Features.Dashboard.Dtos;

/// <summary>
/// One student's academic standing, assembled from the marks and activity actually recorded.
/// Every average is nullable: no marks yet is a real state and reads as null, never as zero,
/// so the UI can show an empty state instead of implying a failing grade.
/// </summary>
public sealed record StudentAnalyticsDto
{
    public Guid StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public AnalyticsWindowDto Window { get; init; } = new();

    /// <summary>
    /// The headline figure: exam, homework and attendance combined, weighted 50/30/20 and
    /// renormalised over whichever of the three exist. Null until at least one of them does.
    /// </summary>
    public decimal? OverallScore { get; init; }

    public decimal? ExamAverage { get; init; }

    public decimal? HomeworkAverage { get; init; }

    public decimal? AttendancePercentage { get; init; }

    /// <summary>Share of enrolled courses finished.</summary>
    public decimal? CourseCompletion { get; init; }

    /// <summary>Share of published lessons completed across every enrolled course.</summary>
    public decimal? LessonCompletion { get; init; }

    public StudentEngagementDto Engagement { get; init; } = new();

    public StudentWorkloadDto Workload { get; init; } = new();

    /// <summary>Subjects the student is doing well in, strongest first.</summary>
    public IReadOnlyList<StudentSubjectStandingDto> StrongSubjects { get; init; } = [];

    /// <summary>Subjects the student is struggling with, weakest first.</summary>
    public IReadOnlyList<StudentSubjectStandingDto> WeakSubjects { get; init; } = [];

    /// <summary>Every subject with a mark, for the full breakdown.</summary>
    public IReadOnlyList<StudentSubjectStandingDto> SubjectBreakdown { get; init; } = [];
}

public sealed record StudentEngagementDto
{
    /// <summary>
    /// Consecutive days up to and including today with at least one recorded learning action.
    /// Zero when nothing was done today or yesterday, so the count never flatters a lapse.
    /// </summary>
    public int LearningStreakDays { get; init; }

    /// <summary>The longest run of consecutive active days on record.</summary>
    public int LongestStreakDays { get; init; }

    /// <summary>Distinct days with recorded activity inside the window.</summary>
    public int ActiveDaysInWindow { get; init; }

    public DateTime? LastActivityAt { get; init; }

    /// <summary>Lessons marked complete in the window.</summary>
    public int LessonsCompletedInWindow { get; init; }

    /// <summary>Total watch time recorded across every enrolled course, in minutes.</summary>
    public int TotalLearningMinutes { get; init; }
}

public sealed record StudentWorkloadDto
{
    public int ActiveGroups { get; init; }

    public int ActiveCourses { get; init; }

    public int CompletedCourses { get; init; }

    /// <summary>Published homework the student has not handed in and whose deadline has passed.</summary>
    public int OverdueHomework { get; init; }

    /// <summary>Published homework still open and not yet handed in.</summary>
    public int PendingHomework { get; init; }

    /// <summary>Published exams dated in the future.</summary>
    public int UpcomingExams { get; init; }
}

public sealed record StudentSubjectStandingDto
{
    public Guid SubjectId { get; init; }

    public string SubjectNameAr { get; init; } = string.Empty;

    public string SubjectNameEn { get; init; } = string.Empty;

    /// <summary>Marks this average is based on. A single mark is a weak signal; the UI can say so.</summary>
    public int MarkCount { get; init; }

    public decimal AveragePercentage { get; init; }

    public int PassedCount { get; init; }

    public decimal PassRate { get; init; }
}
