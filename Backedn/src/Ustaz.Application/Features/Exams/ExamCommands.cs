using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Features.Exams.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Exams;

public sealed record CreateExamCommand : IRequest<ExamDto>
{
    public Guid? GroupId { get; init; }

    public Guid SubjectId { get; init; }

    public Guid? GradeId { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public ExamType Type { get; init; } = ExamType.Quiz;

    public DateTime ExamDate { get; init; }

    public int DurationInMinutes { get; init; } = 60;

    public decimal MaximumScore { get; init; } = 100m;

    public decimal PassingScore { get; init; } = 50m;

    /// <summary>True to have students sit the exam in the app rather than on paper.</summary>
    public bool IsOnline { get; init; }

    public int MaxAttempts { get; init; } = 1;

    public bool ShowResultsImmediately { get; init; }
}

public sealed class CreateExamCommandValidator : AbstractValidator<CreateExamCommand>
{
    public CreateExamCommandValidator()
    {
        RuleFor(command => command.SubjectId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.ExamDate).NotEmpty();
        RuleFor(command => command.DurationInMinutes).InclusiveBetween(0, 1440);
        RuleFor(command => command.MaximumScore).GreaterThan(0).LessThanOrEqualTo(10000);

        RuleFor(command => command.PassingScore)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(command => command.MaximumScore)
            .WithMessage("The passing score must be between 0 and the maximum score.");

        RuleFor(command => command.MaxAttempts).InclusiveBetween(1, 10);
    }
}

public sealed class CreateExamCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    IMediator mediator) : IRequestHandler<CreateExamCommand, ExamDto>
{
    public async Task<ExamDto> Handle(CreateExamCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.ExamsManage, cancellationToken: cancellationToken);

        await planLimits.EnsureFeatureAsync(access.TenantId, PlanFeatures.Exams, cancellationToken);

        var teacherId = await ExamHelper.ResolveTeacherIdAsync(context, access, cancellationToken);

        if (command.GroupId.HasValue)
        {
            var group = await context.Groups
                .AsNoTracking()
                .Where(entity => entity.Id == command.GroupId.Value)
                .Select(entity => new { entity.TenantId })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Group", command.GroupId.Value);

            tenantAuthorization.EnsureSameTenant(access, group.TenantId);
        }

        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            TenantId = access.TenantId,
            TeacherId = teacherId,
            GroupId = command.GroupId,
            SubjectId = command.SubjectId,
            GradeId = command.GradeId,
            Title = command.Title.Trim(),
            Description = command.Description?.Trim(),
            Type = command.Type,
            ExamDate = DateTime.SpecifyKind(command.ExamDate, DateTimeKind.Utc),
            DurationInMinutes = command.DurationInMinutes,
            MaximumScore = command.MaximumScore,
            PassingScore = command.PassingScore,
            IsOnline = command.IsOnline,
            MaxAttempts = command.MaxAttempts,
            ShowResultsImmediately = command.ShowResultsImmediately,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await context.Exams.AddAsync(exam, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return await mediator.Send(new GetExamByIdQuery(exam.Id), cancellationToken);
    }
}

internal static class ExamHelper
{
    /// <summary>
    /// The teacher an exam is filed under. For an assistant acting on the owner's behalf this
    /// resolves to the workspace owner's teacher profile.
    /// </summary>
    public static async Task<Guid> ResolveTeacherIdAsync(
        IApplicationDbContext context,
        TenantAccess access,
        CancellationToken cancellationToken)
    {
        var teacherId = await context.TeacherProfiles
            .AsNoTracking()
            .Where(profile => profile.TenantId == access.TenantId)
            .Select(profile => profile.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (teacherId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "TENANT_HAS_NO_TEACHER",
                "This workspace has no teacher profile to file the record against.");
        }

        return teacherId;
    }

    public static async Task<Exam> LoadOwnedExamAsync(
        IApplicationDbContext context,
        ITenantAuthorizationService tenantAuthorization,
        TenantAccess access,
        Guid examId,
        CancellationToken cancellationToken)
    {
        var exam = await context.Exams
            .FirstOrDefaultAsync(entity => entity.Id == examId, cancellationToken)
            ?? throw new NotFoundException("Exam", examId);

        tenantAuthorization.EnsureSameTenant(access, exam.TenantId);

        return exam;
    }
}

public sealed record UpdateExamCommand : IRequest<ExamDto>
{
    public Guid ExamId { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public ExamType? Type { get; init; }

    public DateTime? ExamDate { get; init; }

    public int? DurationInMinutes { get; init; }

    public decimal? MaximumScore { get; init; }

    public decimal? PassingScore { get; init; }

    public Guid? GroupId { get; init; }

    public bool? IsOnline { get; init; }

    public int? MaxAttempts { get; init; }

    public bool? ShowResultsImmediately { get; init; }
}

public sealed class UpdateExamCommandValidator : AbstractValidator<UpdateExamCommand>
{
    public UpdateExamCommandValidator()
    {
        RuleFor(command => command.ExamId).NotEmpty();
        RuleFor(command => command.Title).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.MaximumScore!.Value).GreaterThan(0).When(command => command.MaximumScore.HasValue);
        RuleFor(command => command.DurationInMinutes!.Value).InclusiveBetween(0, 1440).When(command => command.DurationInMinutes.HasValue);
        RuleFor(command => command.MaxAttempts!.Value).InclusiveBetween(1, 10).When(command => command.MaxAttempts.HasValue);
    }
}

public sealed class UpdateExamCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IMediator mediator) : IRequestHandler<UpdateExamCommand, ExamDto>
{
    public async Task<ExamDto> Handle(UpdateExamCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.ExamsManage, cancellationToken: cancellationToken);

        var exam = await ExamHelper.LoadOwnedExamAsync(
            context, tenantAuthorization, access, command.ExamId, cancellationToken);

        if (command.Title is not null)
        {
            exam.Title = command.Title.Trim();
        }

        if (command.Description is not null)
        {
            exam.Description = command.Description.Trim();
        }

        if (command.Type.HasValue)
        {
            exam.Type = command.Type.Value;
        }

        if (command.ExamDate.HasValue)
        {
            exam.ExamDate = DateTime.SpecifyKind(command.ExamDate.Value, DateTimeKind.Utc);
        }

        if (command.DurationInMinutes.HasValue)
        {
            exam.DurationInMinutes = command.DurationInMinutes.Value;
        }

        if (command.GroupId.HasValue)
        {
            var group = await context.Groups
                .AsNoTracking()
                .Where(entity => entity.Id == command.GroupId.Value)
                .Select(entity => new { entity.TenantId })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Group", command.GroupId.Value);

            tenantAuthorization.EnsureSameTenant(access, group.TenantId);
            exam.GroupId = command.GroupId.Value;
        }

        if (command.IsOnline.HasValue)
        {
            exam.IsOnline = command.IsOnline.Value;
        }

        if (command.MaxAttempts.HasValue)
        {
            exam.MaxAttempts = command.MaxAttempts.Value;
        }

        if (command.ShowResultsImmediately.HasValue)
        {
            exam.ShowResultsImmediately = command.ShowResultsImmediately.Value;
        }

        // Changing the scale invalidates recorded percentages, so they are recomputed below.
        var scaleChanged = false;

        if (command.MaximumScore.HasValue && command.MaximumScore.Value != exam.MaximumScore)
        {
            exam.MaximumScore = command.MaximumScore.Value;
            scaleChanged = true;
        }

        if (command.PassingScore.HasValue)
        {
            exam.PassingScore = command.PassingScore.Value;
            scaleChanged = true;
        }

        if (exam.PassingScore > exam.MaximumScore)
        {
            throw new BusinessRuleException(
                "EXAM_INVALID_PASSING_SCORE",
                "The passing score cannot be higher than the maximum score.");
        }

        var utcNow = dateTimeProvider.UtcNow;

        if (scaleChanged)
        {
            var results = await context.ExamResults
                .Where(result => result.ExamId == exam.Id)
                .ToListAsync(cancellationToken);

            foreach (var result in results)
            {
                var clamped = Math.Min(result.Score, exam.MaximumScore);
                result.ApplyScore(clamped, exam.MaximumScore, exam.PassingScore, utcNow);
            }
        }

        exam.UpdatedAt = utcNow;
        await context.SaveChangesAsync(cancellationToken);

        return await mediator.Send(new GetExamByIdQuery(exam.Id), cancellationToken);
    }
}

public sealed record PublishExamCommand(Guid ExamId, bool PublishResults = false) : IRequest<Unit>;

public sealed class PublishExamCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications) : IRequestHandler<PublishExamCommand, Unit>
{
    public async Task<Unit> Handle(PublishExamCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.ExamsManage, cancellationToken: cancellationToken);

        var exam = await context.Exams
            .Include(entity => entity.Questions)
            .ThenInclude(question => question.Options)
            .FirstOrDefaultAsync(entity => entity.Id == command.ExamId, cancellationToken)
            ?? throw new NotFoundException("Exam", command.ExamId);

        tenantAuthorization.EnsureSameTenant(access, exam.TenantId);

        var utcNow = dateTimeProvider.UtcNow;
        exam.Publish(utcNow);

        if (command.PublishResults)
        {
            exam.PublishResults(utcNow);
        }

        await context.SaveChangesAsync(cancellationToken);

        if (exam.GroupId is not null)
        {
            var students = await NotificationAudience.GroupStudentUserIdsAsync(
                context, exam.GroupId.Value, cancellationToken);

            // Publishing the exam and releasing its results are two different moments in a
            // student's day, and each is announced exactly once however often the teacher
            // saves the exam.
            await notifications.SendManyAsync(
                students,
                new NotificationRequest
                {
                    Event = NotificationEvents.ExamPublished,
                    TenantId = exam.TenantId,
                    ReferenceId = exam.Id,
                    DeduplicationKey = "exam-published:" + exam.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["exam"] = exam.Title,
                        ["examId"] = exam.Id.ToString(),
                        ["date"] = exam.ExamDate.ToString("yyyy-MM-dd HH:mm")
                    }
                },
                cancellationToken);

            if (exam.ResultsPublished)
            {
                await notifications.SendManyAsync(
                    students,
                    new NotificationRequest
                    {
                        Event = NotificationEvents.ExamResultsPublished,
                        TenantId = exam.TenantId,
                        ReferenceId = exam.Id,
                        DeduplicationKey = "exam-results:" + exam.Id,
                        Values = new Dictionary<string, string> { ["exam"] = exam.Title }
                    },
                    cancellationToken);

                await NotifyGuardiansOfResultsAsync(exam, cancellationToken);
            }
        }

        return Unit.Value;
    }

    /// <summary>
    /// Sends each guardian their own child's mark when results are released. One message per
    /// result, so a family with two children in the class hears about both, once each.
    /// </summary>
    private async Task NotifyGuardiansOfResultsAsync(Exam exam, CancellationToken cancellationToken)
    {
        var results = await context.ExamResults
            .AsNoTracking()
            .Where(result => result.ExamId == exam.Id && !result.IsAbsent)
            .Select(result => new
            {
                result.Id,
                result.StudentId,
                StudentName = result.Student.User.DisplayName,
                result.Score,
                result.MaximumScore
            })
            .ToListAsync(cancellationToken);

        foreach (var result in results)
        {
            var guardians = await NotificationAudience.GuardianUserIdsAsync(
                context, result.StudentId, cancellationToken);

            if (guardians.Count == 0)
            {
                continue;
            }

            await notifications.SendManyAsync(
                guardians,
                new NotificationRequest
                {
                    Event = NotificationEvents.ChildExamResult,
                    TenantId = exam.TenantId,
                    ReferenceId = result.Id,
                    DeduplicationKey = "child-exam-result:" + result.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["student"] = result.StudentName,
                        ["studentId"] = result.StudentId.ToString(),
                        ["exam"] = exam.Title,
                        ["score"] = result.Score.ToString("0.##"),
                        ["max"] = result.MaximumScore.ToString("0.##")
                    }
                },
                cancellationToken);
        }
    }
}

public sealed record DeleteExamCommand(Guid ExamId) : IRequest<Unit>;

public sealed class DeleteExamCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<DeleteExamCommand, Unit>
{
    public async Task<Unit> Handle(DeleteExamCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.ExamsManage, cancellationToken: cancellationToken);

        var exam = await ExamHelper.LoadOwnedExamAsync(
            context, tenantAuthorization, access, command.ExamId, cancellationToken);

        var hasResults = await context.ExamResults.AnyAsync(result => result.ExamId == exam.Id, cancellationToken);

        if (hasResults)
        {
            throw new ConflictException(
                "This exam already has recorded results. Unpublish it instead of deleting it.");
        }

        // An online exam that has been sat carries attempts and answers. Those answers point
        // at both the attempt and the question, so deleting the exam underneath them is a
        // referential minefield as well as a loss of student work.
        var hasAttempts = await context.ExamAttempts.AnyAsync(
            attempt => attempt.ExamId == exam.Id, cancellationToken);

        if (hasAttempts)
        {
            throw new ConflictException(
                "Students have already started this exam. Unpublish it instead of deleting it.");
        }

        context.Exams.Remove(exam);
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
