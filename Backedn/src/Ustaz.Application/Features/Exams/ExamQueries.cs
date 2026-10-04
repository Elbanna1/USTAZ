using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Features.Exams.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Exams;

internal static class ExamProjections
{
    public static Expression<Func<Exam, ExamDto>> ToDto =>
        exam => new ExamDto
        {
            Id = exam.Id,
            TenantId = exam.TenantId,
            TeacherId = exam.TeacherId,
            TeacherName = exam.Teacher.User.DisplayName,
            GroupId = exam.GroupId,
            GroupName = exam.Group != null ? exam.Group.Name : null,
            SubjectId = exam.SubjectId,
            SubjectNameAr = exam.Subject.NameAr,
            SubjectNameEn = exam.Subject.NameEn,
            GradeId = exam.GradeId,
            GradeName = exam.Grade != null ? exam.Grade.NameAr : null,
            Title = exam.Title,
            Description = exam.Description,
            Type = exam.Type,
            ExamDate = exam.ExamDate,
            DurationInMinutes = exam.DurationInMinutes,
            MaximumScore = exam.MaximumScore,
            PassingScore = exam.PassingScore,
            IsPublished = exam.IsPublished,
            ResultsPublished = exam.ResultsPublished,
            IsOnline = exam.IsOnline,
            MaxAttempts = exam.MaxAttempts,
            ShowResultsImmediately = exam.ShowResultsImmediately,
            QuestionsCount = exam.Questions.Count,
            CreatedAt = exam.CreatedAt,
            ResultsCount = exam.Results.Count,
            AverageScore = exam.Results.Count == 0 ? null : exam.Results.Average(result => result.Score),
            AveragePercentage = exam.Results.Count == 0 ? null : exam.Results.Average(result => result.Percentage),
            HighestScore = exam.Results.Count == 0 ? null : exam.Results.Max(result => result.Score),
            LowestScore = exam.Results.Count == 0 ? null : exam.Results.Min(result => result.Score),
            PassedCount = exam.Results.Count(result => result.IsPassed)
        };

    public static Expression<Func<ExamResult, ExamResultDto>> ToResultDto =>
        result => new ExamResultDto
        {
            Id = result.Id,
            ExamId = result.ExamId,
            ExamTitle = result.Exam.Title,
            ExamDate = result.Exam.ExamDate,
            StudentId = result.StudentId,
            StudentName = result.Student.User.DisplayName,
            Score = result.Score,
            MaximumScore = result.MaximumScore,
            Percentage = result.Percentage,
            IsPassed = result.IsPassed,
            IsAbsent = result.IsAbsent,
            TeacherComment = result.TeacherComment,
            SubjectNameAr = result.Exam.Subject.NameAr,
            GroupName = result.Exam.Group != null ? result.Exam.Group.Name : null,
            RecordedAt = result.RecordedAt
        };
}

public sealed record GetExamByIdQuery(Guid ExamId) : IRequest<ExamDto>;

public sealed class GetExamByIdQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetExamByIdQuery, ExamDto>
{
    public async Task<ExamDto> Handle(GetExamByIdQuery query, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.ExamsView, cancellationToken: cancellationToken);

        var exam = await context.Exams
            .AsNoTracking()
            .Where(entity => entity.Id == query.ExamId)
            .Select(ExamProjections.ToDto)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Exam", query.ExamId);

        tenantAuthorization.EnsureSameTenant(access, exam.TenantId);

        return exam;
    }
}

public sealed record GetExamsQuery : PagedRequest, IRequest<PaginatedList<ExamDto>>
{
    public Guid? GroupId { get; init; }

    public Guid? SubjectId { get; init; }

    public ExamType? Type { get; init; }

    public bool? IsPublished { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}

public sealed class GetExamsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetExamsQuery, PaginatedList<ExamDto>>
{
    public async Task<PaginatedList<ExamDto>> Handle(GetExamsQuery query, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.ExamsView, cancellationToken: cancellationToken);

        var search = query.Search?.Trim();

        return await context.Exams
            .AsNoTracking()
            .Where(exam => exam.TenantId == access.TenantId)
            .WhereIf(query.GroupId.HasValue, exam => exam.GroupId == query.GroupId!.Value)
            .WhereIf(query.SubjectId.HasValue, exam => exam.SubjectId == query.SubjectId!.Value)
            .WhereIf(query.Type.HasValue, exam => exam.Type == query.Type!.Value)
            .WhereIf(query.IsPublished.HasValue, exam => exam.IsPublished == query.IsPublished!.Value)
            .WhereIf(query.From.HasValue, exam => exam.ExamDate >= query.From!.Value)
            .WhereIf(query.To.HasValue, exam => exam.ExamDate <= query.To!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(search), exam => exam.Title.Contains(search!))
            .OrderByDescending(exam => exam.ExamDate)
            .Select(ExamProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

public sealed record GetExamResultsQuery(Guid ExamId) : IRequest<IReadOnlyList<ExamResultDto>>;

public sealed class GetExamResultsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetExamResultsQuery, IReadOnlyList<ExamResultDto>>
{
    public async Task<IReadOnlyList<ExamResultDto>> Handle(
        GetExamResultsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.GradesView, cancellationToken: cancellationToken);

        var tenantId = await context.Exams
            .AsNoTracking()
            .Where(exam => exam.Id == query.ExamId)
            .Select(exam => (Guid?)exam.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Exam", query.ExamId);

        tenantAuthorization.EnsureSameTenant(access, tenantId);

        return await context.ExamResults
            .AsNoTracking()
            .Where(result => result.ExamId == query.ExamId)
            .OrderByDescending(result => result.Percentage)
            .Select(ExamProjections.ToResultDto)
            .ToListAsync(cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Student and parent facing
// ---------------------------------------------------------------------------

/// <summary>Exams visible to the signed-in student, i.e. published ones for their groups.</summary>
public sealed record GetMyExamsQuery : PagedRequest, IRequest<PaginatedList<ExamDto>>
{
    public bool? Upcoming { get; init; }
}

public sealed class GetMyExamsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetMyExamsQuery, PaginatedList<ExamDto>>
{
    public Task<PaginatedList<ExamDto>> Handle(GetMyExamsQuery query, CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();
        var utcNow = dateTimeProvider.UtcNow;

        return context.Exams
            .AsNoTracking()
            .Where(exam => exam.IsPublished
                && exam.GroupId != null
                && context.GroupEnrollments.Any(enrollment =>
                    enrollment.GroupId == exam.GroupId
                    && enrollment.StudentId == studentId
                    && enrollment.Status != EnrollmentStatus.Cancelled))
            .WhereIf(query.Upcoming == true, exam => exam.ExamDate >= utcNow)
            .WhereIf(query.Upcoming == false, exam => exam.ExamDate < utcNow)
            .OrderBy(exam => exam.ExamDate)
            .Select(ExamProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>A student's own results. Only released results are visible.</summary>
public sealed record GetMyGradesQuery : IRequest<StudentGradesSummaryDto>;

public sealed class GetMyGradesQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetMyGradesQuery, StudentGradesSummaryDto>
{
    public Task<StudentGradesSummaryDto> Handle(GetMyGradesQuery query, CancellationToken cancellationToken) =>
        GradesReader.BuildAsync(context, currentUser.RequireStudentId(), onlyPublished: true, cancellationToken);
}

/// <summary>Grades for one of the calling parent's children.</summary>
public sealed record GetChildGradesQuery(Guid StudentId) : IRequest<StudentGradesSummaryDto>;

public sealed class GetChildGradesQueryHandler(
    IApplicationDbContext context,
    IParentAccessGuard parentAccess) : IRequestHandler<GetChildGradesQuery, StudentGradesSummaryDto>
{
    public async Task<StudentGradesSummaryDto> Handle(
        GetChildGradesQuery query,
        CancellationToken cancellationToken)
    {
        await parentAccess.EnsureLinkedAsync(query.StudentId, cancellationToken);
        return await GradesReader.BuildAsync(context, query.StudentId, onlyPublished: true, cancellationToken);
    }
}

/// <summary>Exams for one of the calling parent's children.</summary>
public sealed record GetChildExamsQuery : PagedRequest, IRequest<PaginatedList<ExamDto>>
{
    public Guid StudentId { get; init; }
}

public sealed class GetChildExamsQueryHandler(
    IApplicationDbContext context,
    IParentAccessGuard parentAccess) : IRequestHandler<GetChildExamsQuery, PaginatedList<ExamDto>>
{
    public async Task<PaginatedList<ExamDto>> Handle(GetChildExamsQuery query, CancellationToken cancellationToken)
    {
        await parentAccess.EnsureLinkedAsync(query.StudentId, cancellationToken);

        return await context.Exams
            .AsNoTracking()
            .Where(exam => exam.IsPublished
                && exam.GroupId != null
                && context.GroupEnrollments.Any(enrollment =>
                    enrollment.GroupId == exam.GroupId
                    && enrollment.StudentId == query.StudentId
                    && enrollment.Status != EnrollmentStatus.Cancelled))
            .OrderByDescending(exam => exam.ExamDate)
            .Select(ExamProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>A teacher's view of one student's grades.</summary>
public sealed record GetStudentGradesQuery(Guid StudentId) : IRequest<StudentGradesSummaryDto>;

public sealed class GetStudentGradesQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetStudentGradesQuery, StudentGradesSummaryDto>
{
    public async Task<StudentGradesSummaryDto> Handle(
        GetStudentGradesQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.GradesView, cancellationToken: cancellationToken);

        return await GradesReader.BuildAsync(
            context, query.StudentId, onlyPublished: false, cancellationToken, access.TenantId);
    }
}

internal static class GradesReader
{
    public static async Task<StudentGradesSummaryDto> BuildAsync(
        IApplicationDbContext context,
        Guid studentId,
        bool onlyPublished,
        CancellationToken cancellationToken,
        Guid? tenantId = null)
    {
        var results = await context.ExamResults
            .AsNoTracking()
            .Where(result => result.StudentId == studentId)
            .WhereIf(onlyPublished, result => result.Exam.ResultsPublished)
            .WhereIf(tenantId.HasValue, result => result.TenantId == tenantId!.Value)
            .OrderByDescending(result => result.Exam.ExamDate)
            .Select(ExamProjections.ToResultDto)
            .ToListAsync(cancellationToken);

        var bySubject = await context.ExamResults
            .AsNoTracking()
            .Where(result => result.StudentId == studentId)
            .WhereIf(onlyPublished, result => result.Exam.ResultsPublished)
            .WhereIf(tenantId.HasValue, result => result.TenantId == tenantId!.Value)
            .GroupBy(result => new
            {
                result.Exam.SubjectId,
                result.Exam.Subject.NameAr,
                result.Exam.Subject.NameEn
            })
            .Select(group => new SubjectAverageDto
            {
                SubjectId = group.Key.SubjectId,
                SubjectNameAr = group.Key.NameAr,
                SubjectNameEn = group.Key.NameEn,
                ExamsTaken = group.Count(),
                AveragePercentage = group.Average(result => result.Percentage)
            })
            .ToListAsync(cancellationToken);

        var studentName = await context.StudentProfiles
            .AsNoTracking()
            .Where(student => student.Id == studentId)
            .Select(student => student.User.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return new StudentGradesSummaryDto
        {
            StudentId = studentId,
            StudentName = studentName,
            ExamsTaken = results.Count,
            ExamsPassed = results.Count(result => result.IsPassed),
            AveragePercentage = results.Count == 0
                ? 0m
                : Math.Round(results.Average(result => result.Percentage), 2, MidpointRounding.AwayFromZero),
            HighestPercentage = results.Count == 0 ? 0m : results.Max(result => result.Percentage),
            LowestPercentage = results.Count == 0 ? 0m : results.Min(result => result.Percentage),
            Results = results,
            BySubject = [.. bySubject.Select(entry => entry with
            {
                AveragePercentage = Math.Round(entry.AveragePercentage, 2, MidpointRounding.AwayFromZero)
            })]
        };
    }
}
