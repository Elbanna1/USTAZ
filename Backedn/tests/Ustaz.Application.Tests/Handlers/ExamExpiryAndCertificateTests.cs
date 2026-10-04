using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Ustaz.Application.Features.Certificates;
using Ustaz.Application.Features.Enrollments.Commands;
using Ustaz.Application.Features.Exams;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Tests.Handlers;

/// <summary>
/// Two things that have to happen without anyone pressing a button: an abandoned exam sitting
/// gets closed, and a finished course produces a certificate that can be verified in public.
/// </summary>
public class ExamExpiryAndCertificateTests : IDisposable
{
    private readonly TestHarness _harness = new();
    private readonly Mock<IMediator> _mediator = new();

    private CloseExpiredExamAttemptsCommandHandler SweepHandler() =>
        new(_harness.Context,
            _harness.Clock,
            _harness.RealNotifications,
            NullLogger<CloseExpiredExamAttemptsCommandHandler>.Instance);

    private StartExamAttemptCommandHandler StartHandler() =>
        new(_harness.Context, _harness.CurrentUser.Object, _harness.Clock);

    private SubmitExamAttemptCommandHandler SubmitHandler() =>
        new(_harness.Context,
            _harness.CurrentUser.Object,
            _harness.Clock,
            _harness.Notifications.Object,
            _harness.Activity,
            _mediator.Object);

    // -----------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------
    private (Exam Exam, ExamQuestion Question) SeedOnlineExam(int durationInMinutes = 30)
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

        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.Tenant.Id,
            TeacherId = _harness.Teacher.Id,
            GroupId = _harness.Group.Id,
            SubjectId = _harness.Subject.Id,
            Title = "اختبار الفيزياء",
            Type = ExamType.Quiz,
            ExamDate = _harness.UtcNow.AddHours(-1),
            DurationInMinutes = durationInMinutes,
            MaximumScore = 10m,
            PassingScore = 5m,
            IsOnline = true,
            MaxAttempts = 2,
            ShowResultsImmediately = true,
            IsPublished = true,
            CreatedAt = _harness.UtcNow.AddDays(-1)
        };

        var question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            TenantId = exam.TenantId,
            ExamId = exam.Id,
            Text = "ما وحدة القوة؟",
            Type = ExamQuestionType.MultipleChoice,
            Points = 10m,
            SortOrder = 1,
            CreatedAt = _harness.UtcNow
        };

        question.Options.Add(new ExamQuestionOption
        {
            Id = Guid.NewGuid(), QuestionId = question.Id, Text = "نيوتن", IsCorrect = true, SortOrder = 1
        });

        question.Options.Add(new ExamQuestionOption
        {
            Id = Guid.NewGuid(), QuestionId = question.Id, Text = "جول", IsCorrect = false, SortOrder = 2
        });

        _harness.Context.Exams.Add(exam);
        _harness.Context.ExamQuestions.Add(question);
        _harness.Context.SaveChanges();

        return (exam, question);
    }

    // -----------------------------------------------------------------------
    // Expiry
    // -----------------------------------------------------------------------

    [Fact]
    public async Task The_sweep_closes_a_sitting_whose_window_has_passed()
    {
        var (exam, question) = SeedOnlineExam();
        _harness.SignInAsStudent();

        var session = await StartHandler().Handle(
            new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        // The student walks away; the clock runs past the window.
        _harness.UtcNow = _harness.UtcNow.AddHours(2);

        var closed = await SweepHandler().Handle(
            new CloseExpiredExamAttemptsCommand(), CancellationToken.None);

        closed.ShouldBe(1);

        var attempt = await _harness.Context.ExamAttempts.FirstAsync();
        attempt.Id.ShouldBe(session.AttemptId);
        attempt.Status.ShouldBe(ExamAttemptStatus.Expired);
        attempt.SubmittedAt.ShouldNotBeNull();
        _ = question;
    }

    [Fact]
    public async Task The_sweep_leaves_a_sitting_that_is_still_inside_its_window()
    {
        var (exam, _) = SeedOnlineExam();
        _harness.SignInAsStudent();

        await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        _harness.UtcNow = _harness.UtcNow.AddMinutes(5);

        (await SweepHandler().Handle(new CloseExpiredExamAttemptsCommand(), CancellationToken.None))
            .ShouldBe(0);

        var attempt = await _harness.Context.ExamAttempts.FirstAsync();
        attempt.Status.ShouldBe(ExamAttemptStatus.InProgress);
    }

    [Fact]
    public async Task The_sweep_is_idempotent()
    {
        var (exam, _) = SeedOnlineExam();
        _harness.SignInAsStudent();

        await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);
        _harness.UtcNow = _harness.UtcNow.AddHours(2);

        (await SweepHandler().Handle(new CloseExpiredExamAttemptsCommand(), CancellationToken.None)).ShouldBe(1);

        // A second pass finds nothing left to do, whatever the schedule does.
        (await SweepHandler().Handle(new CloseExpiredExamAttemptsCommand(), CancellationToken.None)).ShouldBe(0);
    }

    [Fact]
    public async Task An_expired_sitting_still_marks_the_answers_it_had_and_reaches_the_grade_book()
    {
        var (exam, question) = SeedOnlineExam();
        _harness.SignInAsStudent();

        var session = await StartHandler().Handle(
            new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        // An answer was recorded before the student disappeared.
        var attempt = await _harness.Context.ExamAttempts.FirstAsync();

        attempt.Answers.Add(new ExamAttemptAnswer
        {
            Id = Guid.NewGuid(),
            AttemptId = attempt.Id,
            QuestionId = question.Id,
            SelectedOptionId = question.Options.First(option => option.IsCorrect).Id,
            MaximumPoints = question.Points,
            AnsweredAt = _harness.UtcNow,
            CreatedAt = _harness.UtcNow
        });

        await _harness.Context.SaveChangesAsync();

        _harness.UtcNow = _harness.UtcNow.AddHours(2);
        await SweepHandler().Handle(new CloseExpiredExamAttemptsCommand(), CancellationToken.None);

        var closed = await _harness.Context.ExamAttempts.FirstAsync(entity => entity.Id == session.AttemptId);
        closed.Score.ShouldBe(10m);

        var result = await _harness.Context.ExamResults.FirstAsync();
        result.StudentId.ShouldBe(_harness.Student.Id);
        result.Score.ShouldBe(10m);
    }

    [Fact]
    public async Task A_sitting_submitted_before_the_sweep_runs_is_left_exactly_as_the_student_left_it()
    {
        var (exam, question) = SeedOnlineExam();
        _harness.SignInAsStudent();

        var session = await StartHandler().Handle(
            new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        await SubmitHandler().Handle(
            new SubmitExamAttemptCommand
            {
                AttemptId = session.AttemptId,
                Answers =
                [
                    new ExamAnswerInput
                    {
                        QuestionId = question.Id,
                        SelectedOptionId = question.Options.First(option => option.IsCorrect).Id
                    }
                ]
            },
            CancellationToken.None);

        _harness.UtcNow = _harness.UtcNow.AddHours(2);

        (await SweepHandler().Handle(new CloseExpiredExamAttemptsCommand(), CancellationToken.None))
            .ShouldBe(0);

        var attempt = await _harness.Context.ExamAttempts.FirstAsync();
        attempt.Status.ShouldBe(ExamAttemptStatus.Graded);
    }

    [Fact]
    public async Task Closing_a_sitting_tells_the_student_once()
    {
        var (exam, _) = SeedOnlineExam();
        _harness.SignInAsStudent();

        await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);
        _harness.UtcNow = _harness.UtcNow.AddHours(2);

        await SweepHandler().Handle(new CloseExpiredExamAttemptsCommand(), CancellationToken.None);

        var notifications = await _harness.Context.Notifications
            .Where(notification => notification.UserId == _harness.Student.UserId)
            .ToListAsync();

        notifications.Count.ShouldBe(1);
        notifications[0].Title.ShouldBe("وقت الامتحان خلص");
    }

    // -----------------------------------------------------------------------
    // Certificates
    // -----------------------------------------------------------------------

    private (Course Course, CourseEnrollment Enrollment, CourseLesson Lesson) SeedCourse()
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.Tenant.Id,
            TeacherId = _harness.Teacher.Id,
            SubjectId = _harness.Subject.Id,
            GradeId = _harness.Grade.Id,
            Title = "كورس الفيزياء",
            Slug = "physics",
            IsPublished = true,
            CreatedAt = _harness.UtcNow.AddDays(-30)
        };

        var section = new CourseSection
        {
            Id = Guid.NewGuid(), CourseId = course.Id, Title = "الوحدة الأولى", SortOrder = 1
        };

        var lesson = new CourseLesson
        {
            Id = Guid.NewGuid(),
            SectionId = section.Id,
            Title = "الدرس الأول",
            SortOrder = 1,
            IsPublished = true,
            VideoUrl = "https://videos.ustaz.test/1"
        };

        section.Lessons.Add(lesson);
        course.Sections.Add(section);

        var enrollment = new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = _harness.Student.Id,
            Status = EnrollmentStatus.Active,
            EnrolledAt = _harness.UtcNow.AddDays(-5),
            PricePaid = 300m,
            FinalPrice = 300m
        };

        _harness.Context.Courses.Add(course);
        _harness.Context.CourseEnrollments.Add(enrollment);
        _harness.Context.SaveChanges();

        return (course, enrollment, lesson);
    }

    [Fact]
    public async Task Finishing_a_course_issues_a_certificate_and_tells_the_student()
    {
        var (course, _, lesson) = SeedCourse();
        _harness.SignInAsStudent();

        var handler = new CompleteLessonCommandHandler(
            _harness.Context,
            _harness.CurrentUser.Object,
            _harness.Clock,
            _harness.RealNotifications,
            _harness.Activity,
            new CertificateIssuer(_harness.Context, _harness.Clock, _harness.RealNotifications));

        await handler.Handle(
            new CompleteLessonCommand { CourseId = course.Id, LessonId = lesson.Id },
            CancellationToken.None);

        var certificate = await _harness.Context.Certificates.SingleAsync();
        certificate.StudentId.ShouldBe(_harness.Student.Id);
        certificate.CourseTitle.ShouldBe("كورس الفيزياء");
        certificate.Status.ShouldBe(CertificateStatus.Issued);
        certificate.CertificateNumber.ShouldStartWith("USTAZ-");
        certificate.VerificationCode.Length.ShouldBe(32);
        certificate.LessonsCompleted.ShouldBe(1);

        var notification = await _harness.Context.Notifications
            .FirstAsync(entry => entry.Event == "certificate.issued");

        notification.Title.ShouldBe("مبروك 🎉 الشهادة بتاعتك جاهزة");
    }

    [Fact]
    public async Task Issuing_twice_returns_the_same_certificate()
    {
        var (_, enrollment, _) = SeedCourse();

        var stored = await _harness.Context.CourseEnrollments.FirstAsync();
        stored.Status = EnrollmentStatus.Completed;
        stored.CompletedAt = _harness.UtcNow;
        await _harness.Context.SaveChangesAsync();

        var issuer = new CertificateIssuer(_harness.Context, _harness.Clock, _harness.RealNotifications);

        var first = await issuer.IssueForCompletedCourseAsync(enrollment.Id, CancellationToken.None);
        var second = await issuer.IssueForCompletedCourseAsync(enrollment.Id, CancellationToken.None);

        second.ShouldBe(first);
        (await _harness.Context.Certificates.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task An_unfinished_course_earns_nothing()
    {
        var (_, enrollment, _) = SeedCourse();

        var issuer = new CertificateIssuer(_harness.Context, _harness.Clock, _harness.RealNotifications);

        (await issuer.IssueForCompletedCourseAsync(enrollment.Id, CancellationToken.None)).ShouldBeNull();
        (await _harness.Context.Certificates.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Verification_reports_a_valid_certificate_without_leaking_identifiers()
    {
        var (course, _, lesson) = SeedCourse();
        _harness.SignInAsStudent();

        await new CompleteLessonCommandHandler(
                _harness.Context,
                _harness.CurrentUser.Object,
                _harness.Clock,
                _harness.RealNotifications,
                _harness.Activity,
                new CertificateIssuer(_harness.Context, _harness.Clock, _harness.RealNotifications))
            .Handle(new CompleteLessonCommand { CourseId = course.Id, LessonId = lesson.Id }, CancellationToken.None);

        var certificate = await _harness.Context.Certificates.SingleAsync();

        var verification = await new VerifyCertificateQueryHandler(_harness.Context)
            .Handle(new VerifyCertificateQuery(certificate.VerificationCode), CancellationToken.None);

        verification.IsValid.ShouldBeTrue();
        verification.StudentName.ShouldBe("سلمى إبراهيم");
        verification.CourseTitle.ShouldBe("كورس الفيزياء");

        // The public payload carries names and dates only — no ids to walk.
        var payload = System.Text.Json.JsonSerializer.Serialize(verification);
        payload.ShouldNotContain(certificate.StudentId.ToString());
        payload.ShouldNotContain(certificate.CourseId.ToString());
    }

    [Fact]
    public async Task An_unknown_verification_code_is_simply_not_valid()
    {
        var verification = await new VerifyCertificateQueryHandler(_harness.Context)
            .Handle(new VerifyCertificateQuery("deadbeef"), CancellationToken.None);

        verification.IsValid.ShouldBeFalse();
        verification.StudentName.ShouldBeNull();
    }

    [Fact]
    public async Task A_revoked_certificate_stops_verifying()
    {
        var (course, _, lesson) = SeedCourse();
        _harness.SignInAsStudent();

        await new CompleteLessonCommandHandler(
                _harness.Context,
                _harness.CurrentUser.Object,
                _harness.Clock,
                _harness.RealNotifications,
                _harness.Activity,
                new CertificateIssuer(_harness.Context, _harness.Clock, _harness.RealNotifications))
            .Handle(new CompleteLessonCommand { CourseId = course.Id, LessonId = lesson.Id }, CancellationToken.None);

        var certificate = await _harness.Context.Certificates.SingleAsync();

        _harness.SignInAsTeacher();

        await new RevokeCertificateCommandHandler(
                _harness.Context, _harness.TenantAuthorization, _harness.Clock, _harness.Audit.Object)
            .Handle(new RevokeCertificateCommand(certificate.Id, "صدرت بالخطأ"), CancellationToken.None);

        var verification = await new VerifyCertificateQueryHandler(_harness.Context)
            .Handle(new VerifyCertificateQuery(certificate.VerificationCode), CancellationToken.None);

        verification.IsValid.ShouldBeFalse();
        verification.Status.ShouldBe("Revoked");
    }

    [Fact]
    public async Task A_teacher_from_another_workspace_cannot_revoke_a_certificate()
    {
        var (course, _, lesson) = SeedCourse();
        _harness.SignInAsStudent();

        await new CompleteLessonCommandHandler(
                _harness.Context,
                _harness.CurrentUser.Object,
                _harness.Clock,
                _harness.RealNotifications,
                _harness.Activity,
                new CertificateIssuer(_harness.Context, _harness.Clock, _harness.RealNotifications))
            .Handle(new CompleteLessonCommand { CourseId = course.Id, LessonId = lesson.Id }, CancellationToken.None);

        var certificate = await _harness.Context.Certificates.SingleAsync();

        _harness.SignInAsTeacher(_harness.OtherTeacher);

        await Should.ThrowAsync<NotFoundException>(() =>
            new RevokeCertificateCommandHandler(
                    _harness.Context, _harness.TenantAuthorization, _harness.Clock, _harness.Audit.Object)
                .Handle(new RevokeCertificateCommand(certificate.Id, "محاولة"), CancellationToken.None));
    }

    public void Dispose() => _harness.Dispose();
}
