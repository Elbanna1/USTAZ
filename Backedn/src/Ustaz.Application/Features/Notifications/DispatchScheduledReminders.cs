using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Notifications;

/// <summary>
/// The reminders that have to arrive *before* something happens rather than after it: a
/// homework deadline, an exam about to start, tuition coming due, and a guardian whose child
/// still has not handed work in.
///
/// Every reminder is keyed on the thing it is reminding about, so running this pass hourly,
/// daily, or twice by accident produces exactly one message per student per item.
/// </summary>
public sealed record DispatchScheduledRemindersCommand : IRequest<int>
{
    /// <summary>How far ahead to look for a homework deadline.</summary>
    public int HomeworkDueWithinHours { get; init; } = 24;

    /// <summary>How far ahead to look for an exam.</summary>
    public int ExamStartingWithinHours { get; init; } = 24;

    /// <summary>How far ahead to look for tuition falling due.</summary>
    public int PaymentDueWithinDays { get; init; } = 3;
}

public sealed class DispatchScheduledRemindersCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications)
    : IRequestHandler<DispatchScheduledRemindersCommand, int>
{
    public async Task<int> Handle(
        DispatchScheduledRemindersCommand command,
        CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;

        var sent = 0;

        sent += await RemindHomeworkAsync(command, utcNow, cancellationToken);
        sent += await RemindExamsAsync(command, utcNow, cancellationToken);
        sent += await RemindPaymentsAsync(command, utcNow, cancellationToken);

        return sent;
    }

    /// <summary>
    /// Nudges students whose homework is due soon and who have not submitted, and tells their
    /// guardians about the ones that are already overdue.
    /// </summary>
    private async Task<int> RemindHomeworkAsync(
        DispatchScheduledRemindersCommand command,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var horizon = utcNow.AddHours(Math.Clamp(command.HomeworkDueWithinHours, 1, 168));

        var due = await context.Homeworks
            .AsNoTracking()
            .Where(homework => homework.IsPublished
                && homework.DueAt > utcNow
                && homework.DueAt <= horizon)
            .Select(homework => new
            {
                homework.Id,
                homework.Title,
                homework.DueAt,
                homework.GroupId,
                homework.TenantId
            })
            .ToListAsync(cancellationToken);

        var sent = 0;

        foreach (var homework in due)
        {
            // Only the students who still owe it: one query per homework, not per student.
            var outstanding = await context.GroupEnrollments
                .AsNoTracking()
                .Where(enrollment => enrollment.GroupId == homework.GroupId
                    && enrollment.Status == EnrollmentStatus.Active
                    && !context.HomeworkSubmissions.Any(submission =>
                        submission.HomeworkId == homework.Id
                        && submission.StudentId == enrollment.StudentId
                        && submission.Status != HomeworkSubmissionStatus.NotSubmitted))
                .Select(enrollment => enrollment.Student.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (outstanding.Count == 0)
            {
                continue;
            }

            sent += await notifications.SendManyAsync(
                outstanding,
                new NotificationRequest
                {
                    Event = NotificationEvents.HomeworkDueSoon,
                    TenantId = homework.TenantId,
                    ReferenceId = homework.Id,
                    DeduplicationKey = "homework-due:" + homework.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["homework"] = homework.Title,
                        ["homeworkId"] = homework.Id.ToString(),
                        ["due"] = homework.DueAt.ToString("yyyy-MM-dd HH:mm")
                    }
                },
                cancellationToken);
        }

        sent += await WarnGuardiansAboutMissingHomeworkAsync(utcNow, cancellationToken);

        return sent;
    }

    /// <summary>Tells guardians about homework that is now past its deadline and unsubmitted.</summary>
    private async Task<int> WarnGuardiansAboutMissingHomeworkAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        // A narrow window: yesterday's deadlines, so this never re-walks the whole history.
        var from = utcNow.AddDays(-2);

        var overdue = await context.Homeworks
            .AsNoTracking()
            .Where(homework => homework.IsPublished && homework.DueAt < utcNow && homework.DueAt >= from)
            .Select(homework => new
            {
                homework.Id,
                homework.Title,
                homework.DueAt,
                homework.GroupId,
                homework.TenantId
            })
            .ToListAsync(cancellationToken);

        var sent = 0;

        foreach (var homework in overdue)
        {
            var students = await context.GroupEnrollments
                .AsNoTracking()
                .Where(enrollment => enrollment.GroupId == homework.GroupId
                    && enrollment.Status == EnrollmentStatus.Active
                    && !context.HomeworkSubmissions.Any(submission =>
                        submission.HomeworkId == homework.Id
                        && submission.StudentId == enrollment.StudentId
                        && submission.Status != HomeworkSubmissionStatus.NotSubmitted))
                .Select(enrollment => new
                {
                    enrollment.StudentId,
                    StudentName = enrollment.Student.User.DisplayName
                })
                .ToListAsync(cancellationToken);

            foreach (var student in students)
            {
                var guardians = await NotificationAudience.GuardianUserIdsAsync(
                    context, student.StudentId, cancellationToken);

                if (guardians.Count == 0)
                {
                    continue;
                }

                sent += await notifications.SendManyAsync(
                    guardians,
                    new NotificationRequest
                    {
                        Event = NotificationEvents.ChildHomeworkMissing,
                        TenantId = homework.TenantId,
                        ReferenceId = homework.Id,
                        DeduplicationKey = "homework-missing:" + homework.Id + ":" + student.StudentId,
                        Values = new Dictionary<string, string>
                        {
                            ["student"] = student.StudentName,
                            ["studentId"] = student.StudentId.ToString(),
                            ["homework"] = homework.Title,
                            ["due"] = homework.DueAt.ToString("yyyy-MM-dd HH:mm")
                        }
                    },
                    cancellationToken);
            }
        }

        return sent;
    }

    /// <summary>Warns a group when one of its exams is about to open.</summary>
    private async Task<int> RemindExamsAsync(
        DispatchScheduledRemindersCommand command,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var horizon = utcNow.AddHours(Math.Clamp(command.ExamStartingWithinHours, 1, 168));

        var upcoming = await context.Exams
            .AsNoTracking()
            .Where(exam => exam.IsPublished
                && exam.GroupId != null
                && exam.ExamDate > utcNow
                && exam.ExamDate <= horizon)
            .Select(exam => new { exam.Id, exam.Title, exam.ExamDate, exam.GroupId, exam.TenantId })
            .ToListAsync(cancellationToken);

        var sent = 0;

        foreach (var exam in upcoming)
        {
            var students = await NotificationAudience.GroupStudentUserIdsAsync(
                context, exam.GroupId!.Value, cancellationToken);

            if (students.Count == 0)
            {
                continue;
            }

            sent += await notifications.SendManyAsync(
                students,
                new NotificationRequest
                {
                    Event = NotificationEvents.ExamStartingSoon,
                    TenantId = exam.TenantId,
                    ReferenceId = exam.Id,
                    DeduplicationKey = "exam-soon:" + exam.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["exam"] = exam.Title,
                        ["examId"] = exam.Id.ToString(),
                        ["date"] = exam.ExamDate.ToString("yyyy-MM-dd HH:mm")
                    }
                },
                cancellationToken);
        }

        return sent;
    }

    /// <summary>Reminds a student and their guardians that tuition is about to fall due.</summary>
    private async Task<int> RemindPaymentsAsync(
        DispatchScheduledRemindersCommand command,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var horizon = utcNow.AddDays(Math.Clamp(command.PaymentDueWithinDays, 1, 30));

        var due = await context.EducationalPayments
            .AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Pending
                && payment.DueAt != null
                && payment.DueAt > utcNow
                && payment.DueAt <= horizon)
            .Select(payment => new { payment.Id, payment.StudentId, payment.NetAmount, payment.DueAt, payment.TenantId })
            .ToListAsync(cancellationToken);

        var sent = 0;

        foreach (var payment in due)
        {
            var recipients = new List<Guid>();

            var studentUserId = await NotificationAudience.StudentUserIdAsync(
                context, payment.StudentId, cancellationToken);

            if (studentUserId != Guid.Empty)
            {
                recipients.Add(studentUserId);
            }

            recipients.AddRange(await NotificationAudience.GuardianUserIdsAsync(
                context, payment.StudentId, cancellationToken));

            if (recipients.Count == 0)
            {
                continue;
            }

            sent += await notifications.SendManyAsync(
                recipients,
                new NotificationRequest
                {
                    Event = NotificationEvents.PaymentDueSoon,
                    TenantId = payment.TenantId,
                    ReferenceId = payment.Id,
                    DeduplicationKey = "payment-due:" + payment.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["amount"] = payment.NetAmount.ToString("0.##"),
                        ["due"] = payment.DueAt!.Value.ToString("yyyy-MM-dd")
                    }
                },
                cancellationToken);
        }

        return sent;
    }
}
