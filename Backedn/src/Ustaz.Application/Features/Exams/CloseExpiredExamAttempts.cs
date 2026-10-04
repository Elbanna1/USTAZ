using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Exams;

/// <summary>
/// Closes every sitting whose window has passed.
///
/// Lazy expiry already runs when a student touches an attempt, but a student who simply walks
/// away leaves the row open forever — which makes the teacher's list wrong and the attempt
/// limit permanently unusable for that student. This is the sweep that finishes the job.
///
/// It is safe to run repeatedly and from more than one instance: only attempts still marked
/// <see cref="ExamAttemptStatus.InProgress"/> are selected, each is saved on its own, and the
/// attempt's concurrency token means a student who submitted at the same moment wins the race
/// while the sweep quietly skips that row.
/// </summary>
public sealed record CloseExpiredExamAttemptsCommand : IRequest<int>
{
    /// <summary>Upper bound per sweep, so one pass can never become a long transaction.</summary>
    public int BatchSize { get; init; } = 100;
}

public sealed class CloseExpiredExamAttemptsCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    ILogger<CloseExpiredExamAttemptsCommandHandler> logger)
    : IRequestHandler<CloseExpiredExamAttemptsCommand, int>
{
    public async Task<int> Handle(CloseExpiredExamAttemptsCommand command, CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;

        var expired = await context.ExamAttempts
            .Include(attempt => attempt.Answers)
            .Where(attempt => attempt.Status == ExamAttemptStatus.InProgress
                && attempt.ExpiresAt != null
                && attempt.ExpiresAt < utcNow)
            .OrderBy(attempt => attempt.ExpiresAt)
            .Take(Math.Clamp(command.BatchSize, 1, 500))
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return 0;
        }

        // One query for the exams behind this batch rather than one per attempt.
        var examIds = expired.Select(attempt => attempt.ExamId).Distinct().ToList();

        var exams = await context.Exams
            .Include(exam => exam.Questions)
            .ThenInclude(question => question.Options)
            .Where(exam => examIds.Contains(exam.Id))
            .ToDictionaryAsync(exam => exam.Id, cancellationToken);

        var closed = 0;

        foreach (var attempt in expired)
        {
            if (!exams.TryGetValue(attempt.ExamId, out var exam))
            {
                continue;
            }

            var requiresManualGrading = ExamAttemptHelper.AutoGrade(exam, attempt, utcNow);

            attempt.Status = ExamAttemptStatus.Expired;
            attempt.RequiresManualGrading = requiresManualGrading;
            attempt.SubmittedAt ??= attempt.ExpiresAt ?? utcNow;
            attempt.UpdatedAt = utcNow;

            await ExamAttemptHelper.UpsertResultAsync(
                context, exam, attempt, attempt.StudentId, utcNow, cancellationToken);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // The student submitted while we were closing it. Their outcome stands.
                logger.LogDebug(
                    "Attempt {AttemptId} was submitted while the expiry sweep ran; skipping.",
                    attempt.Id);

                continue;
            }

            closed++;

            await NotifyStudentAsync(exam.Title, exam.TenantId, attempt.Id, attempt.StudentId, cancellationToken);
        }

        if (closed > 0)
        {
            logger.LogInformation("Closed {Count} expired exam attempt(s).", closed);
        }

        return closed;
    }

    private async Task NotifyStudentAsync(
        string examTitle,
        Guid tenantId,
        Guid attemptId,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var studentUserId = await NotificationAudience.StudentUserIdAsync(context, studentId, cancellationToken);

        if (studentUserId == Guid.Empty)
        {
            return;
        }

        // Keyed on the attempt, so the lazy path and the sweep can never both tell the
        // student their time ran out.
        await notifications.SendAsync(
            studentUserId,
            new NotificationRequest
            {
                Event = NotificationEvents.ExamAttemptExpired,
                TenantId = tenantId,
                ReferenceId = attemptId,
                DeduplicationKey = "attempt-closed:" + attemptId,
                Values = new Dictionary<string, string>
                {
                    ["exam"] = examTitle,
                    ["attemptId"] = attemptId.ToString()
                }
            },
            cancellationToken);
    }
}
