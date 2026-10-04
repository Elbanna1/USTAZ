using Microsoft.EntityFrameworkCore;
using Shouldly;
using Ustaz.Application.Features.Analytics;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Tests.Handlers;

/// <summary>
/// The at-risk system has to be explainable and deterministic: every flag comes from a real
/// measurement against a stated threshold. These tests drive each rule on its own, then check
/// that the workspace boundary and the parent alerts behave.
/// </summary>
public class StudentRiskTests : IDisposable
{
    private readonly TestHarness _harness = new();

    private StudentRiskAnalyzer Analyzer() => new(_harness.Context, _harness.Clock);

    private AttendanceSession SeedSession(DateTime date)
    {
        var session = new AttendanceSession
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.Tenant.Id,
            GroupId = _harness.Group.Id,
            SessionDate = date.Date,
            Status = AttendanceSessionStatus.Closed,
            CreatedAt = date
        };

        _harness.Context.AttendanceSessions.Add(session);
        return session;
    }

    private void SeedAttendance(int present, int absent)
    {
        for (var index = 0; index < present + absent; index++)
        {
            var session = SeedSession(_harness.UtcNow.AddDays(-index - 1));

            _harness.Context.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                TenantId = _harness.Tenant.Id,
                AttendanceSessionId = session.Id,
                Session = session,
                StudentId = _harness.Student.Id,
                Status = index < present ? AttendanceStatus.Present : AttendanceStatus.Absent,
                CreatedAt = _harness.UtcNow
            });
        }

        _harness.Context.SaveChanges();
    }

    private void SeedHomework(int assigned, int submitted)
    {
        _harness.Context.GroupEnrollments.Add(new GroupEnrollment
        {
            Id = Guid.NewGuid(),
            GroupId = _harness.Group.Id,
            StudentId = _harness.Student.Id,
            Status = EnrollmentStatus.Active,
            EnrolledAt = _harness.UtcNow.AddDays(-20),
            PricePaid = 500m,
            FinalPrice = 500m
        });

        for (var index = 0; index < assigned; index++)
        {
            var homework = new Homework
            {
                Id = Guid.NewGuid(),
                TenantId = _harness.Tenant.Id,
                TeacherId = _harness.Teacher.Id,
                GroupId = _harness.Group.Id,
                Title = "واجب " + index,
                AssignedAt = _harness.UtcNow.AddDays(-10),
                DueAt = _harness.UtcNow.AddDays(-2),
                MaximumScore = 10m,
                IsPublished = true,
                CreatedAt = _harness.UtcNow.AddDays(-10)
            };

            _harness.Context.Homeworks.Add(homework);

            if (index < submitted)
            {
                _harness.Context.HomeworkSubmissions.Add(new HomeworkSubmission
                {
                    Id = Guid.NewGuid(),
                    TenantId = _harness.Tenant.Id,
                    HomeworkId = homework.Id,
                    StudentId = _harness.Student.Id,
                    Status = HomeworkSubmissionStatus.Submitted,
                    SubmittedAt = _harness.UtcNow.AddDays(-3),
                    CreatedAt = _harness.UtcNow.AddDays(-3)
                });
            }
        }

        _harness.Context.SaveChanges();
    }

    private void SeedExamResult(decimal percentage)
    {
        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.Tenant.Id,
            TeacherId = _harness.Teacher.Id,
            GroupId = _harness.Group.Id,
            SubjectId = _harness.Subject.Id,
            Title = "امتحان",
            ExamDate = _harness.UtcNow.AddDays(-5),
            MaximumScore = 100m,
            PassingScore = 50m,
            IsPublished = true,
            CreatedAt = _harness.UtcNow.AddDays(-6)
        };

        _harness.Context.Exams.Add(exam);

        _harness.Context.ExamResults.Add(new ExamResult
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.Tenant.Id,
            ExamId = exam.Id,
            StudentId = _harness.Student.Id,
            Score = percentage,
            MaximumScore = 100m,
            Percentage = percentage,
            IsPassed = percentage >= 50m,
            RecordedAt = _harness.UtcNow.AddDays(-4),
            CreatedAt = _harness.UtcNow.AddDays(-4)
        });

        _harness.Context.SaveChanges();
    }

    // -----------------------------------------------------------------------
    // The rules
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_student_with_nothing_against_them_is_not_flagged()
    {
        var students = await Analyzer().AnalyzeTenantAsync(_harness.Tenant.Id, 30, CancellationToken.None);

        var student = students.Single(entry => entry.StudentId == _harness.Student.Id);
        student.RiskLevel.ShouldBe(StudentRiskLevel.None);
        student.Reasons.ShouldBeEmpty();
    }

    [Fact]
    public async Task Poor_attendance_is_flagged_with_the_rate_that_caused_it()
    {
        // 4 of 10 sessions attended: below the critical threshold.
        SeedAttendance(present: 4, absent: 6);

        var student = (await Analyzer().AnalyzeTenantAsync(_harness.Tenant.Id, 30, CancellationToken.None))
            .Single(entry => entry.StudentId == _harness.Student.Id);

        student.AttendanceRate.ShouldBe(40m);
        student.AbsenceCount.ShouldBe(6);
        student.Reasons.ShouldContain(reason => reason.Code == "attendance.critical");
        student.Reasons.First(reason => reason.Code == "attendance.critical").Value.ShouldBe(40m);
    }

    [Fact]
    public async Task Missing_homework_is_counted_and_explained()
    {
        SeedHomework(assigned: 5, submitted: 1);

        var student = (await Analyzer().AnalyzeTenantAsync(_harness.Tenant.Id, 30, CancellationToken.None))
            .Single(entry => entry.StudentId == _harness.Student.Id);

        student.MissingHomeworkCount.ShouldBe(4);
        student.HomeworkCompletionRate.ShouldBe(20m);
        student.Reasons.ShouldContain(reason => reason.Code == "homework.missing");
    }

    [Fact]
    public async Task A_low_exam_average_is_flagged()
    {
        SeedExamResult(percentage: 35m);

        var student = (await Analyzer().AnalyzeTenantAsync(_harness.Tenant.Id, 30, CancellationToken.None))
            .Single(entry => entry.StudentId == _harness.Student.Id);

        student.AverageExamPercentage.ShouldBe(35m);
        student.FailedExamCount.ShouldBe(1);
        student.Reasons.ShouldContain(reason => reason.Code == "exams.low");
    }

    [Fact]
    public async Task A_good_exam_average_is_not_flagged()
    {
        SeedExamResult(percentage: 88m);

        var student = (await Analyzer().AnalyzeTenantAsync(_harness.Tenant.Id, 30, CancellationToken.None))
            .Single(entry => entry.StudentId == _harness.Student.Id);

        student.Reasons.ShouldNotContain(reason => reason.Code == "exams.low");
    }

    [Fact]
    public async Task Problems_stack_into_a_high_risk_level()
    {
        SeedAttendance(present: 3, absent: 7);
        SeedHomework(assigned: 5, submitted: 0);
        SeedExamResult(percentage: 30m);

        var student = (await Analyzer().AnalyzeTenantAsync(_harness.Tenant.Id, 30, CancellationToken.None))
            .Single(entry => entry.StudentId == _harness.Student.Id);

        student.RiskLevel.ShouldBe(StudentRiskLevel.High);
        student.RiskScore.ShouldBeGreaterThanOrEqualTo(60);

        // Every flag is readable back as a sentence with its own number.
        student.Reasons.Count.ShouldBeGreaterThanOrEqualTo(3);
        student.Reasons.ShouldAllBe(reason => reason.MessageAr.Length > 0 && reason.MessageEn.Length > 0);
    }

    [Fact]
    public async Task The_analysis_never_reaches_into_another_workspace()
    {
        SeedAttendance(present: 0, absent: 8);

        var otherWorkspace = await Analyzer().AnalyzeTenantAsync(
            _harness.OtherTenant.Id, 30, CancellationToken.None);

        otherWorkspace.ShouldBeEmpty();
    }

    // -----------------------------------------------------------------------
    // The query surface
    // -----------------------------------------------------------------------

    [Fact]
    public async Task The_at_risk_list_needs_the_analytics_permission()
    {
        var member = _harness.AddMember(
            _harness.Tenant, "nosy@ustaz.test", TenantMemberRole.Assistant, TenantPermissions.GroupsView);

        _harness.SignInAsMember(
            await _harness.Context.Users.FirstAsync(user => user.Id == member.UserId), _harness.Tenant);

        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            new GetAtRiskStudentsQueryHandler(_harness.TenantAuthorization, Analyzer())
                .Handle(new GetAtRiskStudentsQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task The_at_risk_list_returns_the_worst_first_and_stays_bounded()
    {
        SeedAttendance(present: 1, absent: 9);
        _harness.SignInAsTeacher();

        var students = await new GetAtRiskStudentsQueryHandler(_harness.TenantAuthorization, Analyzer())
            .Handle(new GetAtRiskStudentsQuery { Take = 5 }, CancellationToken.None);

        students.Count.ShouldBe(1);
        students[0].StudentId.ShouldBe(_harness.Student.Id);
        students[0].RiskScore.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task A_student_from_another_workspace_is_not_found()
    {
        _harness.SignInAsTeacher();

        await Should.ThrowAsync<NotFoundException>(() =>
            new GetStudentRiskQueryHandler(_harness.TenantAuthorization, Analyzer())
                .Handle(new GetStudentRiskQuery(Guid.NewGuid()), CancellationToken.None));
    }

    // -----------------------------------------------------------------------
    // Parent early warning
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_guardian_is_warned_once_a_week_about_a_struggling_child()
    {
        SeedAttendance(present: 1, absent: 9);
        SeedHomework(assigned: 5, submitted: 0);
        SeedExamResult(percentage: 25m);

        _harness.AddParent("guardian@ustaz.test", _harness.Student.Id);

        var handler = new DispatchParentEarlyWarningsCommandHandler(
            _harness.Context, Analyzer(), _harness.Clock, _harness.RealNotifications);

        await handler.Handle(new DispatchParentEarlyWarningsCommand(), CancellationToken.None);

        // The guardian is told, and so is the teacher who can act on it.
        var guardianAlert = await _harness.Context.Notifications
            .SingleAsync(entry => entry.Event == "risk.child_falling_behind");

        guardianAlert.Message.ShouldContain("سلمى");

        (await _harness.Context.Notifications.CountAsync(entry => entry.Event == "risk.student_at_risk"))
            .ShouldBe(1);

        // A second pass in the same week says nothing new.
        (await handler.Handle(new DispatchParentEarlyWarningsCommand(), CancellationToken.None))
            .ShouldBe(0);

        (await _harness.Context.Notifications.CountAsync(entry => entry.Event!.StartsWith("risk.")))
            .ShouldBe(2);
    }

    [Fact]
    public async Task A_healthy_student_generates_no_parent_alert()
    {
        SeedAttendance(present: 10, absent: 0);
        _harness.AddParent("calm@ustaz.test", _harness.Student.Id);

        var delivered = await new DispatchParentEarlyWarningsCommandHandler(
                _harness.Context, Analyzer(), _harness.Clock, _harness.RealNotifications)
            .Handle(new DispatchParentEarlyWarningsCommand(), CancellationToken.None);

        // Nobody is flagged, so neither the guardian nor the teacher hears anything.
        delivered.ShouldBe(0);
    }

    [Fact]
    public async Task An_unconfirmed_guardian_is_not_told_anything()
    {
        SeedAttendance(present: 0, absent: 10);
        SeedHomework(assigned: 5, submitted: 0);

        _harness.AddParent("pending@ustaz.test", _harness.Student.Id, confirmed: false);

        await new DispatchParentEarlyWarningsCommandHandler(
                _harness.Context, Analyzer(), _harness.Clock, _harness.RealNotifications)
            .Handle(new DispatchParentEarlyWarningsCommand(), CancellationToken.None);

        // An unconfirmed guardian hears nothing; the teacher still does.
        (await _harness.Context.Notifications.CountAsync(entry => entry.Event == "risk.child_falling_behind"))
            .ShouldBe(0);
    }

    public void Dispose() => _harness.Dispose();
}
