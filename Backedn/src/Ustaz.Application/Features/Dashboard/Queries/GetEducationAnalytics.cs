using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Features.Dashboard.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Dashboard.Queries;

/// <summary>
/// Exam performance across the workspace. Complements <c>/teacher/analytics</c>, which reports
/// commercial performance, and <c>/teacher/attendance/statistics</c>, which reports presence.
/// </summary>
public sealed record GetGradeAnalyticsQuery : IRequest<GradeAnalyticsDto>
{
    public Guid? GroupId { get; init; }

    public Guid? SubjectId { get; init; }

    public ExamType? Type { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}

public sealed class GetGradeAnalyticsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetGradeAnalyticsQuery, GradeAnalyticsDto>
{
    public async Task<GradeAnalyticsDto> Handle(
        GetGradeAnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.GradesView, cancellationToken: cancellationToken);

        var results = context.ExamResults
            .AsNoTracking()
            .Where(result => result.TenantId == access.TenantId)
            .WhereIf(query.GroupId.HasValue, result => result.Exam.GroupId == query.GroupId!.Value)
            .WhereIf(query.SubjectId.HasValue, result => result.Exam.SubjectId == query.SubjectId!.Value)
            .WhereIf(query.Type.HasValue, result => result.Exam.Type == query.Type!.Value)
            .WhereIf(query.From.HasValue, result => result.Exam.ExamDate >= query.From!.Value)
            .WhereIf(query.To.HasValue, result => result.Exam.ExamDate <= query.To!.Value);

        // Absentees are counted separately: folding their zero in would drag every average down.
        var attempted = results.Where(result => !result.IsAbsent);

        var totals = await results
            .GroupBy(result => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Absent = group.Count(result => result.IsAbsent),
                Passed = group.Count(result => result.IsPassed)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var scores = await attempted
            .GroupBy(result => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Average = group.Average(result => (decimal?)result.Percentage),
                Highest = group.Max(result => (decimal?)result.Percentage),
                Lowest = group.Min(result => (decimal?)result.Percentage)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var bySubject = await attempted
            .GroupBy(result => new
            {
                result.Exam.SubjectId,
                result.Exam.Subject.NameAr,
                result.Exam.Subject.NameEn
            })
            .Select(group => new SubjectPerformanceDto
            {
                SubjectId = group.Key.SubjectId,
                SubjectNameAr = group.Key.NameAr,
                SubjectNameEn = group.Key.NameEn,
                ResultsCount = group.Count(),
                AveragePercentage = group.Average(result => result.Percentage),
                HighestPercentage = group.Max(result => result.Percentage),
                LowestPercentage = group.Min(result => result.Percentage),
                PassedCount = group.Count(result => result.IsPassed)
            })
            .ToListAsync(cancellationToken);

        var byExam = await attempted
            .GroupBy(result => new
            {
                result.ExamId,
                result.Exam.Title,
                result.Exam.Type,
                result.Exam.ExamDate
            })
            .OrderByDescending(group => group.Key.ExamDate)
            .Take(20)
            .Select(group => new ExamPerformanceDto
            {
                ExamId = group.Key.ExamId,
                Title = group.Key.Title,
                Type = group.Key.Type,
                ExamDate = group.Key.ExamDate,
                ResultsCount = group.Count(),
                AveragePercentage = group.Average(result => result.Percentage),
                PassedCount = group.Count(result => result.IsPassed)
            })
            .ToListAsync(cancellationToken);

        var attemptedCount = scores?.Count ?? 0;
        var passed = totals?.Passed ?? 0;

        return new GradeAnalyticsDto
        {
            TotalResults = totals?.Total ?? 0,
            AbsentCount = totals?.Absent ?? 0,
            PassedCount = passed,
            FailedCount = attemptedCount - passed,
            PassRate = Percentage(passed, attemptedCount),
            AveragePercentage = Round(scores?.Average),
            HighestPercentage = Round(scores?.Highest),
            LowestPercentage = Round(scores?.Lowest),
            BySubject =
            [
                .. bySubject.Select(entry => entry with
                {
                    AveragePercentage = Round(entry.AveragePercentage),
                    PassRate = Percentage(entry.PassedCount, entry.ResultsCount)
                })
            ],
            ByExam =
            [
                .. byExam.Select(entry => entry with
                {
                    AveragePercentage = Round(entry.AveragePercentage),
                    PassRate = Percentage(entry.PassedCount, entry.ResultsCount)
                })
            ]
        };
    }

    private static decimal Round(decimal? value) =>
        value is null ? 0m : Math.Round(value.Value, 2, MidpointRounding.AwayFromZero);

    private static decimal Percentage(int part, int total) =>
        total == 0 ? 0m : Math.Round((decimal)part / total * 100m, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Per-course commercial and engagement performance: the course-side counterpart of the group
/// breakdown already returned by <c>/teacher/analytics</c>.
/// </summary>
public sealed record GetCourseAnalyticsQuery : IRequest<IReadOnlyList<CoursePerformanceDto>>;

public sealed class GetCourseAnalyticsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetCourseAnalyticsQuery, IReadOnlyList<CoursePerformanceDto>>
{
    public async Task<IReadOnlyList<CoursePerformanceDto>> Handle(
        GetCourseAnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AnalyticsView, cancellationToken: cancellationToken);

        return await context.Courses
            .AsNoTracking()
            .Where(course => course.TenantId == access.TenantId)
            .OrderByDescending(course => course.StudentCount)
            .Select(course => new CoursePerformanceDto
            {
                CourseId = course.Id,
                Title = course.Title,
                Slug = course.Slug,
                IsPublished = course.IsPublished,
                Price = course.Price,
                EnrollmentsCount = course.Enrollments.Count(
                    enrollment => enrollment.Status != EnrollmentStatus.Cancelled),
                CompletedCount = course.Enrollments.Count(
                    enrollment => enrollment.Status == EnrollmentStatus.Completed),
                TotalRevenue = course.Enrollments
                    .Where(enrollment => enrollment.Status != EnrollmentStatus.Cancelled)
                    .Sum(enrollment => (decimal?)enrollment.FinalPrice) ?? 0m,
                AverageProgressPercentage = (int)(course.Enrollments
                    .Where(enrollment => enrollment.Status != EnrollmentStatus.Cancelled)
                    .Average(enrollment => (double?)enrollment.ProgressPercentage) ?? 0d),
                Rating = course.Rating,
                ReviewCount = course.ReviewCount,
                LessonsCount = course.LessonsCount
            })
            .ToListAsync(cancellationToken);
    }
}
