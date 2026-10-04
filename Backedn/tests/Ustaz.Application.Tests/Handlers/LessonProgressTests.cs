using Microsoft.EntityFrameworkCore;
using Shouldly;
using Ustaz.Application.Features.Enrollments.Commands;
using Ustaz.Application.Features.Enrollments.Queries;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Tests.Handlers;

/// <summary>
/// Course progress is derived from the lessons a student actually completed, so these tests
/// pin the arithmetic, the idempotency of a replayed completion, and the access rules.
/// </summary>
public class LessonProgressTests : IDisposable
{
    private readonly TestHarness _harness = new();

    private Course _course = null!;
    private CourseEnrollment _enrollment = null!;
    private readonly List<CourseLesson> _lessons = [];

    public LessonProgressTests() => SeedCourse();

    private CompleteLessonCommandHandler CompleteHandler() =>
        new(_harness.Context,
            _harness.CurrentUser.Object,
            _harness.Clock,
            _harness.Notifications.Object,
            _harness.Activity,
            _harness.CertificateIssuer);

    private ReopenLessonCommandHandler ReopenHandler() =>
        new(_harness.Context, _harness.CurrentUser.Object, _harness.Clock);

    private GetCourseProgressReportQueryHandler ReportHandler() =>
        new(_harness.Context, _harness.CurrentUser.Object);

    /// <summary>A published course with four published lessons and one unpublished draft.</summary>
    private void SeedCourse()
    {
        _course = new Course
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.Tenant.Id,
            TeacherId = _harness.Teacher.Id,
            SubjectId = _harness.Subject.Id,
            GradeId = _harness.Grade.Id,
            Title = "كورس الفيزياء المسجل",
            Slug = "physics-course",
            Price = 300m,
            IsPublished = true,
            CreatedAt = _harness.UtcNow.AddDays(-30)
        };

        var section = new CourseSection
        {
            Id = Guid.NewGuid(),
            CourseId = _course.Id,
            Title = "الوحدة الأولى",
            SortOrder = 1
        };

        for (var index = 1; index <= 4; index++)
        {
            var lesson = new CourseLesson
            {
                Id = Guid.NewGuid(),
                SectionId = section.Id,
                Title = "الدرس " + index,
                SortOrder = index,
                DurationInMinutes = 10,
                VideoUrl = "https://videos.ustaz.test/lesson-" + index,
                IsPublished = true
            };

            section.Lessons.Add(lesson);
            _lessons.Add(lesson);
        }

        // A draft lesson must not count towards the denominator.
        section.Lessons.Add(new CourseLesson
        {
            Id = Guid.NewGuid(),
            SectionId = section.Id,
            Title = "مسودة",
            SortOrder = 5,
            IsPublished = false
        });

        _course.Sections.Add(section);

        _enrollment = new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            CourseId = _course.Id,
            StudentId = _harness.Student.Id,
            Status = EnrollmentStatus.Active,
            EnrolledAt = _harness.UtcNow.AddDays(-5),
            PricePaid = 300m,
            FinalPrice = 300m
        };

        _harness.Context.Courses.Add(_course);
        _harness.Context.CourseEnrollments.Add(_enrollment);
        _harness.Context.SaveChanges();
    }

    [Fact]
    public async Task Completing_one_of_four_published_lessons_puts_the_course_at_25_percent()
    {
        _harness.SignInAsStudent();

        var summary = await CompleteHandler().Handle(
            new CompleteLessonCommand { CourseId = _course.Id, LessonId = _lessons[0].Id },
            CancellationToken.None);

        summary.TotalLessons.ShouldBe(4);
        summary.CompletedLessons.ShouldBe(1);
        summary.ProgressPercentage.ShouldBe(25);
        summary.Status.ShouldBe(EnrollmentStatus.Active);

        var stored = await _harness.Context.CourseEnrollments.FirstAsync(entity => entity.Id == _enrollment.Id);
        stored.ProgressPercentage.ShouldBe(25);
    }

    [Fact]
    public async Task Completing_the_same_lesson_twice_does_not_double_count_it()
    {
        _harness.SignInAsStudent();
        var handler = CompleteHandler();
        var command = new CompleteLessonCommand { CourseId = _course.Id, LessonId = _lessons[0].Id };

        await handler.Handle(command, CancellationToken.None);
        var summary = await handler.Handle(command, CancellationToken.None);

        summary.CompletedLessons.ShouldBe(1);
        summary.ProgressPercentage.ShouldBe(25);
        (await _harness.Context.CourseLessonProgress.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Completing_every_lesson_completes_the_course()
    {
        _harness.SignInAsStudent();
        var handler = CompleteHandler();

        LessonProgressSummary? summary = null;

        foreach (var lesson in _lessons)
        {
            var result = await handler.Handle(
                new CompleteLessonCommand { CourseId = _course.Id, LessonId = lesson.Id },
                CancellationToken.None);

            summary = new LessonProgressSummary(result.ProgressPercentage, result.Status, result.CompletedAt);
        }

        summary!.Percentage.ShouldBe(100);
        summary.Status.ShouldBe(EnrollmentStatus.Completed);
        summary.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Reopening_a_lesson_walks_the_course_back_out_of_completion()
    {
        _harness.SignInAsStudent();
        var handler = CompleteHandler();

        foreach (var lesson in _lessons)
        {
            await handler.Handle(
                new CompleteLessonCommand { CourseId = _course.Id, LessonId = lesson.Id },
                CancellationToken.None);
        }

        var summary = await ReopenHandler().Handle(
            new ReopenLessonCommand(_course.Id, _lessons[0].Id), CancellationToken.None);

        summary.ProgressPercentage.ShouldBe(75);
        summary.Status.ShouldBe(EnrollmentStatus.Active);
        summary.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task A_lesson_from_another_course_cannot_be_completed_through_this_enrollment()
    {
        var otherCourse = new Course
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.OtherTenant.Id,
            TeacherId = _harness.OtherTeacher.Id,
            SubjectId = _harness.Subject.Id,
            GradeId = _harness.Grade.Id,
            Title = "كورس آخر",
            Slug = "other-course",
            IsPublished = true
        };

        var otherSection = new CourseSection
        {
            Id = Guid.NewGuid(), CourseId = otherCourse.Id, Title = "وحدة", SortOrder = 1
        };

        var otherLesson = new CourseLesson
        {
            Id = Guid.NewGuid(),
            SectionId = otherSection.Id,
            Title = "درس آخر",
            SortOrder = 1,
            IsPublished = true
        };

        otherSection.Lessons.Add(otherLesson);
        otherCourse.Sections.Add(otherSection);
        _harness.Context.Courses.Add(otherCourse);
        await _harness.Context.SaveChangesAsync();

        _harness.SignInAsStudent();

        await Should.ThrowAsync<NotFoundException>(() =>
            CompleteHandler().Handle(
                new CompleteLessonCommand { CourseId = _course.Id, LessonId = otherLesson.Id },
                CancellationToken.None));
    }

    [Fact]
    public async Task An_unpublished_lesson_cannot_be_completed()
    {
        _harness.SignInAsStudent();

        var draft = await _harness.Context.CourseLessons.FirstAsync(lesson => !lesson.IsPublished);

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            CompleteHandler().Handle(
                new CompleteLessonCommand { CourseId = _course.Id, LessonId = draft.Id },
                CancellationToken.None));

        exception.Code.ShouldBe("LESSON_NOT_PUBLISHED");
    }

    [Fact]
    public async Task A_student_without_an_enrollment_gets_nothing()
    {
        var stranger = new StudentProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        _harness.Context.StudentProfiles.Add(stranger);
        await _harness.Context.SaveChangesAsync();

        _harness.SignInAsStudent();
        _harness.CurrentUser.Setup(user => user.RequireStudentId()).Returns(stranger.Id);

        await Should.ThrowAsync<NotFoundException>(() =>
            CompleteHandler().Handle(
                new CompleteLessonCommand { CourseId = _course.Id, LessonId = _lessons[0].Id },
                CancellationToken.None));
    }

    [Fact]
    public async Task A_cancelled_enrollment_loses_access_to_progress()
    {
        var enrollment = await _harness.Context.CourseEnrollments.FirstAsync();
        enrollment.Cancel(_harness.UtcNow, "refunded");
        await _harness.Context.SaveChangesAsync();

        _harness.SignInAsStudent();

        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            CompleteHandler().Handle(
                new CompleteLessonCommand { CourseId = _course.Id, LessonId = _lessons[0].Id },
                CancellationToken.None));

        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            ReportHandler().Handle(new GetCourseProgressReportQuery(_course.Id), CancellationToken.None));
    }

    [Fact]
    public async Task The_progress_report_lists_published_lessons_with_their_completion_state()
    {
        _harness.SignInAsStudent();

        await CompleteHandler().Handle(
            new CompleteLessonCommand { CourseId = _course.Id, LessonId = _lessons[1].Id, WatchedSeconds = 420 },
            CancellationToken.None);

        var report = await ReportHandler().Handle(
            new GetCourseProgressReportQuery(_course.Id), CancellationToken.None);

        report.TotalLessons.ShouldBe(4);
        report.CompletedLessons.ShouldBe(1);
        report.ProgressPercentage.ShouldBe(25);
        report.Lessons.Count.ShouldBe(4);

        var completed = report.Lessons.First(lesson => lesson.LessonId == _lessons[1].Id);
        completed.IsCompleted.ShouldBeTrue();
        completed.WatchedSeconds.ShouldBe(420);
        completed.CompletedAt.ShouldNotBeNull();

        report.Lessons.First(lesson => lesson.LessonId == _lessons[0].Id).IsCompleted.ShouldBeFalse();
    }

    public void Dispose() => _harness.Dispose();

    private sealed record LessonProgressSummary(int Percentage, EnrollmentStatus Status, DateTime? CompletedAt);
}
