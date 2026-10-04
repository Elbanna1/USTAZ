using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    /// <summary>
    /// Assessment: online exams a student sits in the app, and offline ones where the teacher
    /// records the marks afterwards.
    ///
    /// All four question types appear, which matters because each is marked differently and
    /// an essay is the only one that forces a teacher into the loop. The attempt set covers
    /// every value of <see cref="ExamAttemptStatus"/> — graded, submitted-awaiting-marking,
    /// expired, and one still in progress — so the attempt screen and the expiry sweep both
    /// have real subjects.
    ///
    /// Answer keys live only in <see cref="ExamQuestion.CorrectAnswer"/> and
    /// <see cref="ExamQuestionOption.IsCorrect"/>, exactly where the application expects them;
    /// nothing is duplicated into a student-visible column.
    /// </summary>
    private async Task SeedExamsAsync(CancellationToken cancellationToken)
    {
        await SeedOnlineExamAsync(cancellationToken);
        await SeedOfflineExamAsync(cancellationToken);
        await SeedDraftExamAsync(cancellationToken);
    }

    private sealed record QuestionSpec(
        string Key,
        string Text,
        ExamQuestionType Type,
        decimal Points,
        string? CorrectAnswer,
        (string Text, bool IsCorrect)[] Options,
        string? Explanation);

    private static readonly QuestionSpec[] PhysicsQuestions =
    [
        new("q1", "وحدة قياس القوة في النظام الدولي هي:", ExamQuestionType.MultipleChoice, 2m, null,
            [("نيوتن", true), ("جول", false), ("واط", false), ("باسكال", false)],
            "القوة = كتلة × عجلة، ووحدتها كجم.م/ث² وتسمى النيوتن."),

        new("q2", "الشغل كمية قياسية وليست متجهة.", ExamQuestionType.TrueFalse, 1m, "true",
            [],
            "الشغل حاصل الضرب القياسي للقوة في الإزاحة، فهو كمية قياسية."),

        new("q3", "اكتب اسم القانون الذي ينص على أن الطاقة لا تفنى ولا تستحدث.", ExamQuestionType.ShortAnswer, 2m,
            "قانون بقاء الطاقة", [],
            "قانون بقاء الطاقة أحد أهم قوانين الفيزياء."),

        new("q4", "اشرح الفرق بين الموجات الطولية والموجات المستعرضة مع مثال لكل منهما.",
            ExamQuestionType.Essay, 5m, null, [],
            null),

        new("q5", "أي الآتي يمثل وحدة القدرة؟", ExamQuestionType.MultipleChoice, 2m, null,
            [("واط", true), ("نيوتن", false), ("كولوم", false), ("أوم", false)],
            "القدرة = الشغل / الزمن، ووحدتها جول/ثانية وتسمى الواط.")
    ];

    private async Task SeedOnlineExamAsync(CancellationToken cancellationToken)
    {
        var group = _groups["physics-3sec-intensive"];
        var teacher = _teachers["ahmed"];
        var examId = SeedId.For("demo:exam", "physics-online-midterm");

        var exam = new Exam
        {
            Id = examId,
            TenantId = group.TenantId,
            TeacherId = teacher.Id,
            GroupId = group.Id,
            SubjectId = PhysicsId,
            GradeId = ThirdSecondaryId,
            Title = "امتحان منتصف الترم - الفيزياء",
            Description = "امتحان إلكتروني يغطي الوحدتين الأولى والثانية.",
            Type = ExamType.Midterm,
            ExamDate = _utcNow.AddDays(-10),
            DurationInMinutes = 45,
            // Set from the question points by SyncMaximumScoreFromQuestions below.
            MaximumScore = 12m,
            PassingScore = 6m,
            IsOnline = true,
            MaxAttempts = 2,
            ShowResultsImmediately = false,
            IsPublished = true,
            PublishedAt = _utcNow.AddDays(-14),
            ResultsPublished = true,
            ResultsPublishedAt = _utcNow.AddDays(-8),
            CreatedAt = _utcNow.AddDays(-16)
        };

        await AddMissingAsync("Exams", [exam], cancellationToken);

        var questions = new List<ExamQuestion>();
        var options = new List<ExamQuestionOption>();
        var order = 0;

        foreach (var spec in PhysicsQuestions)
        {
            order++;
            var questionId = SeedId.For("demo:exam-question", examId, spec.Key);

            questions.Add(new ExamQuestion
            {
                Id = questionId,
                TenantId = group.TenantId,
                ExamId = examId,
                Text = spec.Text,
                Type = spec.Type,
                Points = spec.Points,
                SortOrder = order,
                CorrectAnswer = spec.CorrectAnswer,
                Explanation = spec.Explanation,
                CreatedAt = exam.CreatedAt
            });

            var optionOrder = 0;

            foreach (var (text, isCorrect) in spec.Options)
            {
                optionOrder++;

                options.Add(new ExamQuestionOption
                {
                    Id = SeedId.For("demo:exam-option", questionId, optionOrder),
                    QuestionId = questionId,
                    Text = text,
                    IsCorrect = isCorrect,
                    SortOrder = optionOrder,
                    CreatedAt = exam.CreatedAt
                });
            }
        }

        await AddMissingAsync("ExamQuestions", questions, cancellationToken);
        await AddMissingAsync("ExamQuestionOptions", options, cancellationToken);

        await SyncExamTotalAsync(examId, cancellationToken);
        await SeedAttemptsAsync(examId, group.TenantId, cancellationToken);
    }

    /// <summary>
    /// Lets the exam derive its own maximum from the paper, the way the application does when
    /// a question is added or removed. Seeding the total by hand would risk an exam marked out
    /// of a number its questions do not add up to.
    /// </summary>
    private async Task SyncExamTotalAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await context.Exams.FirstOrDefaultAsync(entity => entity.Id == examId, cancellationToken);

        if (exam is null)
        {
            return;
        }

        var totalPoints = await context.ExamQuestions
            .Where(question => question.ExamId == examId)
            .SumAsync(question => question.Points, cancellationToken);

        if (exam.MaximumScore == totalPoints)
        {
            return;
        }

        exam.SyncMaximumScoreFromQuestions(totalPoints, _utcNow);
        await context.SaveChangesAsync(cancellationToken);
        report.Record("Exams", 0, 1, note: "maximum score synced from questions");
    }

    /// <summary>
    /// Four sittings of the online exam, one per attempt status.
    ///
    /// Marks are not asserted: each answer is graded by <see cref="ExamQuestion.Grade"/> — the
    /// production marker — and the attempt totals come from
    /// <see cref="ExamAttempt.ApplyScore"/>. A seeded score is therefore a score the running
    /// system would have awarded for those same answers.
    /// </summary>
    private async Task SeedAttemptsAsync(Guid examId, Guid tenantId, CancellationToken cancellationToken)
    {
        var exam = await context.Exams
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == examId, cancellationToken);

        if (exam is null)
        {
            return;
        }

        var questions = await context.ExamQuestions
            .Include(question => question.Options)
            .Where(question => question.ExamId == examId)
            .OrderBy(question => question.SortOrder)
            .ToListAsync(cancellationToken);

        if (questions.Count == 0)
        {
            return;
        }

        // Student, status, and how they answered: "all-correct", "mixed", or "blank".
        (string StudentKey, ExamAttemptStatus Status, string Answering)[] plan =
        [
            ("salma", ExamAttemptStatus.Graded, "all-correct"),
            ("youssef", ExamAttemptStatus.Submitted, "mixed"),
            ("zeyad", ExamAttemptStatus.Expired, "blank"),
            ("omnia", ExamAttemptStatus.InProgress, "partial")
        ];

        var attempts = new List<ExamAttempt>();

        foreach (var (studentKey, status, answering) in plan)
        {
            var student = _students[studentKey];
            var attemptId = SeedId.For("demo:exam-attempt", examId, student.Id);
            var startedAt = _utcNow.AddDays(-10).AddMinutes(plan.Length);

            var attempt = new ExamAttempt
            {
                Id = attemptId,
                TenantId = tenantId,
                ExamId = examId,
                StudentId = student.Id,
                AttemptNumber = 1,
                Status = ExamAttemptStatus.InProgress,
                StartedAt = startedAt,
                ExpiresAt = startedAt.AddMinutes(exam.DurationInMinutes),
                MaximumScore = exam.MaximumScore,
                CreatedAt = startedAt
            };

            var awarded = 0m;
            var needsManualGrading = false;

            foreach (var question in questions)
            {
                var (selectedOptionId, textAnswer, answered) = Answer(question, answering);

                // An in-progress sitting has only answered part of the paper so far.
                if (!answered)
                {
                    continue;
                }

                var points = question.Grade(selectedOptionId, textAnswer);

                if (question.Type == ExamQuestionType.Essay)
                {
                    needsManualGrading = true;
                }

                attempt.Answers.Add(new ExamAttemptAnswer
                {
                    Id = SeedId.For("demo:exam-answer", attemptId, question.Id),
                    AttemptId = attemptId,
                    QuestionId = question.Id,
                    SelectedOptionId = selectedOptionId,
                    TextAnswer = textAnswer,
                    IsCorrect = question.Type == ExamQuestionType.Essay ? null : points > 0m,
                    AwardedPoints = points,
                    MaximumPoints = question.Points,
                    AnsweredAt = startedAt.AddMinutes(question.SortOrder * 3),
                    CreatedAt = startedAt.AddMinutes(question.SortOrder * 3)
                });

                awarded += points;
            }

            switch (status)
            {
                case ExamAttemptStatus.Graded:
                    attempt.Submit(startedAt.AddMinutes(40), requiresManualGrading: needsManualGrading);
                    attempt.ApplyScore(awarded, exam.MaximumScore, exam.PassingScore, startedAt.AddMinutes(40));

                    if (needsManualGrading)
                    {
                        // The teacher marked the essay: award it in full and close the attempt.
                        var essay = attempt.Answers.FirstOrDefault(answer =>
                            questions.First(question => question.Id == answer.QuestionId).Type == ExamQuestionType.Essay);

                        if (essay is not null)
                        {
                            essay.AwardedPoints = essay.MaximumPoints;
                            essay.IsCorrect = true;
                            essay.TeacherFeedback = "إجابة ممتازة مع أمثلة واضحة.";
                            awarded += essay.MaximumPoints;
                        }

                        attempt.ApplyScore(awarded, exam.MaximumScore, exam.PassingScore, startedAt.AddMinutes(45));
                        attempt.CompleteManualGrading(
                            _teachers["ahmed"].UserId,
                            startedAt.AddMinutes(45),
                            "مستوى متميز. ركز أكثر على وحدات القياس.");
                    }

                    break;

                case ExamAttemptStatus.Submitted:
                    // Handed in with an essay still waiting for the teacher.
                    attempt.Submit(startedAt.AddMinutes(38), requiresManualGrading: true);
                    attempt.ApplyScore(awarded, exam.MaximumScore, exam.PassingScore, startedAt.AddMinutes(38));
                    break;

                case ExamAttemptStatus.Expired:
                    // The window closed before anything was submitted.
                    attempt.Status = ExamAttemptStatus.Expired;
                    attempt.ApplyScore(0m, exam.MaximumScore, exam.PassingScore, attempt.ExpiresAt!.Value);
                    attempt.UpdatedAt = attempt.ExpiresAt;
                    break;

                case ExamAttemptStatus.InProgress:
                    // Still open: expiry pushed out so the sweep does not immediately close it.
                    attempt.ExpiresAt = _utcNow.AddMinutes(exam.DurationInMinutes);
                    attempt.StartedAt = _utcNow.AddMinutes(-5);
                    break;
            }

            attempts.Add(attempt);
        }

        var inserted = await AddMissingAsync("ExamAttempts", attempts, cancellationToken);
        report.Record("ExamAttemptAnswers", inserted.Sum(attempt => attempt.Answers.Count));

        await SeedOnlineExamResultsAsync(exam, cancellationToken);

        (Guid? SelectedOptionId, string? TextAnswer, bool Answered) Answer(ExamQuestion question, string answering)
        {
            switch (answering)
            {
                case "blank":
                    return (null, null, false);

                case "partial" when question.SortOrder > 2:
                    return (null, null, false);
            }

            var wantCorrect = answering switch
            {
                "all-correct" => true,
                "partial" => true,
                // "mixed": right on the odd-numbered questions, wrong on the even ones.
                _ => question.SortOrder % 2 == 1
            };

            return question.Type switch
            {
                ExamQuestionType.MultipleChoice => (
                    question.Options
                        .OrderBy(option => option.SortOrder)
                        .First(option => option.IsCorrect == wantCorrect).Id,
                    null,
                    true),

                ExamQuestionType.TrueFalse => (
                    null,
                    wantCorrect ? question.CorrectAnswer : "false",
                    true),

                ExamQuestionType.ShortAnswer => (
                    null,
                    wantCorrect ? question.CorrectAnswer : "قانون نيوتن الأول",
                    true),

                _ => (
                    null,
                    "الموجات الطولية تتذبذب في اتجاه الانتشار مثل الصوت، أما المستعرضة فتتذبذب عمودياً عليه مثل الضوء.",
                    true)
            };
        }
    }

    /// <summary>
    /// The class result row for each graded sitting. <see cref="ExamResult"/> is the
    /// teacher-facing record of outcomes, so an online exam needs one per finished attempt
    /// for the results screen and the analytics to agree with the attempts.
    /// </summary>
    private async Task SeedOnlineExamResultsAsync(Exam exam, CancellationToken cancellationToken)
    {
        var finished = await context.ExamAttempts
            .AsNoTracking()
            .Where(attempt => attempt.ExamId == exam.Id
                && (attempt.Status == ExamAttemptStatus.Graded || attempt.Status == ExamAttemptStatus.Expired))
            .ToListAsync(cancellationToken);

        var results = new List<ExamResult>();

        foreach (var attempt in finished)
        {
            var result = new ExamResult
            {
                Id = SeedId.For("demo:exam-result", exam.Id, attempt.StudentId),
                TenantId = exam.TenantId,
                ExamId = exam.Id,
                StudentId = attempt.StudentId,
                MaximumScore = attempt.MaximumScore,
                IsAbsent = false,
                TeacherComment = attempt.Status == ExamAttemptStatus.Expired
                    ? "انتهى وقت الامتحان قبل التسليم."
                    : attempt.TeacherComment,
                RecordedByUserId = _teachers["ahmed"].UserId,
                RecordedAt = attempt.GradedAt ?? attempt.ExpiresAt ?? _utcNow,
                CreatedAt = attempt.GradedAt ?? _utcNow
            };

            result.ApplyScore(attempt.Score, attempt.MaximumScore, exam.PassingScore, result.RecordedAt);
            results.Add(result);
        }

        await AddMissingAsync("ExamResults", results, cancellationToken);
    }

    /// <summary>
    /// An offline exam: sat on paper, marks entered afterwards. Includes an absent student,
    /// whose result carries no mark and must never count as a fail in an average.
    /// </summary>
    private async Task SeedOfflineExamAsync(CancellationToken cancellationToken)
    {
        var group = _groups["center-math-3sec"];
        var teacher = _teachers["hossam"];
        var examId = SeedId.For("demo:exam", "center-math-monthly");

        var exam = new Exam
        {
            Id = examId,
            TenantId = group.TenantId,
            TeacherId = teacher.Id,
            GroupId = group.Id,
            SubjectId = MathematicsId,
            GradeId = ThirdSecondaryId,
            Title = "الامتحان الشهري - الرياضيات",
            Description = "امتحان ورقي شهري يغطي التفاضل والتكامل.",
            Type = ExamType.Monthly,
            ExamDate = _utcNow.AddDays(-20),
            DurationInMinutes = 120,
            MaximumScore = 50m,
            PassingScore = 25m,
            IsOnline = false,
            IsPublished = true,
            PublishedAt = _utcNow.AddDays(-25),
            ResultsPublished = true,
            ResultsPublishedAt = _utcNow.AddDays(-18),
            CreatedAt = _utcNow.AddDays(-28)
        };

        await AddMissingAsync("Exams", [exam], cancellationToken);

        (string StudentKey, decimal Score, bool IsAbsent)[] marks =
        [
            ("omnia", 46m, false),
            ("hassan", 31m, false),
            ("malak", 0m, true)
        ];

        var results = new List<ExamResult>();

        foreach (var (studentKey, score, isAbsent) in marks)
        {
            var student = _students[studentKey];

            var result = new ExamResult
            {
                Id = SeedId.For("demo:exam-result", examId, student.Id),
                TenantId = group.TenantId,
                ExamId = examId,
                StudentId = student.Id,
                MaximumScore = exam.MaximumScore,
                IsAbsent = isAbsent,
                TeacherComment = isAbsent
                    ? "لم يحضر الامتحان."
                    : score >= 40m ? "نتيجة ممتازة." : "يحتاج مراجعة فصل التكامل.",
                RecordedByUserId = teacher.UserId,
                RecordedAt = _utcNow.AddDays(-18),
                ParentNotified = !isAbsent && score < exam.MaximumScore * 0.7m,
                ParentNotifiedAt = !isAbsent && score < exam.MaximumScore * 0.7m
                    ? _utcNow.AddDays(-17)
                    : null,
                CreatedAt = _utcNow.AddDays(-18)
            };

            result.ApplyScore(score, exam.MaximumScore, exam.PassingScore, _utcNow.AddDays(-18));
            results.Add(result);
        }

        await AddMissingAsync("ExamResults", results, cancellationToken);
    }

    /// <summary>
    /// An unpublished online exam with questions but no sittings. Proves a draft exam is
    /// invisible to students and that its question list is only the teacher's to read.
    /// </summary>
    private async Task SeedDraftExamAsync(CancellationToken cancellationToken)
    {
        var group = _groups["math-3sec-calculus"];
        var teacher = _teachers["mariam"];
        var examId = SeedId.For("demo:exam", "math-quiz-draft");

        var exam = new Exam
        {
            Id = examId,
            TenantId = group.TenantId,
            TeacherId = teacher.Id,
            GroupId = group.Id,
            SubjectId = MathematicsId,
            GradeId = ThirdSecondaryId,
            Title = "كويز سريع - النهايات (مسودة)",
            Description = "كويز قيد التحضير لم يُنشر بعد.",
            Type = ExamType.Quiz,
            ExamDate = _utcNow.AddDays(7),
            DurationInMinutes = 15,
            MaximumScore = 3m,
            PassingScore = 2m,
            IsOnline = true,
            MaxAttempts = 1,
            ShowResultsImmediately = true,
            IsPublished = false,
            CreatedAt = _utcNow.AddDays(-1)
        };

        await AddMissingAsync("Exams", [exam], cancellationToken);

        var questionId = SeedId.For("demo:exam-question", examId, "q1");

        await AddMissingAsync(
            "ExamQuestions",
            [
                new ExamQuestion
                {
                    Id = questionId,
                    TenantId = group.TenantId,
                    ExamId = examId,
                    Text = "ما قيمة نهاية (sin x)/x عندما x تقترب من الصفر؟",
                    Type = ExamQuestionType.MultipleChoice,
                    Points = 3m,
                    SortOrder = 1,
                    Explanation = "نهاية شهيرة قيمتها 1.",
                    CreatedAt = exam.CreatedAt
                }
            ],
            cancellationToken);

        await AddMissingAsync(
            "ExamQuestionOptions",
            [
                new ExamQuestionOption
                {
                    Id = SeedId.For("demo:exam-option", questionId, 1),
                    QuestionId = questionId,
                    Text = "1",
                    IsCorrect = true,
                    SortOrder = 1,
                    CreatedAt = exam.CreatedAt
                },
                new ExamQuestionOption
                {
                    Id = SeedId.For("demo:exam-option", questionId, 2),
                    QuestionId = questionId,
                    Text = "0",
                    IsCorrect = false,
                    SortOrder = 2,
                    CreatedAt = exam.CreatedAt
                },
                new ExamQuestionOption
                {
                    Id = SeedId.For("demo:exam-option", questionId, 3),
                    QuestionId = questionId,
                    Text = "لا نهائية",
                    IsCorrect = false,
                    SortOrder = 3,
                    CreatedAt = exam.CreatedAt
                }
            ],
            cancellationToken);
    }
}
