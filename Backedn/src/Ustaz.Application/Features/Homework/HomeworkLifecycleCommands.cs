using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Homework;

// ---------------------------------------------------------------------------
// Teacher: close / reopen the assignment
// ---------------------------------------------------------------------------

/// <summary>
/// Stops accepting submissions, overriding the deadline and the late-submission setting alike.
/// Marks already awarded are untouched and the teacher can still grade what came in.
/// </summary>
public sealed record CloseHomeworkCommand(Guid HomeworkId) : IRequest<HomeworkDto>;

public sealed class CloseHomeworkCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    IMediator mediator) : IRequestHandler<CloseHomeworkCommand, HomeworkDto>
{
    public async Task<HomeworkDto> Handle(CloseHomeworkCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.HomeworkManage, cancellationToken: cancellationToken);

        var homework = await HomeworkHelper.LoadOwnedAsync(
            context, tenantAuthorization, access, command.HomeworkId, cancellationToken);

        homework.Close(dateTimeProvider.UtcNow);
        await context.SaveChangesAsync(cancellationToken);

        // Only tell the students who still had something outstanding; the rest have no reason
        // to hear that a door they already walked through is shut.
        var recipients = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.GroupId == homework.GroupId
                && enrollment.Status == EnrollmentStatus.Active
                && !context.HomeworkSubmissions.Any(submission =>
                    submission.HomeworkId == homework.Id
                    && submission.StudentId == enrollment.StudentId
                    && submission.Status != HomeworkSubmissionStatus.NotSubmitted))
            .Select(enrollment => enrollment.Student.UserId)
            .ToListAsync(cancellationToken);

        if (recipients.Count > 0)
        {
            await notifications.SendManyAsync(
                recipients,
                new NotificationRequest
                {
                    Event = NotificationEvents.HomeworkClosed,
                    TenantId = access.TenantId,
                    ReferenceId = homework.Id,
                    // Closing, reopening and closing again must not spam the group.
                    DeduplicationKey = "homework-closed:" + homework.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["homework"] = homework.Title,
                        ["homeworkId"] = homework.Id.ToString()
                    }
                },
                cancellationToken);
        }

        return await mediator.Send(new GetHomeworkByIdQuery(homework.Id), cancellationToken);
    }
}

/// <summary>Reopens a closed assignment so outstanding work can still be handed in.</summary>
public sealed record ReopenHomeworkCommand(Guid HomeworkId) : IRequest<HomeworkDto>;

public sealed class ReopenHomeworkCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IMediator mediator) : IRequestHandler<ReopenHomeworkCommand, HomeworkDto>
{
    public async Task<HomeworkDto> Handle(ReopenHomeworkCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.HomeworkManage, cancellationToken: cancellationToken);

        var homework = await HomeworkHelper.LoadOwnedAsync(
            context, tenantAuthorization, access, command.HomeworkId, cancellationToken);

        homework.Reopen(dateTimeProvider.UtcNow);
        await context.SaveChangesAsync(cancellationToken);

        return await mediator.Send(new GetHomeworkByIdQuery(homework.Id), cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Teacher: return work for revision
// ---------------------------------------------------------------------------

/// <summary>
/// Hands one submission back with a reason. The student gets one more attempt whatever the
/// assignment's resubmission policy says, which is what makes returning meaningful.
/// </summary>
public sealed record ReturnHomeworkSubmissionCommand : IRequest<HomeworkSubmissionDto>
{
    public Guid SubmissionId { get; init; }

    public required string Reason { get; init; }
}

public sealed class ReturnHomeworkSubmissionCommandValidator
    : AbstractValidator<ReturnHomeworkSubmissionCommand>
{
    public ReturnHomeworkSubmissionCommandValidator()
    {
        RuleFor(command => command.SubmissionId).NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty().WithMessage("Tell the student what to fix.")
            .MaximumLength(DomainRules.ShortTextMaxLength);
    }
}

public sealed class ReturnHomeworkSubmissionCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    IAuditLogger auditLogger) : IRequestHandler<ReturnHomeworkSubmissionCommand, HomeworkSubmissionDto>
{
    public async Task<HomeworkSubmissionDto> Handle(
        ReturnHomeworkSubmissionCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.HomeworkManage, cancellationToken: cancellationToken);

        var submission = await context.HomeworkSubmissions
            .Include(entity => entity.Homework)
            .Include(entity => entity.Student)
            .FirstOrDefaultAsync(entity => entity.Id == command.SubmissionId, cancellationToken)
            ?? throw new NotFoundException("HomeworkSubmission", command.SubmissionId);

        tenantAuthorization.EnsureSameTenant(access, submission.TenantId);

        var reason = command.Reason.Trim();
        var utcNow = dateTimeProvider.UtcNow;

        submission.ReturnForRevision(reason, access.UserId, utcNow);

        // Returning work the student cannot act on would be a dead end, so a closed assignment
        // is reopened as part of the same transaction.
        if (submission.Homework.IsClosed)
        {
            submission.Homework.Reopen(utcNow);
        }

        await context.SaveChangesAsync(cancellationToken);

        await notifications.SendAsync(
            submission.Student.UserId,
            new NotificationRequest
            {
                Event = NotificationEvents.HomeworkReturned,
                TenantId = access.TenantId,
                ReferenceId = submission.Id,
                Values = new Dictionary<string, string>
                {
                    ["homework"] = submission.Homework.Title,
                    ["homeworkId"] = submission.HomeworkId.ToString(),
                    ["reason"] = reason
                }
            },
            cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.HomeworkReturned,
            nameof(HomeworkSubmission),
            submission.Id,
            newValues: new { submission.Status, submission.ReturnedAt },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(entity => entity.Id == submission.Id)
            .Select(HomeworkProjections.ToSubmissionDto)
            .FirstAsync(cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Teacher: bulk grade
// ---------------------------------------------------------------------------

public sealed record BulkGradeEntry
{
    public Guid SubmissionId { get; init; }

    public decimal Score { get; init; }

    public string? Feedback { get; init; }
}

/// <summary>
/// Marks a whole batch of submissions for one assignment in a single request and a single
/// transaction. Either every entry is applied or none is, so a partially marked class cannot
/// result from one bad row.
/// </summary>
public sealed record BulkGradeHomeworkCommand : IRequest<BulkGradeResultDto>
{
    public Guid HomeworkId { get; init; }

    public IReadOnlyList<BulkGradeEntry> Grades { get; init; } = [];
}

public sealed record BulkGradeResultDto
{
    public Guid HomeworkId { get; init; }

    public int GradedCount { get; init; }

    public IReadOnlyList<HomeworkSubmissionDto> Submissions { get; init; } = [];
}

public sealed class BulkGradeHomeworkCommandValidator : AbstractValidator<BulkGradeHomeworkCommand>
{
    /// <summary>Caps one request so a single call cannot lock the table for an unbounded time.</summary>
    public const int MaxEntries = 200;

    public BulkGradeHomeworkCommandValidator()
    {
        RuleFor(command => command.HomeworkId).NotEmpty();

        RuleFor(command => command.Grades)
            .NotEmpty().WithMessage("Supply at least one grade.")
            .Must(grades => grades.Count <= MaxEntries)
            .WithMessage("Grade at most " + MaxEntries + " submissions per request.")
            .Must(grades => grades.Select(entry => entry.SubmissionId).Distinct().Count() == grades.Count)
            .WithMessage("Each submission may appear only once.");

        RuleForEach(command => command.Grades).ChildRules(entry =>
        {
            entry.RuleFor(item => item.SubmissionId).NotEmpty();
            entry.RuleFor(item => item.Score).GreaterThanOrEqualTo(0);
            entry.RuleFor(item => item.Feedback).MaximumLength(DomainRules.ShortTextMaxLength);
        });
    }
}

public sealed class BulkGradeHomeworkCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    IAuditLogger auditLogger) : IRequestHandler<BulkGradeHomeworkCommand, BulkGradeResultDto>
{
    public async Task<BulkGradeResultDto> Handle(
        BulkGradeHomeworkCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.HomeworkManage, cancellationToken: cancellationToken);

        var homework = await HomeworkHelper.LoadOwnedAsync(
            context, tenantAuthorization, access, command.HomeworkId, cancellationToken);

        var ids = command.Grades.Select(entry => entry.SubmissionId).ToList();

        var submissions = await context.HomeworkSubmissions
            .Include(entity => entity.Student)
            .Where(entity => ids.Contains(entity.Id))
            .ToListAsync(cancellationToken);

        // Every id has to resolve to a submission on this assignment. A stray id is refused
        // outright rather than skipped, so the caller is never told it graded something it did
        // not; anything from another assignment or workspace reads as not found.
        foreach (var id in ids)
        {
            var submission = submissions.FirstOrDefault(entity => entity.Id == id);

            if (submission is null || submission.HomeworkId != homework.Id)
            {
                throw new NotFoundException("HomeworkSubmission", id);
            }

            tenantAuthorization.EnsureSameTenant(access, submission.TenantId);
        }

        var utcNow = dateTimeProvider.UtcNow;

        // Grade everything before saving: a score out of range throws here, and nothing has
        // been written yet, so the batch is all-or-nothing.
        foreach (var entry in command.Grades)
        {
            var submission = submissions.First(candidate => candidate.Id == entry.SubmissionId);

            submission.Grade(
                entry.Score,
                homework.MaximumScore,
                entry.Feedback?.Trim(),
                access.UserId,
                utcNow);
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var submission in submissions)
        {
            await notifications.SendAsync(
                submission.Student.UserId,
                new NotificationRequest
                {
                    Event = NotificationEvents.HomeworkGraded,
                    TenantId = access.TenantId,
                    ReferenceId = submission.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["homework"] = homework.Title,
                        ["homeworkId"] = homework.Id.ToString(),
                        ["score"] = (submission.Score ?? 0m).ToString("0.##"),
                        ["max"] = (submission.MaximumScore ?? 0m).ToString("0.##")
                    }
                },
                cancellationToken);
        }

        await auditLogger.LogAsync(
            AuditActions.HomeworkBulkGraded,
            nameof(Domain.Entities.Education.Homework),
            homework.Id,
            newValues: new { GradedCount = submissions.Count },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        var graded = await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(entity => ids.Contains(entity.Id))
            .OrderBy(entity => entity.Student.User.DisplayName)
            .Select(HomeworkProjections.ToSubmissionDto)
            .ToListAsync(cancellationToken);

        return new BulkGradeResultDto
        {
            HomeworkId = homework.Id,
            GradedCount = graded.Count,
            Submissions = graded
        };
    }
}
