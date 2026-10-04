using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Exams.Dtos;

public sealed record ExamDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid TeacherId { get; init; }

    public string TeacherName { get; init; } = string.Empty;

    public Guid? GroupId { get; init; }

    public string? GroupName { get; init; }

    public Guid SubjectId { get; init; }

    public string SubjectNameAr { get; init; } = string.Empty;

    public string SubjectNameEn { get; init; } = string.Empty;

    public Guid? GradeId { get; init; }

    public string? GradeName { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public ExamType Type { get; init; }

    public string TypeName => Type.ToString();

    public DateTime ExamDate { get; init; }

    public int DurationInMinutes { get; init; }

    public decimal MaximumScore { get; init; }

    public decimal PassingScore { get; init; }

    public bool IsPublished { get; init; }

    public bool ResultsPublished { get; init; }

    /// <summary>True when students sit this exam in the app.</summary>
    public bool IsOnline { get; init; }

    public int MaxAttempts { get; init; }

    public bool ShowResultsImmediately { get; init; }

    /// <summary>How many questions the paper carries. Zero for an offline exam.</summary>
    public int QuestionsCount { get; init; }

    public int ResultsCount { get; init; }

    public decimal? AverageScore { get; init; }

    public decimal? AveragePercentage { get; init; }

    public decimal? HighestScore { get; init; }

    public decimal? LowestScore { get; init; }

    public int PassedCount { get; init; }

    public DateTime CreatedAt { get; init; }
}

public sealed record ExamResultDto
{
    public Guid Id { get; init; }

    public Guid ExamId { get; init; }

    public string ExamTitle { get; init; } = string.Empty;

    public DateTime ExamDate { get; init; }

    public Guid StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public decimal Score { get; init; }

    public decimal MaximumScore { get; init; }

    public decimal Percentage { get; init; }

    public bool IsPassed { get; init; }

    public bool IsAbsent { get; init; }

    public string? TeacherComment { get; init; }

    public string SubjectNameAr { get; init; } = string.Empty;

    public string? GroupName { get; init; }

    public DateTime RecordedAt { get; init; }
}

/// <summary>A student's overall academic picture inside one workspace.</summary>
public sealed record StudentGradesSummaryDto
{
    public Guid StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public int ExamsTaken { get; init; }

    public int ExamsPassed { get; init; }

    public decimal AveragePercentage { get; init; }

    public decimal HighestPercentage { get; init; }

    public decimal LowestPercentage { get; init; }

    public IReadOnlyList<ExamResultDto> Results { get; init; } = [];

    public IReadOnlyList<SubjectAverageDto> BySubject { get; init; } = [];
}

public sealed record SubjectAverageDto
{
    public Guid SubjectId { get; init; }

    public string SubjectNameAr { get; init; } = string.Empty;

    public string SubjectNameEn { get; init; } = string.Empty;

    public int ExamsTaken { get; init; }

    public decimal AveragePercentage { get; init; }
}
