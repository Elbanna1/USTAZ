using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Exams.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Exams;

/// <summary>Shared rules for the question bank of one online exam.</summary>
internal static class ExamQuestionHelper
{
    /// <summary>
    /// Loads an exam with its questions for editing. Editing the paper after students have
    /// started sitting it would silently rewrite their marks, so that is refused outright.
    /// </summary>
    public static async Task<Exam> LoadEditableExamAsync(
        IApplicationDbContext context,
        ITenantAuthorizationService tenantAuthorization,
        TenantAccess access,
        Guid examId,
        CancellationToken cancellationToken)
    {
        var exam = await context.Exams
            .Include(entity => entity.Questions)
            .ThenInclude(question => question.Options)
            .FirstOrDefaultAsync(entity => entity.Id == examId, cancellationToken)
            ?? throw new NotFoundException("Exam", examId);

        tenantAuthorization.EnsureSameTenant(access, exam.TenantId);

        if (!exam.IsOnline)
        {
            throw new BusinessRuleException(
                "EXAM_NOT_ONLINE",
                "Only an online exam has a question paper. Switch the exam to online first.");
        }

        var hasAttempts = await context.ExamAttempts
            .AnyAsync(attempt => attempt.ExamId == examId, cancellationToken);

        if (hasAttempts)
        {
            throw new ConflictException(
                "Students have already started this exam, so its questions can no longer be changed.");
        }

        return exam;
    }

    /// <summary>Keeps the exam's total in step with the points actually on the paper.</summary>
    public static void SyncMaximumScore(Exam exam, DateTime utcNow) =>
        exam.SyncMaximumScoreFromQuestions(exam.Questions.Sum(question => question.Points), utcNow);

    public static ExamQuestionDto ToDto(ExamQuestion question) => new()
    {
        Id = question.Id,
        ExamId = question.ExamId,
        Text = question.Text,
        Type = question.Type,
        Points = question.Points,
        SortOrder = question.SortOrder,
        CorrectAnswer = question.CorrectAnswer,
        Explanation = question.Explanation,
        Options = question.Options
            .OrderBy(option => option.SortOrder)
            .Select(option => new ExamQuestionOptionDto
            {
                Id = option.Id,
                Text = option.Text,
                IsCorrect = option.IsCorrect,
                SortOrder = option.SortOrder
            })
            .ToList()
    };
}

/// <summary>An option supplied by the teacher when writing a multiple choice question.</summary>
public sealed record ExamQuestionOptionInput
{
    public required string Text { get; init; }

    public bool IsCorrect { get; init; }

    public int SortOrder { get; init; }
}

// ---------------------------------------------------------------------------
// Create
// ---------------------------------------------------------------------------
public sealed record AddExamQuestionCommand : IRequest<ExamQuestionDto>
{
    public Guid ExamId { get; init; }

    public required string Text { get; init; }

    public ExamQuestionType Type { get; init; } = ExamQuestionType.MultipleChoice;

    public decimal Points { get; init; } = 1m;

    public int? SortOrder { get; init; }

    /// <summary>Model answer for a true/false or short answer question.</summary>
    public string? CorrectAnswer { get; init; }

    public string? Explanation { get; init; }

    public IReadOnlyList<ExamQuestionOptionInput> Options { get; init; } = [];
}

public sealed class AddExamQuestionCommandValidator : AbstractValidator<AddExamQuestionCommand>
{
    public AddExamQuestionCommandValidator()
    {
        RuleFor(command => command.ExamId).NotEmpty();
        RuleFor(command => command.Text).NotEmpty().MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Points).GreaterThan(0).LessThanOrEqualTo(1000);
        RuleFor(command => command.CorrectAnswer).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.Explanation).MaximumLength(DomainRules.ShortTextMaxLength);

        RuleFor(command => command.Options)
            .Must(options => options.Count <= 10)
            .WithMessage("A question can have at most ten options.");

        RuleForEach(command => command.Options).ChildRules(option =>
            option.RuleFor(item => item.Text)
                .NotEmpty()
                .MaximumLength(DomainRules.ShortTextMaxLength));
    }
}

public sealed class AddExamQuestionCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<AddExamQuestionCommand, ExamQuestionDto>
{
    public async Task<ExamQuestionDto> Handle(AddExamQuestionCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.ExamsManage, cancellationToken: cancellationToken);

        await planLimits.EnsureFeatureAsync(access.TenantId, PlanFeatures.Exams, cancellationToken);

        var exam = await ExamQuestionHelper.LoadEditableExamAsync(
            context, tenantAuthorization, access, command.ExamId, cancellationToken);

        var utcNow = dateTimeProvider.UtcNow;

        var question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            TenantId = exam.TenantId,
            ExamId = exam.Id,
            Text = command.Text.Trim(),
            Type = command.Type,
            Points = command.Points,
            SortOrder = command.SortOrder ?? exam.Questions.Count + 1,
            CorrectAnswer = command.CorrectAnswer?.Trim(),
            Explanation = command.Explanation?.Trim(),
            CreatedAt = utcNow
        };

        var sortOrder = 1;

        foreach (var option in command.Options)
        {
            question.Options.Add(new ExamQuestionOption
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                Text = option.Text.Trim(),
                IsCorrect = option.IsCorrect,
                SortOrder = option.SortOrder == 0 ? sortOrder : option.SortOrder,
                CreatedAt = utcNow
            });

            sortOrder++;
        }

        // The domain decides what a well-formed question of this type looks like.
        question.EnsureIsAnswerable();

        exam.Questions.Add(question);
        ExamQuestionHelper.SyncMaximumScore(exam, utcNow);

        await context.SaveChangesAsync(cancellationToken);

        return ExamQuestionHelper.ToDto(question);
    }
}

// ---------------------------------------------------------------------------
// Update
// ---------------------------------------------------------------------------
public sealed record UpdateExamQuestionCommand : IRequest<ExamQuestionDto>
{
    public Guid ExamId { get; init; }

    public Guid QuestionId { get; init; }

    public string? Text { get; init; }

    public ExamQuestionType? Type { get; init; }

    public decimal? Points { get; init; }

    public int? SortOrder { get; init; }

    public string? CorrectAnswer { get; init; }

    public string? Explanation { get; init; }

    /// <summary>When supplied, replaces the whole option set.</summary>
    public IReadOnlyList<ExamQuestionOptionInput>? Options { get; init; }
}

public sealed class UpdateExamQuestionCommandValidator : AbstractValidator<UpdateExamQuestionCommand>
{
    public UpdateExamQuestionCommandValidator()
    {
        RuleFor(command => command.ExamId).NotEmpty();
        RuleFor(command => command.QuestionId).NotEmpty();
        RuleFor(command => command.Text!).NotEmpty().MaximumLength(DomainRules.DescriptionMaxLength)
            .When(command => command.Text is not null);
        RuleFor(command => command.Type!.Value).IsInEnum().When(command => command.Type.HasValue);
        RuleFor(command => command.Points!.Value).GreaterThan(0).LessThanOrEqualTo(1000)
            .When(command => command.Points.HasValue);
        RuleFor(command => command.CorrectAnswer).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.Explanation).MaximumLength(DomainRules.ShortTextMaxLength);

        RuleForEach(command => command.Options!).ChildRules(option =>
            option.RuleFor(item => item.Text)
                .NotEmpty()
                .MaximumLength(DomainRules.ShortTextMaxLength))
            .When(command => command.Options is not null);
    }
}

public sealed class UpdateExamQuestionCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<UpdateExamQuestionCommand, ExamQuestionDto>
{
    public async Task<ExamQuestionDto> Handle(UpdateExamQuestionCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.ExamsManage, cancellationToken: cancellationToken);

        var exam = await ExamQuestionHelper.LoadEditableExamAsync(
            context, tenantAuthorization, access, command.ExamId, cancellationToken);

        var question = exam.Questions.FirstOrDefault(entity => entity.Id == command.QuestionId)
            ?? throw new NotFoundException("ExamQuestion", command.QuestionId);

        var utcNow = dateTimeProvider.UtcNow;

        if (command.Text is not null)
        {
            question.Text = command.Text.Trim();
        }

        if (command.Type.HasValue)
        {
            question.Type = command.Type.Value;
        }

        if (command.Points.HasValue)
        {
            question.Points = command.Points.Value;
        }

        if (command.SortOrder.HasValue)
        {
            question.SortOrder = command.SortOrder.Value;
        }

        if (command.CorrectAnswer is not null)
        {
            question.CorrectAnswer = command.CorrectAnswer.Trim();
        }

        if (command.Explanation is not null)
        {
            question.Explanation = command.Explanation.Trim();
        }

        if (command.Options is not null)
        {
            context.ExamQuestionOptions.RemoveRange(question.Options);
            question.Options.Clear();

            var sortOrder = 1;

            foreach (var option in command.Options)
            {
                question.Options.Add(new ExamQuestionOption
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question.Id,
                    Text = option.Text.Trim(),
                    IsCorrect = option.IsCorrect,
                    SortOrder = option.SortOrder == 0 ? sortOrder : option.SortOrder,
                    CreatedAt = utcNow
                });

                sortOrder++;
            }
        }

        question.EnsureIsAnswerable();
        question.UpdatedAt = utcNow;

        ExamQuestionHelper.SyncMaximumScore(exam, utcNow);

        await context.SaveChangesAsync(cancellationToken);

        return ExamQuestionHelper.ToDto(question);
    }
}

// ---------------------------------------------------------------------------
// Delete
// ---------------------------------------------------------------------------
public sealed record DeleteExamQuestionCommand(Guid ExamId, Guid QuestionId) : IRequest<Unit>;

public sealed class DeleteExamQuestionCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<DeleteExamQuestionCommand, Unit>
{
    public async Task<Unit> Handle(DeleteExamQuestionCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.ExamsManage, cancellationToken: cancellationToken);

        var exam = await ExamQuestionHelper.LoadEditableExamAsync(
            context, tenantAuthorization, access, command.ExamId, cancellationToken);

        var question = exam.Questions.FirstOrDefault(entity => entity.Id == command.QuestionId)
            ?? throw new NotFoundException("ExamQuestion", command.QuestionId);

        context.ExamQuestionOptions.RemoveRange(question.Options);
        context.ExamQuestions.Remove(question);
        exam.Questions.Remove(question);

        ExamQuestionHelper.SyncMaximumScore(exam, dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

// ---------------------------------------------------------------------------
// Read
// ---------------------------------------------------------------------------

/// <summary>The full question paper with its answer key. Teacher side only.</summary>
public sealed record GetExamQuestionsQuery(Guid ExamId) : IRequest<IReadOnlyList<ExamQuestionDto>>;

public sealed class GetExamQuestionsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetExamQuestionsQuery, IReadOnlyList<ExamQuestionDto>>
{
    public async Task<IReadOnlyList<ExamQuestionDto>> Handle(
        GetExamQuestionsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.ExamsView, cancellationToken: cancellationToken);

        var tenantId = await context.Exams
            .AsNoTracking()
            .Where(exam => exam.Id == query.ExamId)
            .Select(exam => (Guid?)exam.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Exam", query.ExamId);

        tenantAuthorization.EnsureSameTenant(access, tenantId);

        return await context.ExamQuestions
            .AsNoTracking()
            .Where(question => question.ExamId == query.ExamId)
            .OrderBy(question => question.SortOrder)
            .Select(question => new ExamQuestionDto
            {
                Id = question.Id,
                ExamId = question.ExamId,
                Text = question.Text,
                Type = question.Type,
                Points = question.Points,
                SortOrder = question.SortOrder,
                CorrectAnswer = question.CorrectAnswer,
                Explanation = question.Explanation,
                Options = question.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new ExamQuestionOptionDto
                    {
                        Id = option.Id,
                        Text = option.Text,
                        IsCorrect = option.IsCorrect,
                        SortOrder = option.SortOrder
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }
}
