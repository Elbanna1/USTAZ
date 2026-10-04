using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using Ustaz.Application.Features.Exams;
using Ustaz.Application.Features.Exams.Dtos;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Tests.Handlers;

/// <summary>
/// Covers the online exam lifecycle: authoring the paper, sitting it, automatic marking,
/// manual marking, and — most importantly — that the answer key never reaches a student
/// before their marks are released.
/// </summary>
public class OnlineExamTests : IDisposable
{
    private readonly TestHarness _harness = new();
    private readonly Mock<IMediator> _mediator = new();

    public OnlineExamTests()
    {
        // The submit and grade handlers re-read the attempt through MediatR; point that at
        // the real query handler so the tests assert on the shape clients actually receive.
        _mediator
            .Setup(mediator => mediator.Send(It.IsAny<GetExamAttemptQuery>(), It.IsAny<CancellationToken>()))
            .Returns((GetExamAttemptQuery query, CancellationToken token) =>
                AttemptQueryHandler().Handle(query, token));
    }

    // -----------------------------------------------------------------------
    // Handlers under test
    // -----------------------------------------------------------------------
    private AddExamQuestionCommandHandler AddQuestionHandler() =>
        new(_harness.Context, _harness.TenantAuthorization, _harness.PlanLimits, _harness.Clock);

    private UpdateExamQuestionCommandHandler UpdateQuestionHandler() =>
        new(_harness.Context, _harness.TenantAuthorization, _harness.Clock);

    private StartExamAttemptCommandHandler StartHandler() =>
        new(_harness.Context, _harness.CurrentUser.Object, _harness.Clock);

    private SubmitExamAttemptCommandHandler SubmitHandler() =>
        new(_harness.Context,
            _harness.CurrentUser.Object,
            _harness.Clock,
            _harness.Notifications.Object,
            _harness.Activity,
            _mediator.Object);

    private GradeExamAttemptCommandHandler GradeHandler() =>
        new(_harness.Context,
            _harness.TenantAuthorization,
            _harness.Clock,
            _harness.Audit.Object,
            _mediator.Object);

    private GetExamAttemptQueryHandler AttemptQueryHandler() =>
        new(_harness.Context, _harness.CurrentUser.Object, _harness.TenantAuthorization);

    // -----------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------

    /// <summary>An online exam on the seeded group, with the seeded student enrolled.</summary>
    private Exam SeedOnlineExam(bool published = true, int maxAttempts = 1, bool showResultsImmediately = false)
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
            DurationInMinutes = 30,
            MaximumScore = 10m,
            PassingScore = 5m,
            IsOnline = true,
            MaxAttempts = maxAttempts,
            ShowResultsImmediately = showResultsImmediately,
            IsPublished = published,
            PublishedAt = published ? _harness.UtcNow.AddDays(-1) : null,
            CreatedAt = _harness.UtcNow.AddDays(-2)
        };

        _harness.Context.Exams.Add(exam);
        _harness.Context.SaveChanges();

        return exam;
    }

    /// <summary>A two-option multiple choice question worth the given points.</summary>
    private ExamQuestion SeedMultipleChoice(Exam exam, decimal points = 6m, int sortOrder = 1)
    {
        var question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            TenantId = exam.TenantId,
            ExamId = exam.Id,
            Text = "ما وحدة قياس القوة؟",
            Type = ExamQuestionType.MultipleChoice,
            Points = points,
            SortOrder = sortOrder,
            Explanation = "النيوتن هي وحدة القوة.",
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

        _harness.Context.ExamQuestions.Add(question);
        _harness.Context.SaveChanges();

        return question;
    }

    private ExamQuestion SeedEssay(Exam exam, decimal points = 4m, int sortOrder = 2)
    {
        var question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            TenantId = exam.TenantId,
            ExamId = exam.Id,
            Text = "اشرح قانون نيوتن الأول.",
            Type = ExamQuestionType.Essay,
            Points = points,
            SortOrder = sortOrder,
            CreatedAt = _harness.UtcNow
        };

        _harness.Context.ExamQuestions.Add(question);
        _harness.Context.SaveChanges();

        return question;
    }

    // -----------------------------------------------------------------------
    // Authoring the paper
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Adding_a_question_keeps_the_exam_total_in_step_with_its_points()
    {
        var exam = SeedOnlineExam(published: false);
        _harness.SignInAsTeacher();

        await AddQuestionHandler().Handle(
            new AddExamQuestionCommand
            {
                ExamId = exam.Id,
                Text = "ما وحدة قياس القوة؟",
                Type = ExamQuestionType.MultipleChoice,
                Points = 7m,
                Options =
                [
                    new ExamQuestionOptionInput { Text = "نيوتن", IsCorrect = true },
                    new ExamQuestionOptionInput { Text = "جول", IsCorrect = false }
                ]
            },
            CancellationToken.None);

        var stored = await _harness.Context.Exams.FirstAsync(entity => entity.Id == exam.Id);
        stored.MaximumScore.ShouldBe(7m);
        stored.PassingScore.ShouldBe(5m);
    }

    [Fact]
    public async Task A_multiple_choice_question_without_a_correct_option_is_rejected()
    {
        var exam = SeedOnlineExam(published: false);
        _harness.SignInAsTeacher();

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            AddQuestionHandler().Handle(
                new AddExamQuestionCommand
                {
                    ExamId = exam.Id,
                    Text = "سؤال",
                    Type = ExamQuestionType.MultipleChoice,
                    Options =
                    [
                        new ExamQuestionOptionInput { Text = "أ", IsCorrect = false },
                        new ExamQuestionOptionInput { Text = "ب", IsCorrect = false }
                    ]
                },
                CancellationToken.None));

        exception.Code.ShouldBe("QUESTION_NEEDS_ONE_CORRECT_OPTION");
    }

    [Fact]
    public async Task A_short_answer_question_without_a_model_answer_is_rejected()
    {
        var exam = SeedOnlineExam(published: false);
        _harness.SignInAsTeacher();

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            AddQuestionHandler().Handle(
                new AddExamQuestionCommand
                {
                    ExamId = exam.Id,
                    Text = "أكمل: وحدة القوة هي ...",
                    Type = ExamQuestionType.ShortAnswer
                },
                CancellationToken.None));

        exception.Code.ShouldBe("QUESTION_NEEDS_MODEL_ANSWER");
    }

    [Fact]
    public async Task Questions_cannot_be_edited_once_a_student_has_started_the_exam()
    {
        var exam = SeedOnlineExam();
        var question = SeedMultipleChoice(exam);

        _harness.SignInAsStudent();
        await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        _harness.SignInAsTeacher();

        await Should.ThrowAsync<ConflictException>(() =>
            UpdateQuestionHandler().Handle(
                new UpdateExamQuestionCommand { ExamId = exam.Id, QuestionId = question.Id, Points = 9m },
                CancellationToken.None));
    }

    [Fact]
    public async Task A_teacher_from_another_workspace_cannot_add_questions()
    {
        var exam = SeedOnlineExam(published: false);
        _harness.SignInAsTeacher(_harness.OtherTeacher);

        // Cross-tenant reads answer 404 by design, so the other workspace's exam is never
        // confirmed to exist.
        await Should.ThrowAsync<NotFoundException>(() =>
            AddQuestionHandler().Handle(
                new AddExamQuestionCommand
                {
                    ExamId = exam.Id,
                    Text = "سؤال",
                    Type = ExamQuestionType.Essay
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task Publishing_an_online_exam_without_questions_is_rejected()
    {
        var exam = SeedOnlineExam(published: false);

        var exception = Should.Throw<BusinessRuleException>(() => exam.Publish(_harness.UtcNow));

        exception.Code.ShouldBe("EXAM_HAS_NO_QUESTIONS");
    }

    // -----------------------------------------------------------------------
    // Sitting the exam
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Starting_an_exam_returns_the_paper_without_any_answer_key()
    {
        var exam = SeedOnlineExam();
        SeedMultipleChoice(exam);
        _harness.SignInAsStudent();

        var session = await StartHandler().Handle(
            new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        session.Questions.Count.ShouldBe(1);
        session.Questions[0].Options.Count.ShouldBe(2);

        // The student-facing option shape has no IsCorrect member at all; assert that the
        // serialised payload carries nothing that identifies the right answer.
        var payload = System.Text.Json.JsonSerializer.Serialize(session);
        payload.ShouldNotContain("isCorrect", Case.Insensitive);
        payload.ShouldNotContain("correctAnswer", Case.Insensitive);
        payload.ShouldNotContain("explanation", Case.Insensitive);
    }

    [Fact]
    public async Task A_student_who_is_not_enrolled_cannot_start_the_exam()
    {
        var exam = SeedOnlineExam();
        SeedMultipleChoice(exam);

        var enrollment = await _harness.Context.GroupEnrollments.FirstAsync();
        _harness.Context.GroupEnrollments.Remove(enrollment);
        await _harness.Context.SaveChangesAsync();

        _harness.SignInAsStudent();

        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None));
    }

    [Fact]
    public async Task An_unpublished_exam_is_invisible_to_students()
    {
        var exam = SeedOnlineExam(published: false);
        SeedMultipleChoice(exam);
        _harness.SignInAsStudent();

        await Should.ThrowAsync<NotFoundException>(() =>
            StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Starting_twice_resumes_the_same_sitting_rather_than_opening_another()
    {
        var exam = SeedOnlineExam();
        SeedMultipleChoice(exam);
        _harness.SignInAsStudent();

        var first = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);
        var second = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        second.AttemptId.ShouldBe(first.AttemptId);
        (await _harness.Context.ExamAttempts.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task A_second_attempt_is_refused_when_the_exam_allows_only_one()
    {
        var exam = SeedOnlineExam(maxAttempts: 1);
        var question = SeedMultipleChoice(exam);
        _harness.SignInAsStudent();

        var session = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        await SubmitHandler().Handle(
            new SubmitExamAttemptCommand
            {
                AttemptId = session.AttemptId,
                Answers = [new ExamAnswerInput { QuestionId = question.Id }]
            },
            CancellationToken.None);

        await Should.ThrowAsync<ConflictException>(() =>
            StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None));
    }

    [Fact]
    public async Task A_retake_is_allowed_while_attempts_remain()
    {
        var exam = SeedOnlineExam(maxAttempts: 2);
        var question = SeedMultipleChoice(exam);
        _harness.SignInAsStudent();

        var first = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        await SubmitHandler().Handle(
            new SubmitExamAttemptCommand
            {
                AttemptId = first.AttemptId,
                Answers = [new ExamAnswerInput { QuestionId = question.Id }]
            },
            CancellationToken.None);

        var second = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        second.AttemptNumber.ShouldBe(2);
    }

    [Fact]
    public async Task An_exam_that_has_been_sat_cannot_be_deleted()
    {
        var exam = SeedOnlineExam();
        SeedMultipleChoice(exam);

        _harness.SignInAsStudent();
        await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        _harness.SignInAsTeacher();

        var handler = new DeleteExamCommandHandler(_harness.Context, _harness.TenantAuthorization);

        await Should.ThrowAsync<ConflictException>(() =>
            handler.Handle(new DeleteExamCommand(exam.Id), CancellationToken.None));
    }

    // -----------------------------------------------------------------------
    // Marking
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_correct_multiple_choice_answer_is_graded_automatically_and_lands_in_the_grade_book()
    {
        var exam = SeedOnlineExam(showResultsImmediately: true);
        var question = SeedMultipleChoice(exam, points: 10m);
        var correctOption = question.Options.First(option => option.IsCorrect);

        _harness.SignInAsStudent();
        var session = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        var result = await SubmitHandler().Handle(
            new SubmitExamAttemptCommand
            {
                AttemptId = session.AttemptId,
                Answers = [new ExamAnswerInput { QuestionId = question.Id, SelectedOptionId = correctOption.Id }]
            },
            CancellationToken.None);

        result.Status.ShouldBe(ExamAttemptStatus.Graded);
        result.Score.ShouldBe(10m);
        result.Percentage.ShouldBe(100m);
        result.IsPassed.ShouldBeTrue();

        var examResult = await _harness.Context.ExamResults.FirstAsync();
        examResult.StudentId.ShouldBe(_harness.Student.Id);
        examResult.Score.ShouldBe(10m);
        examResult.IsPassed.ShouldBeTrue();
    }

    [Fact]
    public async Task A_wrong_answer_scores_nothing()
    {
        var exam = SeedOnlineExam(showResultsImmediately: true);
        var question = SeedMultipleChoice(exam, points: 10m);
        var wrongOption = question.Options.First(option => !option.IsCorrect);

        _harness.SignInAsStudent();
        var session = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        var result = await SubmitHandler().Handle(
            new SubmitExamAttemptCommand
            {
                AttemptId = session.AttemptId,
                Answers = [new ExamAnswerInput { QuestionId = question.Id, SelectedOptionId = wrongOption.Id }]
            },
            CancellationToken.None);

        result.Score.ShouldBe(0m);
        result.IsPassed.ShouldBeFalse();
    }

    [Fact]
    public async Task A_short_answer_is_matched_case_and_whitespace_insensitively()
    {
        var exam = SeedOnlineExam(showResultsImmediately: true);

        var question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            TenantId = exam.TenantId,
            ExamId = exam.Id,
            Text = "What is the unit of force?",
            Type = ExamQuestionType.ShortAnswer,
            Points = 10m,
            CorrectAnswer = "Newton",
            CreatedAt = _harness.UtcNow
        };

        _harness.Context.ExamQuestions.Add(question);
        await _harness.Context.SaveChangesAsync();

        _harness.SignInAsStudent();
        var session = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        var result = await SubmitHandler().Handle(
            new SubmitExamAttemptCommand
            {
                AttemptId = session.AttemptId,
                Answers = [new ExamAnswerInput { QuestionId = question.Id, TextAnswer = "  newton " }]
            },
            CancellationToken.None);

        result.Score.ShouldBe(10m);
    }

    [Fact]
    public async Task An_option_belonging_to_another_question_is_rejected()
    {
        var exam = SeedOnlineExam();
        var first = SeedMultipleChoice(exam, points: 5m, sortOrder: 1);
        var second = SeedMultipleChoice(exam, points: 5m, sortOrder: 2);

        _harness.SignInAsStudent();
        var session = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            SubmitHandler().Handle(
                new SubmitExamAttemptCommand
                {
                    AttemptId = session.AttemptId,
                    Answers =
                    [
                        new ExamAnswerInput
                        {
                            QuestionId = first.Id,
                            SelectedOptionId = second.Options.First().Id
                        }
                    ]
                },
                CancellationToken.None));

        exception.Code.ShouldBe("ANSWER_OPTION_MISMATCH");
    }

    [Fact]
    public async Task Submitting_the_same_attempt_twice_is_a_conflict()
    {
        var exam = SeedOnlineExam();
        var question = SeedMultipleChoice(exam);

        _harness.SignInAsStudent();
        var session = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        var command = new SubmitExamAttemptCommand
        {
            AttemptId = session.AttemptId,
            Answers = [new ExamAnswerInput { QuestionId = question.Id }]
        };

        await SubmitHandler().Handle(command, CancellationToken.None);

        await Should.ThrowAsync<ConflictException>(() =>
            SubmitHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Another_students_attempt_cannot_be_submitted()
    {
        var exam = SeedOnlineExam();
        var question = SeedMultipleChoice(exam);

        _harness.SignInAsStudent();
        var session = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        // A different learner, with the attempt id of the first.
        var intruder = new StudentProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        _harness.Context.StudentProfiles.Add(intruder);
        await _harness.Context.SaveChangesAsync();

        _harness.CurrentUser.Setup(user => user.RequireStudentId()).Returns(intruder.Id);

        await Should.ThrowAsync<NotFoundException>(() =>
            SubmitHandler().Handle(
                new SubmitExamAttemptCommand
                {
                    AttemptId = session.AttemptId,
                    Answers = [new ExamAnswerInput { QuestionId = question.Id }]
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task An_essay_holds_the_attempt_open_for_the_teacher()
    {
        var exam = SeedOnlineExam(showResultsImmediately: true);
        var choice = SeedMultipleChoice(exam, points: 6m);
        var essay = SeedEssay(exam, points: 4m);

        _harness.SignInAsStudent();
        var session = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        var submitted = await SubmitHandler().Handle(
            new SubmitExamAttemptCommand
            {
                AttemptId = session.AttemptId,
                Answers =
                [
                    new ExamAnswerInput
                    {
                        QuestionId = choice.Id,
                        SelectedOptionId = choice.Options.First(option => option.IsCorrect).Id
                    },
                    new ExamAnswerInput { QuestionId = essay.Id, TextAnswer = "الجسم يبقى ساكنًا..." }
                ]
            },
            CancellationToken.None);

        submitted.Status.ShouldBe(ExamAttemptStatus.Submitted);
        submitted.RequiresManualGrading.ShouldBeTrue();
        submitted.Score.ShouldBe(6m);

        // The teacher awards the essay marks.
        _harness.SignInAsTeacher();

        var essayAnswer = submitted.Answers.First(answer => answer.QuestionId == essay.Id);

        var graded = await GradeHandler().Handle(
            new GradeExamAttemptCommand
            {
                AttemptId = session.AttemptId,
                Answers = [new GradeAnswerInput { AnswerId = essayAnswer.Id, AwardedPoints = 4m, Feedback = "ممتاز" }],
                TeacherComment = "أحسنت"
            },
            CancellationToken.None);

        graded.Status.ShouldBe(ExamAttemptStatus.Graded);
        graded.Score.ShouldBe(10m);
        graded.RequiresManualGrading.ShouldBeFalse();

        var examResult = await _harness.Context.ExamResults.FirstAsync();
        examResult.Score.ShouldBe(10m);
    }

    [Fact]
    public async Task An_answer_cannot_be_awarded_more_than_the_question_is_worth()
    {
        var exam = SeedOnlineExam();
        var essay = SeedEssay(exam, points: 4m);

        _harness.SignInAsStudent();
        var session = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        var submitted = await SubmitHandler().Handle(
            new SubmitExamAttemptCommand
            {
                AttemptId = session.AttemptId,
                Answers = [new ExamAnswerInput { QuestionId = essay.Id, TextAnswer = "إجابة" }]
            },
            CancellationToken.None);

        _harness.SignInAsTeacher();

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            GradeHandler().Handle(
                new GradeExamAttemptCommand
                {
                    AttemptId = session.AttemptId,
                    Answers =
                    [
                        new GradeAnswerInput
                        {
                            AnswerId = submitted.Answers.First().Id,
                            AwardedPoints = 99m
                        }
                    ]
                },
                CancellationToken.None));

        exception.Code.ShouldBe("ANSWER_SCORE_OUT_OF_RANGE");
    }

    [Fact]
    public async Task A_teacher_from_another_workspace_cannot_grade_an_attempt()
    {
        var exam = SeedOnlineExam();
        var essay = SeedEssay(exam, points: 4m);

        _harness.SignInAsStudent();
        var session = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        var submitted = await SubmitHandler().Handle(
            new SubmitExamAttemptCommand
            {
                AttemptId = session.AttemptId,
                Answers = [new ExamAnswerInput { QuestionId = essay.Id, TextAnswer = "إجابة" }]
            },
            CancellationToken.None);

        _harness.SignInAsTeacher(_harness.OtherTeacher);

        await Should.ThrowAsync<NotFoundException>(() =>
            GradeHandler().Handle(
                new GradeExamAttemptCommand
                {
                    AttemptId = session.AttemptId,
                    Answers =
                    [
                        new GradeAnswerInput { AnswerId = submitted.Answers.First().Id, AwardedPoints = 4m }
                    ]
                },
                CancellationToken.None));
    }

    // -----------------------------------------------------------------------
    // Visibility of marks
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_student_cannot_see_their_marks_or_the_key_before_results_are_released()
    {
        var exam = SeedOnlineExam(showResultsImmediately: false);
        var question = SeedMultipleChoice(exam, points: 10m);

        _harness.SignInAsStudent();
        var session = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

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

        var seenByStudent = await AttemptQueryHandler().Handle(
            new GetExamAttemptQuery(session.AttemptId), CancellationToken.None);

        seenByStudent.ScoresVisible.ShouldBeFalse();
        seenByStudent.Score.ShouldBe(0m);
        seenByStudent.Percentage.ShouldBe(0m);
        seenByStudent.IsPassed.ShouldBeFalse();
        seenByStudent.Answers.ShouldAllBe(answer => answer.CorrectAnswer == null);
        seenByStudent.Answers.ShouldAllBe(answer => answer.IsCorrect == null);
        seenByStudent.Answers.ShouldAllBe(answer => answer.Explanation == null);

        // The mark itself was recorded: it is only withheld from the student.
        var attempt = await _harness.Context.ExamAttempts.FirstAsync();
        attempt.Score.ShouldBe(10m);
    }

    [Fact]
    public async Task Releasing_results_reveals_the_marks_and_the_key_to_the_student()
    {
        var exam = SeedOnlineExam(showResultsImmediately: false);
        var question = SeedMultipleChoice(exam, points: 10m);

        _harness.SignInAsStudent();
        var session = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

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

        var stored = await _harness.Context.Exams.FirstAsync(entity => entity.Id == exam.Id);
        stored.PublishResults(_harness.UtcNow);
        await _harness.Context.SaveChangesAsync();

        var seenByStudent = await AttemptQueryHandler().Handle(
            new GetExamAttemptQuery(session.AttemptId), CancellationToken.None);

        seenByStudent.ScoresVisible.ShouldBeTrue();
        seenByStudent.Score.ShouldBe(10m);
        seenByStudent.Answers.First().CorrectAnswer.ShouldBe("نيوتن");
    }

    [Fact]
    public async Task A_teacher_from_another_workspace_cannot_read_an_attempt()
    {
        var exam = SeedOnlineExam();
        SeedMultipleChoice(exam);

        _harness.SignInAsStudent();
        var session = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        _harness.SignInAsTeacher(_harness.OtherTeacher);

        await Should.ThrowAsync<NotFoundException>(() =>
            AttemptQueryHandler().Handle(new GetExamAttemptQuery(session.AttemptId), CancellationToken.None));
    }

    // -----------------------------------------------------------------------
    // The clock
    // -----------------------------------------------------------------------

    [Fact]
    public async Task An_exam_that_has_not_opened_yet_cannot_be_started()
    {
        var exam = SeedOnlineExam();
        SeedMultipleChoice(exam);

        var stored = await _harness.Context.Exams.FirstAsync(entity => entity.Id == exam.Id);
        stored.ExamDate = _harness.UtcNow.AddDays(1);
        await _harness.Context.SaveChangesAsync();

        _harness.SignInAsStudent();

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None));

        exception.Code.ShouldBe("EXAM_NOT_OPEN");
    }

    [Fact]
    public async Task Submitting_after_the_window_closes_still_marks_the_work_but_flags_it_as_expired()
    {
        var exam = SeedOnlineExam();
        var question = SeedMultipleChoice(exam, points: 10m);

        _harness.SignInAsStudent();
        var session = await StartHandler().Handle(new StartExamAttemptCommand(exam.Id), CancellationToken.None);

        // The duration is 30 minutes; come back an hour later.
        _harness.UtcNow = _harness.UtcNow.AddHours(1);

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

        var attempt = await _harness.Context.ExamAttempts.FirstAsync();
        attempt.Status.ShouldBe(ExamAttemptStatus.Expired);
        attempt.Score.ShouldBe(10m);
    }

    public void Dispose() => _harness.Dispose();
}
