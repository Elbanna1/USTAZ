using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Dashboard.Dtos;

/// <summary>Exam performance for a workspace, with subject and exam breakdowns.</summary>
public sealed record GradeAnalyticsDto
{
    /// <summary>Every recorded result, absentees included.</summary>
    public int TotalResults { get; init; }

    public int AbsentCount { get; init; }

    public int PassedCount { get; init; }

    public int FailedCount { get; init; }

    /// <summary>Share of students who sat the exam and passed. Absentees are excluded.</summary>
    public decimal PassRate { get; init; }

    public decimal AveragePercentage { get; init; }

    public decimal HighestPercentage { get; init; }

    public decimal LowestPercentage { get; init; }

    public IReadOnlyList<SubjectPerformanceDto> BySubject { get; init; } = [];

    public IReadOnlyList<ExamPerformanceDto> ByExam { get; init; } = [];
}

/// <summary>Exam performance aggregated for one subject.</summary>
public sealed record SubjectPerformanceDto
{
    public Guid SubjectId { get; init; }

    public string SubjectNameAr { get; init; } = string.Empty;

    public string SubjectNameEn { get; init; } = string.Empty;

    public int ResultsCount { get; init; }

    public decimal AveragePercentage { get; init; }

    public decimal HighestPercentage { get; init; }

    public decimal LowestPercentage { get; init; }

    public int PassedCount { get; init; }

    public decimal PassRate { get; init; }
}

/// <summary>Performance for a single exam.</summary>
public sealed record ExamPerformanceDto
{
    public Guid ExamId { get; init; }

    public string Title { get; init; } = string.Empty;

    public ExamType Type { get; init; }

    public DateTime ExamDate { get; init; }

    public int ResultsCount { get; init; }

    public decimal AveragePercentage { get; init; }

    public int PassedCount { get; init; }

    public decimal PassRate { get; init; }
}

/// <summary>Commercial and engagement performance for one course.</summary>
public sealed record CoursePerformanceDto
{
    public Guid CourseId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public bool IsPublished { get; init; }

    public decimal Price { get; init; }

    public int EnrollmentsCount { get; init; }

    public int CompletedCount { get; init; }

    public decimal TotalRevenue { get; init; }

    public int AverageProgressPercentage { get; init; }

    public decimal Rating { get; init; }

    public int ReviewCount { get; init; }

    public int LessonsCount { get; init; }
}
