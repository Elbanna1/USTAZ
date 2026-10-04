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

/// <summary>Shared rules for sitting and marking an online exam.</summary>
internal static class ExamAttemptHelper
{
    /// <summary>
    /// Loads an online exam a student is entitled to sit. Access follows the same rule as
    /// the rest of the student-facing exam surface: the exam must be published and the
    /// student must hold a live enrollment in its group.
    /// </summary>
    public static async Task<Exam> LoadSittableExamAsync(
        IApplicationDbContext context,
        Guid examId,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var exam = await context.Exams
            .Include(entity => entity.Questions)
            .ThenInclude(question => question.Options)
            .FirstOrDefaultAsync(entity => entity.Id == examId, cancellationToken)
            ?? throw new NotFoundException("Exam", examId);

        if (!exam.IsPublished)
        {
            // An unpublished exam must be indistinguishable from one that does not exist.
            throw new NotFoundException("Exam", examId);
        }

        if (!exam.IsOnline)
        {
            throw new BusinessRuleException(
                "EXAM_NOT_ONLINE",
                "This exam is not sat online.");
        }

        var isEnrolled = exam.GroupId.HasValue
            && await context.GroupEnrollments.AnyAsync(
                enrollment => enrollment.GroupId == exam.GroupId.Value
                    && enrollment.StudentId == studentId
                    && enrollment.Status != EnrollmentStatus.Cancelled,
                cancellationToken);

        if (!isEnrolled)
        {
            throw new ForbiddenAccessException("You are not enrolled in the group this exam belongs to.");
        }

        return exam;
    }

    /// <summary>
    /// Marks every answer that can be marked without a teacher and writes the totals onto
    /// the attempt. Returns true when an essay still needs a human.
    /// </summary>
    public static bool AutoGrade(Exam exam, ExamAttempt attempt, DateTime utcNow)
    {
        var requiresManualGrading = false;
        var awarded = 0m;

        foreach (var answer in attempt.Answers)
        {
            var question = exam.Questions.First(entity => entity.Id == answer.QuestionId);

            answer.MaximumPoints = question.Points;

            if (question.IsAutoGradable)
            {
                var points = question.Grade(answer.SelectedOptionId, answer.TextAnswer);
                answer.AwardedPoints = points;
                answer.IsCorrect = points > 0m;
            }
            else
            {
                // Essays start unmarked; the teacher supplies the points later.
                answer.AwardedPoints = 0m;
                answer.IsCorrect = null;
                requiresManualGrading = true;
            }

            awarded += answer.AwardedPoints;
        }

        // Unanswered essays still hold the attempt open for grading.
        if (exam.Questions.Any(question =>
                !question.IsAutoGradable
                && attempt.Answers.All(answer => answer.QuestionId != question.Id)))
        {
            requiresManualGrading = true;
        }

        var maximum = exam.Questions.Sum(question => question.Points);
        attempt.ApplyScore(awarded, maximum, exam.PassingScore, utcNow);

        return requiresManualGrading;
    }

    /// <summary>
    /// Mirrors a finished attempt into <see cref="ExamResult"/>, which is what the grade
    /// books, dashboards and parent views already read. Keeping one result row per student
    /// per exam means an online sitting shows up everywhere an offline mark would.
    /// </summary>
    public static async Task UpsertResultAsync(
        IApplicationDbContext context,
        Exam exam,
        ExamAttempt attempt,
        Guid recordedByUserId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var result = await context.ExamResults
            .FirstOrDefaultAsync(
                entity => entity.ExamId == exam.Id && entity.StudentId == attempt.StudentId,
                cancellationToken);

        if (result is null)
        {
            result = new ExamResult
            {
                Id = Guid.NewGuid(),
                TenantId = exam.TenantId,
                ExamId = exam.Id,
                StudentId = attempt.StudentId,
                CreatedAt = utcNow
            };

            await context.ExamResults.AddAsync(result, cancellationToken);
        }

        // Retakes replace the previous mark, so the grade book always shows the latest sitting.
        result.IsAbsent = false;
        result.RecordedByUserId = recordedByUserId;
        result.RecordedAt = utcNow;
        result.ParentNotified = false;

        // The attempt is marked out of the paper's own total; the grade book is kept on the
        // exam's scale so an edited maximum never produces an out-of-range result row.
        var scaled = attempt.MaximumScore <= 0m
            ? 0m
            : Math.Round(attempt.Score / attempt.MaximumScore * exam.MaximumScore, 2, MidpointRounding.AwayFromZero);

        result.ApplyScore(Math.Min(scaled, exam.MaximumScore), exam.MaximumScore, exam.PassingScore, utcNow);
    }
}

// ---------------------------------------------------------------------------
// Start / resume
// ---------------------------------------------------------------------------

/// <summary>
/// Opens a sitting for the calling student, or hands back the one already in progress so a
/// refresh never burns an attempt.
/// </summary>
public sealed record StartExamAttemptCommand(Guid ExamId) : IRequest<ExamAttemptSessionDto>;

public sealed class StartExamAttemptCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<StartExamAttemptCommand, ExamAttemptSessionDto>
{
    public async Task<ExamAttemptSessionDto> Handle(
        StartExamAttemptCommand command,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();
        var utcNow = dateTimeProvider.UtcNow;

        var exam = await ExamAttemptHelper.LoadSittableExamAsync(
            context, command.ExamId, studentId, cancellationToken);

        if (exam.Questions.Count == 0)
        {
            throw new BusinessRuleException(
                "EXAM_HAS_NO_QUESTIONS",
                "This exam has no questions yet.");
        }

        if (utcNow < exam.ExamDate)
        {
            throw new BusinessRuleException(
                "EXAM_NOT_OPEN",
                "This exam has not opened yet.");
        }

        var attempts = await context.ExamAttempts
            .Include(attempt => attempt.Answers)
            .Where(attempt => attempt.ExamId == exam.Id && attempt.StudentId == studentId)
            .ToListAsync(cancellationToken);

        var open = attempts.FirstOrDefault(attempt => attempt.Status == ExamAttemptStatus.InProgress);

        if (open is not null)
        {
            if (!open.HasExpired(utcNow))
            {
                // Resuming: the same sitting, with whatever was already answered.
                return BuildSession(exam, open);
            }

            // The window closed while the student was away. Mark what was saved and move on.
            var needsGrading = ExamAttemptHelper.AutoGrade(exam, open, utcNow);
            open.Status = ExamAttemptStatus.Expired;
            open.SubmittedAt = utcNow;
            open.RequiresManualGrading = needsGrading;
            open.UpdatedAt = utcNow;

            await ExamAttemptHelper.UpsertResultAsync(
                context, exam, open, currentUser.RequireUserId(), utcNow, cancellationToken);
        }

        // Every closed sitting counts against the limit, expired ones included. The one that
        // was just expired above is already in this list with its new status.
        var used = attempts.Count(attempt => attempt.Status != ExamAttemptStatus.InProgress);

        if (used >= exam.MaxAttempts)
        {
            throw new ConflictException("You have used all the attempts allowed for this exam.");
        }

        var attempt = new ExamAttempt
        {
            Id = Guid.NewGuid(),
            TenantId = exam.TenantId,
            ExamId = exam.Id,
            StudentId = studentId,
            AttemptNumber = attempts.Count + 1,
            Status = ExamAttemptStatus.InProgress,
            StartedAt = utcNow,
            ExpiresAt = exam.DurationInMinutes > 0
                ? utcNow.AddMinutes(exam.DurationInMinutes)
                : null,
            MaximumScore = exam.Questions.Sum(question => question.Points),
            CreatedAt = utcNow
        };

        await context.ExamAttempts.AddAsync(attempt, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return BuildSession(exam, attempt);
    }

    /// <summary>
    /// Projects the paper for the student. The answer key never enters this shape, which is
    /// what keeps it out of the response.
    /// </summary>
    private static ExamAttemptSessionDto BuildSession(Exam exam, ExamAttempt attempt) => new()
    {
        AttemptId = attempt.Id,
        ExamId = exam.Id,
        ExamTitle = exam.Title,
        Description = exam.Description,
        AttemptNumber = attempt.AttemptNumber,
        MaxAttempts = exam.MaxAttempts,
        DurationInMinutes = exam.DurationInMinutes,
        StartedAt = attempt.StartedAt,
        ExpiresAt = attempt.ExpiresAt,
        MaximumScore = attempt.MaximumScore,
        Questions = exam.Questions
            .OrderBy(question => question.SortOrder)
            .Select(question => new StudentExamQuestionDto
            {
                Id = question.Id,
                Text = question.Text,
                Type = question.Type,
                Points = question.Points,
                SortOrder = question.SortOrder,
                SelectedOptionId = attempt.Answers
                    .FirstOrDefault(answer => answer.QuestionId == question.Id)?.SelectedOptionId,
                TextAnswer = attempt.Answers
                    .FirstOrDefault(answer => answer.QuestionId == question.Id)?.TextAnswer,
                Options = question.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new StudentExamOptionDto
                    {
                        Id = option.Id,
                        Text = option.Text,
                        SortOrder = option.SortOrder
                    })
                    .ToList()
            })
            .ToList()
    };
}

// ---------------------------------------------------------------------------
// Submit
// ---------------------------------------------------------------------------

/// <summary>One answer as the student submits it.</summary>
public sealed record ExamAnswerInput
{
    public Guid QuestionId { get; init; }

    public Guid? SelectedOptionId { get; init; }

    public string? TextAnswer { get; init; }
}

public sealed record SubmitExamAttemptCommand : IRequest<ExamAttemptDto>
{
    public Guid AttemptId { get; init; }

    public IReadOnlyList<ExamAnswerInput> Answers { get; init; } = [];
}

public sealed class SubmitExamAttemptCommandValidator : AbstractValidator<SubmitExamAttemptCommand>
{
    public SubmitExamAttemptCommandValidator()
    {
        RuleFor(command => command.AttemptId).NotEmpty();

        RuleFor(command => command.Answers)
            .Must(answers => answers.Select(answer => answer.QuestionId).Distinct().Count() == answers.Count)
            .WithMessage("Each question may be answered only once.");

        RuleForEach(command => command.Answers).ChildRules(answer =>
        {
            answer.RuleFor(item => item.QuestionId).NotEmpty();
            answer.RuleFor(item => item.TextAnswer).MaximumLength(DomainRules.DescriptionMaxLength);
        });
    }
}

public sealed class SubmitExamAttemptCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    IActivityRecorder activity,
    IMediator mediator) : IRequestHandler<SubmitExamAttemptCommand, ExamAttemptDto>
{
    public async Task<ExamAttemptDto> Handle(
        SubmitExamAttemptCommand command,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();
        var utcNow = dateTimeProvider.UtcNow;

        var attempt = await context.ExamAttempts
            .Include(entity => entity.Answers)
            .FirstOrDefaultAsync(entity => entity.Id == command.AttemptId, cancellationToken)
            ?? throw new NotFoundException("ExamAttempt", command.AttemptId);

        // An attempt belonging to someone else is not merely forbidden, it is invisible.
        if (attempt.StudentId != studentId)
        {
            throw new NotFoundException("ExamAttempt", command.AttemptId);
        }

        if (!attempt.IsOpen)
        {
            throw new ConflictException("This attempt has already been submitted.");
        }

        var exam = await context.Exams
            .Include(entity => entity.Questions)
            .ThenInclude(question => question.Options)
            .FirstAsync(entity => entity.Id == attempt.ExamId, cancellationToken);

        var expired = attempt.HasExpired(utcNow);

        foreach (var input in command.Answers)
        {
            var question = exam.Questions.FirstOrDefault(entity => entity.Id == input.QuestionId)
                ?? throw new NotFoundException("ExamQuestion", input.QuestionId);

            // An option id lifted from another question must not be markable.
            if (input.SelectedOptionId.HasValue
                && question.Options.All(option => option.Id != input.SelectedOptionId.Value))
            {
                throw new BusinessRuleException(
                    "ANSWER_OPTION_MISMATCH",
                    "The selected option does not belong to that question.");
            }

            var answer = attempt.Answers.FirstOrDefault(entity => entity.QuestionId == input.QuestionId);

            if (answer is null)
            {
                answer = new ExamAttemptAnswer
                {
                    Id = Guid.NewGuid(),
                    AttemptId = attempt.Id,
                    QuestionId = question.Id,
                    CreatedAt = utcNow
                };

                attempt.Answers.Add(answer);
            }

            answer.SelectedOptionId = input.SelectedOptionId;
            answer.TextAnswer = input.TextAnswer?.Trim();
            answer.MaximumPoints = question.Points;
            answer.AnsweredAt = utcNow;
            answer.UpdatedAt = utcNow;
        }

        var requiresManualGrading = ExamAttemptHelper.AutoGrade(exam, attempt, utcNow);

        if (expired)
        {
            // The clock ran out: whatever was stored is marked, and the sitting is closed as
            // expired rather than as a clean submission.
            attempt.Status = ExamAttemptStatus.Expired;
            attempt.RequiresManualGrading = requiresManualGrading;
            attempt.SubmittedAt = utcNow;
            attempt.UpdatedAt = utcNow;
        }
        else
        {
            attempt.Submit(utcNow, requiresManualGrading);
        }

        await ExamAttemptHelper.UpsertResultAsync(
            context, exam, attempt, currentUser.RequireUserId(), utcNow, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        await NotifySubmissionAsync(exam, attempt, expired, cancellationToken);

        if (requiresManualGrading)
        {
            await NotifyTeacherAsync(exam, cancellationToken);
        }

        await activity.RecordAsync(
            new ActivityRequest
            {
                Type = ActivityTypes.ExamCompleted,
                TenantId = exam.TenantId,
                ActorUserId = currentUser.UserId,
                StudentId = attempt.StudentId,
                RelatedEntityType = nameof(ExamAttempt),
                RelatedEntityId = attempt.Id,
                Visibility = ActivityVisibility.Shared,
                // One entry per sitting, so a retried submit adds nothing.
                DeduplicationKey = ActivityTypes.ExamCompleted + ":" + attempt.Id,
                Metadata = new Dictionary<string, string>
                {
                    ["exam"] = exam.Title,
                    ["examId"] = exam.Id.ToString(),
                    ["attemptNumber"] = attempt.AttemptNumber.ToString(),
                    // The mark is a display value the student already sees on their own result;
                    // it is not added for a sitting that is still awaiting a teacher.
                    ["percentage"] = requiresManualGrading
                        ? string.Empty
                        : attempt.Percentage.ToString("0.##"),
                    ["awaitingGrading"] = requiresManualGrading ? "true" : "false"
                }
            },
            cancellationToken);

        return await mediator.Send(new GetExamAttemptQuery(attempt.Id), cancellationToken);
    }

    /// <summary>
    /// Confirms the sitting to the student and tells the teacher it landed. The student's
    /// copy is keyed on the attempt, so a retried submit cannot produce two receipts.
    /// </summary>
    private async Task NotifySubmissionAsync(
        Exam exam,
        ExamAttempt attempt,
        bool expired,
        CancellationToken cancellationToken)
    {
        var studentUserId = await NotificationAudience.StudentUserIdAsync(
            context, attempt.StudentId, cancellationToken);

        if (studentUserId != Guid.Empty)
        {
            await notifications.SendAsync(
                studentUserId,
                new NotificationRequest
                {
                    Event = expired
                        ? NotificationEvents.ExamAttemptExpired
                        : NotificationEvents.ExamSubmitted,
                    TenantId = exam.TenantId,
                    ReferenceId = attempt.Id,
                    DeduplicationKey = "attempt-closed:" + attempt.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["exam"] = exam.Title,
                        ["attemptId"] = attempt.Id.ToString()
                    }
                },
                cancellationToken);
        }

        var teacherUserId = await NotificationAudience.TeacherUserIdAsync(
            context, exam.TeacherId, cancellationToken);

        if (teacherUserId != Guid.Empty)
        {
            var studentName = await NotificationAudience.StudentNameAsync(
                context, attempt.StudentId, cancellationToken);

            await notifications.SendAsync(
                teacherUserId,
                new NotificationRequest
                {
                    Event = NotificationEvents.ExamStudentSubmitted,
                    TenantId = exam.TenantId,
                    ReferenceId = attempt.Id,
                    DeduplicationKey = "attempt-received:" + attempt.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["student"] = studentName,
                        ["exam"] = exam.Title,
                        ["examId"] = exam.Id.ToString()
                    }
                },
                cancellationToken);
        }
    }

    /// <summary>Tells the workspace owner that an essay is waiting to be marked.</summary>
    private async Task NotifyTeacherAsync(Exam exam, CancellationToken cancellationToken)
    {
        var ownerUserId = await context.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == exam.TenantId)
            .Select(tenant => (Guid?)tenant.OwnerUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerUserId is null)
        {
            return;
        }

        await notifications.SendAsync(
            ownerUserId.Value,
            new NotificationRequest
            {
                Event = NotificationEvents.ExamAwaitingGrading,
                TenantId = exam.TenantId,
                ReferenceId = exam.Id,
                Values = new Dictionary<string, string>
                {
                    ["exam"] = exam.Title,
                    ["examId"] = exam.Id.ToString()
                }
            },
            cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Manual grading
// ---------------------------------------------------------------------------

/// <summary>Points a teacher awards for one answer.</summary>
public sealed record GradeAnswerInput
{
    public Guid AnswerId { get; init; }

    public decimal AwardedPoints { get; init; }

    public string? Feedback { get; init; }
}

/// <summary>Finalises a submitted attempt by marking its essay answers.</summary>
public sealed record GradeExamAttemptCommand : IRequest<ExamAttemptDto>
{
    public Guid AttemptId { get; init; }

    public IReadOnlyList<GradeAnswerInput> Answers { get; init; } = [];

    public string? TeacherComment { get; init; }
}

public sealed class GradeExamAttemptCommandValidator : AbstractValidator<GradeExamAttemptCommand>
{
    public GradeExamAttemptCommandValidator()
    {
        RuleFor(command => command.AttemptId).NotEmpty();
        RuleFor(command => command.TeacherComment).MaximumLength(DomainRules.ShortTextMaxLength);

        RuleFor(command => command.Answers)
            .Must(answers => answers.Select(answer => answer.AnswerId).Distinct().Count() == answers.Count)
            .WithMessage("Each answer may be graded only once.");

        RuleForEach(command => command.Answers).ChildRules(answer =>
        {
            answer.RuleFor(item => item.AnswerId).NotEmpty();
            answer.RuleFor(item => item.AwardedPoints).GreaterThanOrEqualTo(0);
            answer.RuleFor(item => item.Feedback).MaximumLength(DomainRules.ShortTextMaxLength);
        });
    }
}

public sealed class GradeExamAttemptCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger,
    IMediator mediator) : IRequestHandler<GradeExamAttemptCommand, ExamAttemptDto>
{
    public async Task<ExamAttemptDto> Handle(
        GradeExamAttemptCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.GradesManage, cancellationToken: cancellationToken);

        var attempt = await context.ExamAttempts
            .Include(entity => entity.Answers)
            .FirstOrDefaultAsync(entity => entity.Id == command.AttemptId, cancellationToken)
            ?? throw new NotFoundException("ExamAttempt", command.AttemptId);

        tenantAuthorization.EnsureSameTenant(access, attempt.TenantId);

        var exam = await context.Exams
            .Include(entity => entity.Questions)
            .FirstAsync(entity => entity.Id == attempt.ExamId, cancellationToken);

        var utcNow = dateTimeProvider.UtcNow;

        foreach (var input in command.Answers)
        {
            var answer = attempt.Answers.FirstOrDefault(entity => entity.Id == input.AnswerId)
                ?? throw new NotFoundException("ExamAttemptAnswer", input.AnswerId);

            if (input.AwardedPoints > answer.MaximumPoints)
            {
                throw new BusinessRuleException(
                    "ANSWER_SCORE_OUT_OF_RANGE",
                    "An answer cannot be awarded more than " + answer.MaximumPoints + " points.");
            }

            answer.AwardedPoints = input.AwardedPoints;
            answer.IsCorrect = input.AwardedPoints >= answer.MaximumPoints;
            answer.TeacherFeedback = input.Feedback?.Trim();
            answer.UpdatedAt = utcNow;
        }

        var total = attempt.Answers.Sum(answer => answer.AwardedPoints);
        var maximum = exam.Questions.Sum(question => question.Points);

        attempt.ApplyScore(total, maximum, exam.PassingScore, utcNow);
        attempt.CompleteManualGrading(access.UserId, utcNow, command.TeacherComment?.Trim());

        await ExamAttemptHelper.UpsertResultAsync(
            context, exam, attempt, access.UserId, utcNow, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.GradeRecorded,
            nameof(ExamAttempt),
            attempt.Id,
            newValues: new { attempt.Score, attempt.Percentage },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return await mediator.Send(new GetExamAttemptQuery(attempt.Id), cancellationToken);
    }
}
