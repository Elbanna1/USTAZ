using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Exams.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Exams;

/// <summary>
/// Full analytics for one exam.
///
/// Two storage shapes feed this: an online exam is sat in the app and produces
/// <c>ExamAttempts</c> with per-question answers, while an offline exam only ever has the
/// teacher's recorded <c>ExamResults</c>. The overview and performance sections are produced
/// for both; the question-level sections exist only where question-level data does, and come
/// back empty rather than guessed for an offline paper.
///
/// Nothing here exposes the answer key. Question rows carry the text, the type, the points and
/// how the cohort did — never the correct option or the model answer — so the same payload is
/// safe for any workspace member holding <c>Exams.View</c>.
/// </summary>
public sealed record GetExamAnalyticsQuery(Guid ExamId) : IRequest<ExamAnalyticsDto>
{
    /// <summary>How many entries to return in each of the ranked lists.</summary>
    public int Take { get; init; } = 5;
}

public sealed class GetExamAnalyticsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetExamAnalyticsQuery, ExamAnalyticsDto>
{
    /// <summary>Ten-point bands, so the distribution chart always has the same shape.</summary>
    private const int BucketCount = 10;

    public async Task<ExamAnalyticsDto> Handle(
        GetExamAnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.ExamsView, cancellationToken: cancellationToken);

        var take = Math.Clamp(query.Take, 1, 25);

        var exam = await context.Exams
            .AsNoTracking()
            .Where(entity => entity.Id == query.ExamId)
            .Select(entity => new ExamShape
            {
                Id = entity.Id,
                TenantId = entity.TenantId,
                Title = entity.Title,
                Type = entity.Type,
                IsOnline = entity.IsOnline,
                ExamDate = entity.ExamDate,
                DurationInMinutes = entity.DurationInMinutes,
                MaximumScore = entity.MaximumScore,
                PassingScore = entity.PassingScore,
                IsPublished = entity.IsPublished,
                ResultsPublished = entity.ResultsPublished,
                QuestionsCount = entity.Questions.Count,
                GroupId = entity.GroupId,
                GroupName = entity.Group != null ? entity.Group.Name : null,
                SubjectId = entity.SubjectId,
                SubjectNameAr = entity.Subject.NameAr,
                SubjectNameEn = entity.Subject.NameEn
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Exam", query.ExamId);

        // Cross-workspace reads are indistinguishable from a missing exam.
        tenantAuthorization.EnsureSameTenant(access, exam.TenantId);

        var expectedParticipants = exam.GroupId is null
            ? (int?)null
            : await context.GroupEnrollments
                .AsNoTracking()
                .CountAsync(
                    enrollment => enrollment.GroupId == exam.GroupId.Value
                        && enrollment.Status == EnrollmentStatus.Active,
                    cancellationToken);

        var standings = exam.IsOnline
            ? await LoadOnlineStandingsAsync(exam, cancellationToken)
            : await LoadOfflineStandingsAsync(exam, cancellationToken);

        var questions = exam.IsOnline
            ? await LoadQuestionAnalyticsAsync(exam, cancellationToken)
            : [];

        var performance = Summarise(standings);

        return new ExamAnalyticsDto
        {
            Overview = new ExamAnalyticsOverviewDto
            {
                ExamId = exam.Id,
                Title = exam.Title,
                Type = exam.Type,
                IsOnline = exam.IsOnline,
                ExamDate = exam.ExamDate,
                DurationInMinutes = exam.DurationInMinutes,
                MaximumScore = exam.MaximumScore,
                PassingScore = exam.PassingScore,
                IsPublished = exam.IsPublished,
                ResultsPublished = exam.ResultsPublished,
                QuestionsCount = exam.QuestionsCount,
                GroupId = exam.GroupId,
                GroupName = exam.GroupName,
                SubjectId = exam.SubjectId,
                SubjectNameAr = exam.SubjectNameAr,
                SubjectNameEn = exam.SubjectNameEn,
                ExpectedParticipants = expectedParticipants
            },
            Performance = performance,
            ScoreDistribution = BuildDistribution(standings),
            Questions = questions,
            MostDifficultQuestions =
            [
                .. questions
                    .Where(question => question.Difficulty != QuestionDifficulty.Unknown)
                    .OrderBy(question => question.CorrectPercentage)
                    .ThenByDescending(question => question.IncorrectCount)
                    .Take(take)
            ],
            MostSuccessfulQuestions =
            [
                .. questions
                    .Where(question => question.Difficulty != QuestionDifficulty.Unknown)
                    .OrderByDescending(question => question.CorrectPercentage)
                    .ThenByDescending(question => question.CorrectCount)
                    .Take(take)
            ],
            TopicPerformance = BuildTopicPerformance(questions),
            TopPerformers =
            [
                .. standings
                    .Where(standing => standing.Percentage.HasValue)
                    .OrderByDescending(standing => standing.Percentage)
                    .Take(take)
            ],
            LowestPerformers =
            [
                .. standings
                    .Where(standing => standing.Percentage.HasValue)
                    .OrderBy(standing => standing.Percentage)
                    .Take(take)
            ],
            NotAttempted = await LoadNotAttemptedAsync(exam, standings, cancellationToken),
            Insights = BuildInsights(exam, performance, questions, expectedParticipants)
        };
    }

    // -----------------------------------------------------------------------
    // Loading
    // -----------------------------------------------------------------------

    /// <summary>
    /// One row per student for an online exam. A student may sit a retake, so only the best
    /// scoring finished sitting counts towards the cohort figures; the open and expired ones
    /// are still surfaced as counts in the summary.
    /// </summary>
    private async Task<List<ExamStudentStandingDto>> LoadOnlineStandingsAsync(
        ExamShape exam,
        CancellationToken cancellationToken)
    {
        var attempts = await context.ExamAttempts
            .AsNoTracking()
            .Where(attempt => attempt.ExamId == exam.Id)
            .Select(attempt => new
            {
                attempt.StudentId,
                StudentName = attempt.Student.User.DisplayName,
                attempt.Score,
                attempt.Percentage,
                attempt.IsPassed,
                attempt.Status,
                attempt.RequiresManualGrading,
                attempt.StartedAt,
                attempt.SubmittedAt,
                attempt.AttemptNumber
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. attempts
                .GroupBy(attempt => attempt.StudentId)
                .Select(group =>
                {
                    // A finished sitting beats an open one; among finished ones the best mark
                    // stands, which matches how the student's grade is reported elsewhere.
                    var best = group
                        .OrderByDescending(attempt => attempt.Status == ExamAttemptStatus.Graded)
                        .ThenByDescending(attempt => attempt.Percentage)
                        .ThenByDescending(attempt => attempt.AttemptNumber)
                        .First();

                    var scored = best.Status is ExamAttemptStatus.Graded or ExamAttemptStatus.Submitted
                        or ExamAttemptStatus.Expired;

                    return new ExamStudentStandingDto
                    {
                        StudentId = group.Key,
                        StudentName = best.StudentName,
                        Score = scored ? best.Score : null,
                        Percentage = scored ? best.Percentage : null,
                        IsPassed = best.Status == ExamAttemptStatus.Graded ? best.IsPassed : null,
                        IsAbsent = false,
                        AttemptStatus = best.Status,
                        SubmittedAt = best.SubmittedAt,
                        DurationMinutes = best.SubmittedAt is { } submitted
                            ? Round((decimal)(submitted - best.StartedAt).TotalMinutes)
                            : null
                    };
                })
                .OrderByDescending(standing => standing.Percentage ?? -1m)
        ];
    }

    /// <summary>One row per recorded mark for an offline exam.</summary>
    private async Task<List<ExamStudentStandingDto>> LoadOfflineStandingsAsync(
        ExamShape exam,
        CancellationToken cancellationToken) =>
        await context.ExamResults
            .AsNoTracking()
            .Where(result => result.ExamId == exam.Id)
            .OrderByDescending(result => result.Percentage)
            .Select(result => new ExamStudentStandingDto
            {
                StudentId = result.StudentId,
                StudentName = result.Student.User.DisplayName,
                // An absentee has no mark to report, so the score reads as absent, not as zero.
                Score = result.IsAbsent ? null : result.Score,
                Percentage = result.IsAbsent ? null : result.Percentage,
                IsPassed = result.IsAbsent ? null : result.IsPassed,
                IsAbsent = result.IsAbsent,
                AttemptStatus = null,
                SubmittedAt = result.RecordedAt,
                DurationMinutes = null
            })
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Per-question figures for an online exam. Only finished sittings contribute: an answer
    /// inside a sitting still in progress can change, and counting it would let the difficulty
    /// of a question drift while the class is mid-exam.
    /// </summary>
    private async Task<List<QuestionAnalyticsDto>> LoadQuestionAnalyticsAsync(
        ExamShape exam,
        CancellationToken cancellationToken)
    {
        var questions = await context.ExamQuestions
            .AsNoTracking()
            .Where(question => question.ExamId == exam.Id)
            .OrderBy(question => question.SortOrder)
            .Select(question => new
            {
                question.Id,
                question.Text,
                question.Type,
                question.Points,
                question.SortOrder
            })
            .ToListAsync(cancellationToken);

        if (questions.Count == 0)
        {
            return [];
        }

        var finishedAttempts = await context.ExamAttempts
            .AsNoTracking()
            .CountAsync(
                attempt => attempt.ExamId == exam.Id
                    && attempt.Status != ExamAttemptStatus.InProgress,
                cancellationToken);

        var answerStats = await context.ExamAttemptAnswers
            .AsNoTracking()
            .Where(answer => answer.Attempt.ExamId == exam.Id
                && answer.Attempt.Status != ExamAttemptStatus.InProgress)
            .GroupBy(answer => answer.QuestionId)
            .Select(group => new
            {
                QuestionId = group.Key,
                Answered = group.Count(),
                Correct = group.Count(answer => answer.IsCorrect == true),
                Incorrect = group.Count(answer => answer.IsCorrect == false),
                AwaitingGrading = group.Count(answer => answer.IsCorrect == null),
                // Awarded points are only meaningful once the answer carries a verdict.
                AwardedSum = group
                    .Where(answer => answer.IsCorrect != null)
                    .Sum(answer => (decimal?)answer.AwardedPoints) ?? 0m
            })
            .ToDictionaryAsync(entry => entry.QuestionId, cancellationToken);

        return
        [
            .. questions.Select(question =>
            {
                answerStats.TryGetValue(question.Id, out var stats);

                var answered = stats?.Answered ?? 0;
                var correct = stats?.Correct ?? 0;
                var incorrect = stats?.Incorrect ?? 0;
                var awaiting = stats?.AwaitingGrading ?? 0;
                var graded = correct + incorrect;

                return new QuestionAnalyticsDto
                {
                    QuestionId = question.Id,
                    Text = question.Text,
                    Type = question.Type,
                    Points = question.Points,
                    SortOrder = question.SortOrder,
                    AnsweredCount = answered,
                    SkippedCount = Math.Max(0, finishedAttempts - answered),
                    CorrectCount = correct,
                    IncorrectCount = incorrect,
                    AwaitingGradingCount = awaiting,
                    CorrectPercentage = Percentage(correct, graded),
                    IncorrectPercentage = Percentage(incorrect, graded),
                    AverageAwardedPoints = graded == 0
                        ? 0m
                        : Round((stats?.AwardedSum ?? 0m) / graded),
                    Difficulty = Classify(correct, graded)
                };
            })
        ];
    }

    /// <summary>
    /// Active members of the exam group with no mark at all. Returns nothing for an exam that
    /// is not tied to a group, because there is then no roll to compare against.
    /// </summary>
    private async Task<List<ExamStudentStandingDto>> LoadNotAttemptedAsync(
        ExamShape exam,
        List<ExamStudentStandingDto> standings,
        CancellationToken cancellationToken)
    {
        if (exam.GroupId is null)
        {
            return [];
        }

        var accountedFor = standings.Select(standing => standing.StudentId).ToList();

        return await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.GroupId == exam.GroupId.Value
                && enrollment.Status == EnrollmentStatus.Active
                && !accountedFor.Contains(enrollment.StudentId))
            .OrderBy(enrollment => enrollment.Student.User.DisplayName)
            .Select(enrollment => new ExamStudentStandingDto
            {
                StudentId = enrollment.StudentId,
                StudentName = enrollment.Student.User.DisplayName,
                Score = null,
                Percentage = null,
                IsPassed = null,
                IsAbsent = false,
                AttemptStatus = null,
                SubmittedAt = null,
                DurationMinutes = null
            })
            .ToListAsync(cancellationToken);
    }

    // -----------------------------------------------------------------------
    // Aggregation
    // -----------------------------------------------------------------------

    private static ExamPerformanceSummaryDto Summarise(List<ExamStudentStandingDto> standings)
    {
        var scored = standings
            .Where(standing => standing.Percentage.HasValue)
            .ToList();

        var percentages = scored
            .Select(standing => standing.Percentage!.Value)
            .OrderBy(value => value)
            .ToList();

        var durations = standings
            .Where(standing => standing.DurationMinutes.HasValue)
            .Select(standing => standing.DurationMinutes!.Value)
            .ToList();

        // Pass and fail are only counted where a final verdict exists: a sitting awaiting a
        // teacher is neither, and folding it into either would move the pass rate.
        var passed = standings.Count(standing => standing.IsPassed == true);
        var failed = standings.Count(standing => standing.IsPassed == false);
        var decided = passed + failed;

        return new ExamPerformanceSummaryDto
        {
            TotalAttempts = standings.Count,
            ScoredAttempts = scored.Count,
            AbsentCount = standings.Count(standing => standing.IsAbsent),
            InProgressCount = standings.Count(
                standing => standing.AttemptStatus == ExamAttemptStatus.InProgress),
            ExpiredCount = standings.Count(
                standing => standing.AttemptStatus == ExamAttemptStatus.Expired),
            AwaitingGradingCount = standings.Count(
                standing => standing.AttemptStatus == ExamAttemptStatus.Submitted),
            AverageScore = scored.Count == 0
                ? 0m
                : Round(scored.Sum(standing => standing.Score ?? 0m) / scored.Count),
            AveragePercentage = percentages.Count == 0 ? 0m : Round(percentages.Average()),
            HighestPercentage = percentages.Count == 0 ? 0m : Round(percentages[^1]),
            LowestPercentage = percentages.Count == 0 ? 0m : Round(percentages[0]),
            MedianPercentage = Median(percentages),
            StandardDeviation = StandardDeviation(percentages),
            PassedCount = passed,
            FailedCount = failed,
            PassRate = Percentage(passed, decided),
            FailRate = Percentage(failed, decided),
            AverageDurationMinutes = durations.Count == 0 ? null : Round(durations.Average())
        };
    }

    private static List<ScoreBucketDto> BuildDistribution(List<ExamStudentStandingDto> standings)
    {
        var percentages = standings
            .Where(standing => standing.Percentage.HasValue)
            .Select(standing => standing.Percentage!.Value)
            .ToList();

        var buckets = new List<ScoreBucketDto>(BucketCount);

        for (var index = 0; index < BucketCount; index++)
        {
            var from = index * 10;
            var to = from + 10;
            var isTop = index == BucketCount - 1;

            // Bands are half-open so a mark lands in exactly one, except the last which has to
            // take 100 itself.
            var count = percentages.Count(value =>
                value >= from && (isTop ? value <= to : value < to));

            buckets.Add(new ScoreBucketDto
            {
                FromPercentage = from,
                ToPercentage = to,
                Label = from + "-" + to + "%",
                Count = count,
                Percentage = Percentage(count, percentages.Count)
            });
        }

        return buckets;
    }

    private static List<ExamTopicPerformanceDto> BuildTopicPerformance(
        List<QuestionAnalyticsDto> questions) =>
        [
            .. questions
                .GroupBy(question => question.Type)
                .Select(group =>
                {
                    var correct = group.Sum(question => question.CorrectCount);
                    var graded = correct + group.Sum(question => question.IncorrectCount);

                    return new ExamTopicPerformanceDto
                    {
                        Topic = group.Key.ToString(),
                        QuestionsCount = group.Count(),
                        GradedAnswers = graded,
                        CorrectAnswers = correct,
                        CorrectPercentage = Percentage(correct, graded)
                    };
                })
                .OrderBy(entry => entry.Topic)
        ];

    /// <summary>
    /// Turns the figures into the handful of statements a teacher would actually act on. Every
    /// one is a direct reading of a number computed above; nothing is inferred or predicted.
    /// </summary>
    private static List<ExamInsightDto> BuildInsights(
        ExamShape exam,
        ExamPerformanceSummaryDto performance,
        List<QuestionAnalyticsDto> questions,
        int? expectedParticipants)
    {
        var insights = new List<ExamInsightDto>();

        if (performance.ScoredAttempts == 0)
        {
            insights.Add(new ExamInsightDto
            {
                Code = "NO_RESULTS",
                Severity = ExamInsightSeverity.Info,
                Message = "مفيش نتايج متسجلة للامتحان ده لحد الآن."
            });

            return insights;
        }

        if (performance.PassedCount + performance.FailedCount > 0 && performance.PassRate < 50m)
        {
            insights.Add(new ExamInsightDto
            {
                Code = "LOW_PASS_RATE",
                Severity = ExamInsightSeverity.Critical,
                Message = "نسبة النجاح " + performance.PassRate.ToString("0.##")
                    + "% — أقل من نصف الطلبة نجحوا.",
                Value = performance.PassRate
            });
        }

        if (performance.AveragePercentage < exam.PassingScorePercentage)
        {
            insights.Add(new ExamInsightDto
            {
                Code = "AVERAGE_BELOW_PASS_MARK",
                Severity = ExamInsightSeverity.Warning,
                Message = "متوسط الدرجات " + performance.AveragePercentage.ToString("0.##")
                    + "% وده تحت درجة النجاح.",
                Value = performance.AveragePercentage
            });
        }

        if (performance.StandardDeviation >= 25m)
        {
            insights.Add(new ExamInsightDto
            {
                Code = "WIDE_SPREAD",
                Severity = ExamInsightSeverity.Info,
                Message = "الفرق بين الطلبة كبير (انحراف معياري "
                    + performance.StandardDeviation.ToString("0.##") + ") — فيه مستويات متباعدة.",
                Value = performance.StandardDeviation
            });
        }

        if (performance.AwaitingGradingCount > 0)
        {
            insights.Add(new ExamInsightDto
            {
                Code = "AWAITING_GRADING",
                Severity = ExamInsightSeverity.Warning,
                Message = performance.AwaitingGradingCount
                    + " محاولة مستنية تصحيح الأسئلة المقالية.",
                Value = performance.AwaitingGradingCount
            });
        }

        if (performance.ExpiredCount > 0)
        {
            insights.Add(new ExamInsightDto
            {
                Code = "EXPIRED_ATTEMPTS",
                Severity = ExamInsightSeverity.Info,
                Message = performance.ExpiredCount + " طالب خلص وقته قبل ما يسلّم.",
                Value = performance.ExpiredCount
            });
        }

        var struggled = questions
            .Count(question => question.Difficulty == QuestionDifficulty.VeryHard);

        if (struggled > 0)
        {
            insights.Add(new ExamInsightDto
            {
                Code = "QUESTIONS_NEED_REVIEW",
                Severity = ExamInsightSeverity.Warning,
                Message = struggled + " سؤال جاب أقل من 30% إجابات صحيحة — محتاج مراجعة مع الطلبة.",
                Value = struggled
            });
        }

        if (expectedParticipants is { } expected && expected > 0)
        {
            var missing = expected - performance.ScoredAttempts - performance.AbsentCount;

            if (missing > 0)
            {
                insights.Add(new ExamInsightDto
                {
                    Code = "MISSING_PARTICIPANTS",
                    Severity = ExamInsightSeverity.Warning,
                    Message = missing + " طالب من المجموعة لسه مالهوش نتيجة.",
                    Value = missing
                });
            }
        }

        if (!exam.ResultsPublished && performance.ScoredAttempts > 0)
        {
            insights.Add(new ExamInsightDto
            {
                Code = "RESULTS_NOT_PUBLISHED",
                Severity = ExamInsightSeverity.Info,
                Message = "النتايج لسه مش منشورة للطلبة."
            });
        }

        return insights;
    }

    // -----------------------------------------------------------------------
    // Maths
    // -----------------------------------------------------------------------

    private static QuestionDifficulty Classify(int correct, int graded)
    {
        if (graded == 0)
        {
            return QuestionDifficulty.Unknown;
        }

        var share = (decimal)correct / graded * 100m;

        return share switch
        {
            >= 80m => QuestionDifficulty.Easy,
            >= 50m => QuestionDifficulty.Moderate,
            >= 30m => QuestionDifficulty.Hard,
            _ => QuestionDifficulty.VeryHard
        };
    }

    /// <summary>Expects <paramref name="sorted"/> in ascending order.</summary>
    private static decimal Median(List<decimal> sorted)
    {
        if (sorted.Count == 0)
        {
            return 0m;
        }

        var middle = sorted.Count / 2;

        return sorted.Count % 2 == 1
            ? Round(sorted[middle])
            : Round((sorted[middle - 1] + sorted[middle]) / 2m);
    }

    /// <summary>
    /// Population standard deviation: these are all the marks for this exam, not a sample
    /// drawn from a larger set.
    /// </summary>
    private static decimal StandardDeviation(List<decimal> values)
    {
        if (values.Count < 2)
        {
            return 0m;
        }

        var mean = values.Average();
        var variance = values.Sum(value => (value - mean) * (value - mean)) / values.Count;

        return Round((decimal)Math.Sqrt((double)variance));
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Percentage(int part, int total) =>
        total == 0 ? 0m : Round((decimal)part / total * 100m);

    /// <summary>Flat shape of the exam, read once and reused across every section.</summary>
    private sealed record ExamShape
    {
        public Guid Id { get; init; }

        public Guid TenantId { get; init; }

        public string Title { get; init; } = string.Empty;

        public ExamType Type { get; init; }

        public bool IsOnline { get; init; }

        public DateTime ExamDate { get; init; }

        public int DurationInMinutes { get; init; }

        public decimal MaximumScore { get; init; }

        public decimal PassingScore { get; init; }

        public bool IsPublished { get; init; }

        public bool ResultsPublished { get; init; }

        public int QuestionsCount { get; init; }

        public Guid? GroupId { get; init; }

        public string? GroupName { get; init; }

        public Guid SubjectId { get; init; }

        public string SubjectNameAr { get; init; } = string.Empty;

        public string SubjectNameEn { get; init; } = string.Empty;

        /// <summary>The pass mark expressed on the same 0-100 scale as the averages.</summary>
        public decimal PassingScorePercentage => MaximumScore <= 0m
            ? 0m
            : Math.Round(PassingScore / MaximumScore * 100m, 2, MidpointRounding.AwayFromZero);
    }
}
