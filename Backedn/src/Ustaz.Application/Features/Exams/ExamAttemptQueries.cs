using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Exams.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Exams;

internal static class ExamAttemptProjections
{
    /// <summary>
    /// Builds the attempt DTO. <paramref name="revealMarks"/> is the single switch that
    /// decides whether scores, correctness and the answer key are written into the payload;
    /// a teacher always sees them, a student only once the exam allows it.
    /// </summary>
    public static ExamAttemptDto ToDto(ExamAttempt attempt, Exam exam, string studentName, bool revealMarks) => new()
    {
        Id = attempt.Id,
        ExamId = attempt.ExamId,
        ExamTitle = exam.Title,
        StudentId = attempt.StudentId,
        StudentName = studentName,
        AttemptNumber = attempt.AttemptNumber,
        Status = attempt.Status,
        StartedAt = attempt.StartedAt,
        ExpiresAt = attempt.ExpiresAt,
        SubmittedAt = attempt.SubmittedAt,
        RequiresManualGrading = attempt.RequiresManualGrading,
        ScoresVisible = revealMarks,
        Score = revealMarks ? attempt.Score : 0m,
        MaximumScore = attempt.MaximumScore,
        Percentage = revealMarks ? attempt.Percentage : 0m,
        IsPassed = revealMarks && attempt.IsPassed,
        TeacherComment = revealMarks ? attempt.TeacherComment : null,
        GradedAt = attempt.GradedAt,
        Answers = attempt.Answers
            .OrderBy(answer => exam.Questions
                .Where(question => question.Id == answer.QuestionId)
                .Select(question => question.SortOrder)
                .FirstOrDefault())
            .Select(answer =>
            {
                var question = exam.Questions.FirstOrDefault(entity => entity.Id == answer.QuestionId);

                return new ExamAttemptAnswerDto
                {
                    Id = answer.Id,
                    QuestionId = answer.QuestionId,
                    QuestionText = question?.Text ?? string.Empty,
                    QuestionType = question?.Type ?? ExamQuestionType.ShortAnswer,
                    SelectedOptionId = answer.SelectedOptionId,
                    TextAnswer = answer.TextAnswer,
                    IsCorrect = revealMarks ? answer.IsCorrect : null,
                    AwardedPoints = revealMarks ? answer.AwardedPoints : 0m,
                    MaximumPoints = answer.MaximumPoints,
                    TeacherFeedback = revealMarks ? answer.TeacherFeedback : null,
                    CorrectAnswer = revealMarks ? ResolveCorrectAnswer(question) : null,
                    Explanation = revealMarks ? question?.Explanation : null
                };
            })
            .ToList()
    };

    /// <summary>The model answer in a form a client can display, whatever the question type.</summary>
    private static string? ResolveCorrectAnswer(ExamQuestion? question) => question switch
    {
        null => null,
        { Type: ExamQuestionType.MultipleChoice } => question.Options
            .Where(option => option.IsCorrect)
            .Select(option => option.Text)
            .FirstOrDefault(),
        { Type: ExamQuestionType.Essay } => null,
        _ => question.CorrectAnswer
    };
}

/// <summary>
/// One attempt. A student may read their own; a workspace member with the exams permission
/// may read any attempt inside their own workspace.
/// </summary>
public sealed record GetExamAttemptQuery(Guid AttemptId) : IRequest<ExamAttemptDto>;

public sealed class GetExamAttemptQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetExamAttemptQuery, ExamAttemptDto>
{
    public async Task<ExamAttemptDto> Handle(GetExamAttemptQuery query, CancellationToken cancellationToken)
    {
        var attempt = await context.ExamAttempts
            .AsNoTracking()
            .Include(entity => entity.Answers)
            .FirstOrDefaultAsync(entity => entity.Id == query.AttemptId, cancellationToken)
            ?? throw new NotFoundException("ExamAttempt", query.AttemptId);

        var exam = await context.Exams
            .AsNoTracking()
            .Include(entity => entity.Questions)
            .ThenInclude(question => question.Options)
            .FirstAsync(entity => entity.Id == attempt.ExamId, cancellationToken);

        var studentName = await context.StudentProfiles
            .AsNoTracking()
            .Where(student => student.Id == attempt.StudentId)
            .Select(student => student.User.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        // The owning student sees their own sitting, but only sees marks once released.
        if (currentUser.StudentId == attempt.StudentId)
        {
            return ExamAttemptProjections.ToDto(
                attempt,
                exam,
                studentName,
                revealMarks: exam.ScoresVisibleToStudent && attempt.Status != ExamAttemptStatus.InProgress);
        }

        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.ExamsView, cancellationToken: cancellationToken);

        tenantAuthorization.EnsureSameTenant(access, attempt.TenantId);

        return ExamAttemptProjections.ToDto(attempt, exam, studentName, revealMarks: true);
    }
}

/// <summary>Every attempt on one exam. Teacher side.</summary>
public sealed record GetExamAttemptsQuery : PagedRequest, IRequest<PaginatedList<ExamAttemptDto>>
{
    public Guid ExamId { get; init; }

    public ExamAttemptStatus? Status { get; init; }

    /// <summary>Narrows the list to attempts still waiting on a teacher.</summary>
    public bool? AwaitingGrading { get; init; }
}

public sealed class GetExamAttemptsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetExamAttemptsQuery, PaginatedList<ExamAttemptDto>>
{
    public async Task<PaginatedList<ExamAttemptDto>> Handle(
        GetExamAttemptsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.ExamsView, cancellationToken: cancellationToken);

        var exam = await context.Exams
            .AsNoTracking()
            .Include(entity => entity.Questions)
            .ThenInclude(question => question.Options)
            .FirstOrDefaultAsync(entity => entity.Id == query.ExamId, cancellationToken)
            ?? throw new NotFoundException("Exam", query.ExamId);

        tenantAuthorization.EnsureSameTenant(access, exam.TenantId);

        var page = await context.ExamAttempts
            .AsNoTracking()
            .Include(attempt => attempt.Answers)
            .Where(attempt => attempt.ExamId == query.ExamId)
            .WhereIf(query.Status.HasValue, attempt => attempt.Status == query.Status!.Value)
            .WhereIf(query.AwaitingGrading == true, attempt => attempt.RequiresManualGrading)
            .OrderByDescending(attempt => attempt.StartedAt)
            .ToPaginatedListAsync(query, cancellationToken);

        var studentIds = page.Items.Select(attempt => attempt.StudentId).Distinct().ToList();

        var names = await context.StudentProfiles
            .AsNoTracking()
            .Where(student => studentIds.Contains(student.Id))
            .Select(student => new { student.Id, student.User.DisplayName })
            .ToDictionaryAsync(entry => entry.Id, entry => entry.DisplayName, cancellationToken);

        return new PaginatedList<ExamAttemptDto>(
            page.Items
                .Select(attempt => ExamAttemptProjections.ToDto(
                    attempt,
                    exam,
                    names.GetValueOrDefault(attempt.StudentId, string.Empty),
                    revealMarks: true))
                .ToList(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }
}

/// <summary>The calling student's own attempts on one exam.</summary>
public sealed record GetMyExamAttemptsQuery(Guid ExamId) : IRequest<IReadOnlyList<ExamAttemptDto>>;

public sealed class GetMyExamAttemptsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetMyExamAttemptsQuery, IReadOnlyList<ExamAttemptDto>>
{
    public async Task<IReadOnlyList<ExamAttemptDto>> Handle(
        GetMyExamAttemptsQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        var exam = await context.Exams
            .AsNoTracking()
            .Include(entity => entity.Questions)
            .ThenInclude(question => question.Options)
            .FirstOrDefaultAsync(entity => entity.Id == query.ExamId, cancellationToken)
            ?? throw new NotFoundException("Exam", query.ExamId);

        var attempts = await context.ExamAttempts
            .AsNoTracking()
            .Include(attempt => attempt.Answers)
            .Where(attempt => attempt.ExamId == query.ExamId && attempt.StudentId == studentId)
            .OrderBy(attempt => attempt.AttemptNumber)
            .ToListAsync(cancellationToken);

        var studentName = await context.StudentProfiles
            .AsNoTracking()
            .Where(student => student.Id == studentId)
            .Select(student => student.User.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return attempts
            .Select(attempt => ExamAttemptProjections.ToDto(
                attempt,
                exam,
                studentName,
                revealMarks: exam.ScoresVisibleToStudent && attempt.Status != ExamAttemptStatus.InProgress))
            .ToList();
    }
}
