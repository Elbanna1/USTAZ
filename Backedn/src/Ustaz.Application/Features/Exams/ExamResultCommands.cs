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

public sealed record ExamResultInput
{
    public Guid StudentId { get; init; }

    public decimal Score { get; init; }

    public bool IsAbsent { get; init; }

    public string? TeacherComment { get; init; }
}

/// <summary>
/// Records or corrects exam results. One or many, same path: the bulk endpoint simply
/// supplies more entries.
/// </summary>
public sealed record RecordExamResultsCommand : IRequest<IReadOnlyList<ExamResultDto>>
{
    public Guid ExamId { get; init; }

    public IReadOnlyList<ExamResultInput> Results { get; init; } = [];
}

public sealed class RecordExamResultsCommandValidator : AbstractValidator<RecordExamResultsCommand>
{
    public RecordExamResultsCommandValidator()
    {
        RuleFor(command => command.ExamId).NotEmpty();

        RuleFor(command => command.Results)
            .NotEmpty().WithMessage("Provide at least one result.")
            .Must(results => results.Select(result => result.StudentId).Distinct().Count() == results.Count)
            .WithMessage("Each student may appear only once.");

        RuleForEach(command => command.Results).ChildRules(result =>
        {
            result.RuleFor(item => item.StudentId).NotEmpty();
            result.RuleFor(item => item.Score).GreaterThanOrEqualTo(0);
            result.RuleFor(item => item.TeacherComment).MaximumLength(DomainRules.ShortTextMaxLength);
        });
    }
}

public sealed class RecordExamResultsCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    IAuditLogger auditLogger) : IRequestHandler<RecordExamResultsCommand, IReadOnlyList<ExamResultDto>>
{
    public async Task<IReadOnlyList<ExamResultDto>> Handle(
        RecordExamResultsCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.GradesManage, cancellationToken: cancellationToken);

        await planLimits.EnsureFeatureAsync(access.TenantId, PlanFeatures.Exams, cancellationToken);

        var exam = await context.Exams
            .Include(entity => entity.Results)
            .FirstOrDefaultAsync(entity => entity.Id == command.ExamId, cancellationToken)
            ?? throw new NotFoundException("Exam", command.ExamId);

        tenantAuthorization.EnsureSameTenant(access, exam.TenantId);

        var utcNow = dateTimeProvider.UtcNow;
        var touched = new List<ExamResult>();

        foreach (var input in command.Results)
        {
            exam.EnsureScoreIsValid(input.Score);

            var result = exam.Results.FirstOrDefault(entity => entity.StudentId == input.StudentId);

            if (result is null)
            {
                result = new ExamResult
                {
                    Id = Guid.NewGuid(),
                    TenantId = exam.TenantId,
                    ExamId = exam.Id,
                    StudentId = input.StudentId,
                    CreatedAt = utcNow
                };

                exam.Results.Add(result);
            }
            else
            {
                // A corrected score re-opens the low-grade alert.
                result.ParentNotified = false;
            }

            result.IsAbsent = input.IsAbsent;
            result.TeacherComment = input.TeacherComment?.Trim();
            result.RecordedByUserId = access.UserId;
            result.RecordedAt = utcNow;
            result.ApplyScore(input.IsAbsent ? 0m : input.Score, exam.MaximumScore, exam.PassingScore, utcNow);

            touched.Add(result);
        }

        await context.SaveChangesAsync(cancellationToken);

        await NotifyLowGradesAsync(exam, touched, access.TenantId, cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.GradeRecorded,
            nameof(Exam),
            exam.Id,
            newValues: new { Results = command.Results.Count },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        var ids = touched.Select(result => result.Id).ToList();

        return await context.ExamResults
            .AsNoTracking()
            .Where(result => ids.Contains(result.Id))
            .Select(ExamProjections.ToResultDto)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Alerts guardians when a child scores below the workspace's configured threshold.
    /// The threshold comes from tenant settings, never from a hardcoded constant.
    /// </summary>
    private async Task NotifyLowGradesAsync(
        Exam exam,
        IReadOnlyCollection<ExamResult> results,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var settings = await context.TenantSettings
            .AsNoTracking()
            .Where(entry => entry.TenantId == tenantId)
            .Select(entry => new { entry.NotifyParentsOnLowGrade, entry.GradeNotificationThresholdPercentage })
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null || !settings.NotifyParentsOnLowGrade)
        {
            return;
        }

        var threshold = settings.GradeNotificationThresholdPercentage;

        var below = results
            .Where(result => !result.ParentNotified && result.Percentage < threshold)
            .ToList();

        if (below.Count == 0)
        {
            return;
        }

        var studentIds = below.Select(result => result.StudentId).Distinct().ToList();

        var students = await context.StudentProfiles
            .AsNoTracking()
            .Where(student => studentIds.Contains(student.Id))
            .Select(student => new { student.Id, student.User.DisplayName })
            .ToListAsync(cancellationToken);

        var guardians = await context.ParentStudentLinks
            .AsNoTracking()
            .Where(link => studentIds.Contains(link.StudentId) && link.IsConfirmed)
            .Select(link => new { link.StudentId, link.Parent.UserId })
            .ToListAsync(cancellationToken);

        var utcNow = dateTimeProvider.UtcNow;

        foreach (var result in below)
        {
            var student = students.FirstOrDefault(entry => entry.Id == result.StudentId);
            if (student is null)
            {
                continue;
            }

            foreach (var guardian in guardians.Where(entry => entry.StudentId == result.StudentId))
            {
                await notifications.SendAsync(
                    guardian.UserId,
                    new NotificationRequest
                    {
                        Event = NotificationEvents.ChildLowGrade,
                        TenantId = tenantId,
                        ReferenceId = result.Id,
                        DeduplicationKey = "low-grade:" + result.Id,
                        Values = new Dictionary<string, string>
                        {
                            ["student"] = student.DisplayName,
                            ["studentId"] = result.StudentId.ToString(),
                            ["exam"] = exam.Title,
                            ["score"] = result.Score.ToString("0.##"),
                            ["max"] = result.MaximumScore.ToString("0.##"),
                            ["percentage"] = result.Percentage.ToString("0.#")
                        }
                    },
                    cancellationToken);
            }

            result.ParentNotified = true;
            result.ParentNotifiedAt = utcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Corrects one student's result.</summary>
public sealed record UpdateExamResultCommand : IRequest<ExamResultDto>
{
    public Guid ExamId { get; init; }

    public Guid StudentId { get; init; }

    public decimal Score { get; init; }

    public bool IsAbsent { get; init; }

    public string? TeacherComment { get; init; }
}

public sealed class UpdateExamResultCommandHandler(IMediator mediator)
    : IRequestHandler<UpdateExamResultCommand, ExamResultDto>
{
    public async Task<ExamResultDto> Handle(UpdateExamResultCommand command, CancellationToken cancellationToken)
    {
        var results = await mediator.Send(
            new RecordExamResultsCommand
            {
                ExamId = command.ExamId,
                Results =
                [
                    new ExamResultInput
                    {
                        StudentId = command.StudentId,
                        Score = command.Score,
                        IsAbsent = command.IsAbsent,
                        TeacherComment = command.TeacherComment
                    }
                ]
            },
            cancellationToken);

        return results[0];
    }
}
