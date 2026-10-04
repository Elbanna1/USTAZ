using Microsoft.EntityFrameworkCore;
using Shouldly;
using Ustaz.Application.Features.Notifications;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Tests.Handlers;

/// <summary>
/// Reminders are only useful before the thing happens, and only once. These tests pin the
/// window each reminder looks at, the fact that a student who already handed in is left
/// alone, and that running the pass twice changes nothing.
/// </summary>
public class ScheduledReminderTests : IDisposable
{
    private readonly TestHarness _harness = new();

    private DispatchScheduledRemindersCommandHandler Handler() =>
        new(_harness.Context, _harness.Clock, _harness.RealNotifications);

    private void EnrolStudent()
    {
        _harness.Context.GroupEnrollments.Add(new GroupEnrollment
        {
            Id = Guid.NewGuid(),
            GroupId = _harness.Group.Id,
            StudentId = _harness.Student.Id,
            Status = EnrollmentStatus.Active,
            EnrolledAt = _harness.UtcNow.AddDays(-10),
            PricePaid = 500m,
            FinalPrice = 500m
        });

        _harness.Context.SaveChanges();
    }

    private Homework SeedHomework(DateTime dueAt)
    {
        var homework = new Homework
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.Tenant.Id,
            TeacherId = _harness.Teacher.Id,
            GroupId = _harness.Group.Id,
            Title = "واجب الفيزياء",
            AssignedAt = _harness.UtcNow.AddDays(-2),
            DueAt = dueAt,
            MaximumScore = 10m,
            IsPublished = true,
            CreatedAt = _harness.UtcNow.AddDays(-2)
        };

        _harness.Context.Homeworks.Add(homework);
        _harness.Context.SaveChanges();

        return homework;
    }

    private Exam SeedExam(DateTime examDate)
    {
        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.Tenant.Id,
            TeacherId = _harness.Teacher.Id,
            GroupId = _harness.Group.Id,
            SubjectId = _harness.Subject.Id,
            Title = "امتحان الفيزياء",
            ExamDate = examDate,
            MaximumScore = 10m,
            PassingScore = 5m,
            IsPublished = true,
            CreatedAt = _harness.UtcNow.AddDays(-2)
        };

        _harness.Context.Exams.Add(exam);
        _harness.Context.SaveChanges();

        return exam;
    }

    private EducationalPayment SeedPayment(DateTime dueAt)
    {
        var payment = new EducationalPayment
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.Tenant.Id,
            StudentId = _harness.Student.Id,
            Type = EducationalPaymentType.GroupEnrollment,
            InvoiceNumber = "INV-1",
            Amount = 500m,
            NetAmount = 500m,
            Status = PaymentStatus.Pending,
            DueAt = dueAt,
            CreatedAt = _harness.UtcNow
        };

        _harness.Context.EducationalPayments.Add(payment);
        _harness.Context.SaveChanges();

        return payment;
    }

    // -----------------------------------------------------------------------
    // Homework
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_student_who_has_not_handed_in_is_reminded_before_the_deadline()
    {
        EnrolStudent();
        SeedHomework(_harness.UtcNow.AddHours(6));

        await Handler().Handle(new DispatchScheduledRemindersCommand(), CancellationToken.None);

        var notification = await _harness.Context.Notifications
            .SingleAsync(entry => entry.Event == "homework.due_soon");

        notification.UserId.ShouldBe(_harness.Student.UserId);
        notification.Title.ShouldBe("الديدلاين قرب ⏰");
    }

    [Fact]
    public async Task A_student_who_already_handed_in_is_left_alone()
    {
        EnrolStudent();
        var homework = SeedHomework(_harness.UtcNow.AddHours(6));

        _harness.Context.HomeworkSubmissions.Add(new HomeworkSubmission
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.Tenant.Id,
            HomeworkId = homework.Id,
            StudentId = _harness.Student.Id,
            Status = HomeworkSubmissionStatus.Submitted,
            SubmittedAt = _harness.UtcNow,
            CreatedAt = _harness.UtcNow
        });

        await _harness.Context.SaveChangesAsync();

        await Handler().Handle(new DispatchScheduledRemindersCommand(), CancellationToken.None);

        (await _harness.Context.Notifications.CountAsync(entry => entry.Event == "homework.due_soon"))
            .ShouldBe(0);
    }

    [Fact]
    public async Task A_deadline_beyond_the_window_is_not_announced_yet()
    {
        EnrolStudent();
        SeedHomework(_harness.UtcNow.AddDays(5));

        await Handler().Handle(new DispatchScheduledRemindersCommand(), CancellationToken.None);

        (await _harness.Context.Notifications.CountAsync(entry => entry.Event == "homework.due_soon"))
            .ShouldBe(0);
    }

    [Fact]
    public async Task Running_the_pass_twice_does_not_repeat_a_reminder()
    {
        EnrolStudent();
        SeedHomework(_harness.UtcNow.AddHours(6));

        var handler = Handler();

        await handler.Handle(new DispatchScheduledRemindersCommand(), CancellationToken.None);
        await handler.Handle(new DispatchScheduledRemindersCommand(), CancellationToken.None);

        (await _harness.Context.Notifications.CountAsync(entry => entry.Event == "homework.due_soon"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task A_guardian_hears_about_homework_that_was_missed()
    {
        EnrolStudent();
        SeedHomework(_harness.UtcNow.AddHours(-6));
        _harness.AddParent("guardian.missing@ustaz.test", _harness.Student.Id);

        await Handler().Handle(new DispatchScheduledRemindersCommand(), CancellationToken.None);

        var notification = await _harness.Context.Notifications
            .SingleAsync(entry => entry.Event == "homework.child_missing");

        notification.Message.ShouldContain("سلمى");
    }

    // -----------------------------------------------------------------------
    // Exams
    // -----------------------------------------------------------------------

    [Fact]
    public async Task An_exam_starting_soon_is_announced_to_the_group()
    {
        EnrolStudent();
        SeedExam(_harness.UtcNow.AddHours(3));

        await Handler().Handle(new DispatchScheduledRemindersCommand(), CancellationToken.None);

        var notification = await _harness.Context.Notifications
            .SingleAsync(entry => entry.Event == "exam.starting_soon");

        notification.UserId.ShouldBe(_harness.Student.UserId);
    }

    [Fact]
    public async Task An_exam_that_already_started_is_not_announced()
    {
        EnrolStudent();
        SeedExam(_harness.UtcNow.AddHours(-1));

        await Handler().Handle(new DispatchScheduledRemindersCommand(), CancellationToken.None);

        (await _harness.Context.Notifications.CountAsync(entry => entry.Event == "exam.starting_soon"))
            .ShouldBe(0);
    }

    // -----------------------------------------------------------------------
    // Payments
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Tuition_falling_due_reaches_the_student_and_the_guardian()
    {
        SeedPayment(_harness.UtcNow.AddDays(2));
        _harness.AddParent("guardian.pay@ustaz.test", _harness.Student.Id);

        await Handler().Handle(new DispatchScheduledRemindersCommand(), CancellationToken.None);

        var recipients = await _harness.Context.Notifications
            .Where(entry => entry.Event == "payment.due_soon")
            .Select(entry => entry.UserId)
            .ToListAsync();

        recipients.Count.ShouldBe(2);
        recipients.ShouldContain(_harness.Student.UserId);
    }

    [Fact]
    public async Task A_settled_payment_is_never_chased()
    {
        var payment = SeedPayment(_harness.UtcNow.AddDays(2));
        payment.MarkPaid(_harness.UtcNow);
        await _harness.Context.SaveChangesAsync();

        await Handler().Handle(new DispatchScheduledRemindersCommand(), CancellationToken.None);

        (await _harness.Context.Notifications.CountAsync(entry => entry.Event == "payment.due_soon"))
            .ShouldBe(0);
    }

    public void Dispose() => _harness.Dispose();
}
