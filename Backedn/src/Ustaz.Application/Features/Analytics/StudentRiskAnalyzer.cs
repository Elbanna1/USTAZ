using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Analytics;

/// <summary>How much attention a student needs, and why.</summary>
public enum StudentRiskLevel
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

/// <summary>One reason a student was flagged, with the number behind it.</summary>
public sealed record RiskReasonDto
{
    public required string Code { get; init; }

    /// <summary>Plain Egyptian Arabic, so a teacher can read the list as-is.</summary>
    public required string MessageAr { get; init; }

    public required string MessageEn { get; init; }

    /// <summary>The measured value that triggered this reason.</summary>
    public decimal Value { get; init; }

    /// <summary>The threshold it fell on the wrong side of.</summary>
    public decimal Threshold { get; init; }
}

/// <summary>A student's standing, with every number that produced it.</summary>
public sealed record AtRiskStudentDto
{
    public Guid StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public StudentRiskLevel RiskLevel { get; init; }

    public string RiskLevelName => RiskLevel.ToString();

    /// <summary>0–100. Higher means more concerning; it is a sum of weighted reasons.</summary>
    public int RiskScore { get; init; }

    public IReadOnlyList<RiskReasonDto> Reasons { get; init; } = [];

    public decimal AttendanceRate { get; init; }

    public int AbsenceCount { get; init; }

    public decimal HomeworkCompletionRate { get; init; }

    public int MissingHomeworkCount { get; init; }

    public decimal? AverageExamPercentage { get; init; }

    public int FailedExamCount { get; init; }

    public decimal AverageCourseProgress { get; init; }

    public DateTime? LastActivityAt { get; init; }

    public int DaysSinceLastActivity { get; init; }
}

/// <summary>
/// Works out which students are slipping, from data the platform already records.
///
/// There is nothing predictive here and nothing opaque: each rule is a threshold on a real
/// measurement, each fires its own reason with the value and the threshold attached, and the
/// score is the sum of the weights. A teacher can always answer "why is this student flagged"
/// by reading the list back.
///
/// It is written as a handful of grouped queries over the whole workspace rather than a query
/// per student, so the cost does not grow with class size.
/// </summary>
public sealed class StudentRiskAnalyzer(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
{
    // --- Thresholds, in one place so they can be read and argued with ---------
    private const decimal LowAttendanceThreshold = 75m;
    private const decimal CriticalAttendanceThreshold = 50m;
    private const decimal LowHomeworkThreshold = 70m;
    private const int MissingHomeworkThreshold = 3;
    private const decimal LowExamThreshold = 50m;
    private const int InactivityDaysThreshold = 14;
    private const decimal LowCourseProgressThreshold = 25m;

    private const int HighRiskScore = 60;
    private const int MediumRiskScore = 35;
    private const int LowRiskScore = 15;

    /// <summary>Analyses every active student of a workspace over the given window.</summary>
    public async Task<IReadOnlyList<AtRiskStudentDto>> AnalyzeTenantAsync(
        Guid tenantId,
        int windowInDays,
        CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;
        var since = utcNow.AddDays(-Math.Clamp(windowInDays, 7, 365));

        var students = await context.TenantStudents
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId && link.IsActive)
            .Select(link => new { link.StudentId, Name = link.Student.User.DisplayName })
            .ToListAsync(cancellationToken);

        if (students.Count == 0)
        {
            return [];
        }

        var studentIds = students.Select(student => student.StudentId).ToList();

        // One aggregate per concern, all keyed by student id.
        var attendance = await context.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.TenantId == tenantId
                && studentIds.Contains(record.StudentId)
                && record.Session.SessionDate >= since)
            .GroupBy(record => record.StudentId)
            .Select(group => new
            {
                StudentId = group.Key,
                Total = group.Count(),
                Present = group.Count(record => record.Status == AttendanceStatus.Present
                    || record.Status == AttendanceStatus.Late),
                Absent = group.Count(record => record.Status == AttendanceStatus.Absent),
                LastSession = group.Max(record => record.Session.SessionDate)
            })
            .ToDictionaryAsync(entry => entry.StudentId, cancellationToken);

        var homework = await context.Homeworks
            .AsNoTracking()
            .Where(entity => entity.TenantId == tenantId && entity.IsPublished && entity.DueAt >= since)
            .SelectMany(
                entity => context.GroupEnrollments
                    .Where(enrollment => enrollment.GroupId == entity.GroupId
                        && enrollment.Status == EnrollmentStatus.Active
                        && studentIds.Contains(enrollment.StudentId))
                    .Select(enrollment => new
                    {
                        enrollment.StudentId,
                        HomeworkId = entity.Id,
                        Submitted = context.HomeworkSubmissions.Any(submission =>
                            submission.HomeworkId == entity.Id
                            && submission.StudentId == enrollment.StudentId
                            && submission.Status != HomeworkSubmissionStatus.NotSubmitted)
                    }))
            .GroupBy(entry => entry.StudentId)
            .Select(group => new
            {
                StudentId = group.Key,
                Assigned = group.Count(),
                Submitted = group.Count(entry => entry.Submitted)
            })
            .ToDictionaryAsync(entry => entry.StudentId, cancellationToken);

        var exams = await context.ExamResults
            .AsNoTracking()
            .Where(result => result.TenantId == tenantId
                && studentIds.Contains(result.StudentId)
                && result.RecordedAt >= since)
            .GroupBy(result => result.StudentId)
            .Select(group => new
            {
                StudentId = group.Key,
                Average = group.Average(result => result.Percentage),
                Failed = group.Count(result => !result.IsPassed),
                LastRecorded = group.Max(result => result.RecordedAt)
            })
            .ToDictionaryAsync(entry => entry.StudentId, cancellationToken);

        var progress = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => studentIds.Contains(enrollment.StudentId)
                && enrollment.Status == EnrollmentStatus.Active
                && enrollment.Course.TenantId == tenantId)
            .GroupBy(enrollment => enrollment.StudentId)
            .Select(group => new
            {
                StudentId = group.Key,
                Average = (decimal)group.Average(enrollment => enrollment.ProgressPercentage),
                LastAccessed = group.Max(enrollment => enrollment.LastAccessedAt)
            })
            .ToDictionaryAsync(entry => entry.StudentId, cancellationToken);

        var submissions = await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(submission => submission.TenantId == tenantId
                && studentIds.Contains(submission.StudentId)
                && submission.SubmittedAt != null)
            .GroupBy(submission => submission.StudentId)
            .Select(group => new { StudentId = group.Key, Last = group.Max(entry => entry.SubmittedAt) })
            .ToDictionaryAsync(entry => entry.StudentId, cancellationToken);

        var results = new List<AtRiskStudentDto>(students.Count);

        foreach (var student in students)
        {
            results.Add(Evaluate(
                student.StudentId,
                student.Name,
                attendance.GetValueOrDefault(student.StudentId) is { } attendanceEntry
                    ? (attendanceEntry.Total, attendanceEntry.Present, attendanceEntry.Absent, attendanceEntry.LastSession)
                    : null,
                homework.GetValueOrDefault(student.StudentId) is { } homeworkEntry
                    ? (homeworkEntry.Assigned, homeworkEntry.Submitted)
                    : null,
                exams.GetValueOrDefault(student.StudentId) is { } examEntry
                    ? (examEntry.Average, examEntry.Failed, examEntry.LastRecorded)
                    : null,
                progress.GetValueOrDefault(student.StudentId) is { } progressEntry
                    ? (progressEntry.Average, progressEntry.LastAccessed)
                    : null,
                submissions.GetValueOrDefault(student.StudentId)?.Last,
                utcNow));
        }

        return results;
    }

    /// <summary>Applies every rule to one student's numbers.</summary>
    private static AtRiskStudentDto Evaluate(
        Guid studentId,
        string studentName,
        (int Total, int Present, int Absent, DateTime LastSession)? attendance,
        (int Assigned, int Submitted)? homework,
        (decimal Average, int Failed, DateTime LastRecorded)? exams,
        (decimal Average, DateTime? LastAccessed)? progress,
        DateTime? lastSubmission,
        DateTime utcNow)
    {
        var reasons = new List<RiskReasonDto>();
        var score = 0;

        // --- Attendance -------------------------------------------------------
        var attendanceRate = attendance is { Total: > 0 }
            ? Math.Round(attendance.Value.Present * 100m / attendance.Value.Total, 1)
            : 100m;

        if (attendance is { Total: > 0 } && attendanceRate < CriticalAttendanceThreshold)
        {
            score += 35;
            reasons.Add(new RiskReasonDto
            {
                Code = "attendance.critical",
                MessageAr = "الحضور ضعيف جدًا (" + attendanceRate + "%)",
                MessageEn = "Attendance is very low (" + attendanceRate + "%)",
                Value = attendanceRate,
                Threshold = CriticalAttendanceThreshold
            });
        }
        else if (attendance is { Total: > 0 } && attendanceRate < LowAttendanceThreshold)
        {
            score += 20;
            reasons.Add(new RiskReasonDto
            {
                Code = "attendance.low",
                MessageAr = "الحضور تحت المستوى (" + attendanceRate + "%)",
                MessageEn = "Attendance is below target (" + attendanceRate + "%)",
                Value = attendanceRate,
                Threshold = LowAttendanceThreshold
            });
        }

        // --- Homework ---------------------------------------------------------
        var assigned = homework?.Assigned ?? 0;
        var submitted = homework?.Submitted ?? 0;
        var missing = Math.Max(0, assigned - submitted);

        var homeworkRate = assigned > 0
            ? Math.Round(submitted * 100m / assigned, 1)
            : 100m;

        if (missing >= MissingHomeworkThreshold)
        {
            score += 25;
            reasons.Add(new RiskReasonDto
            {
                Code = "homework.missing",
                MessageAr = missing + " واجبات لسه ماتسلمتش",
                MessageEn = missing + " homework assignments not handed in",
                Value = missing,
                Threshold = MissingHomeworkThreshold
            });
        }
        else if (assigned > 0 && homeworkRate < LowHomeworkThreshold)
        {
            score += 15;
            reasons.Add(new RiskReasonDto
            {
                Code = "homework.low",
                MessageAr = "نسبة تسليم الواجبات ضعيفة (" + homeworkRate + "%)",
                MessageEn = "Homework completion is low (" + homeworkRate + "%)",
                Value = homeworkRate,
                Threshold = LowHomeworkThreshold
            });
        }

        // --- Exams ------------------------------------------------------------
        if (exams is { } examData && examData.Average < LowExamThreshold)
        {
            score += 25;
            reasons.Add(new RiskReasonDto
            {
                Code = "exams.low",
                MessageAr = "متوسط الامتحانات ضعيف (" + Math.Round(examData.Average, 1) + "%)",
                MessageEn = "Exam average is low (" + Math.Round(examData.Average, 1) + "%)",
                Value = Math.Round(examData.Average, 1),
                Threshold = LowExamThreshold
            });
        }

        // --- Course progress --------------------------------------------------
        var courseProgress = progress?.Average ?? 0m;

        if (progress is not null && courseProgress < LowCourseProgressThreshold)
        {
            score += 10;
            reasons.Add(new RiskReasonDto
            {
                Code = "course.progress_low",
                MessageAr = "التقدم في الكورسات بطيء (" + Math.Round(courseProgress, 1) + "%)",
                MessageEn = "Course progress is slow (" + Math.Round(courseProgress, 1) + "%)",
                Value = Math.Round(courseProgress, 1),
                Threshold = LowCourseProgressThreshold
            });
        }

        // --- Inactivity -------------------------------------------------------
        var lastActivity = new[]
            {
                attendance?.LastSession,
                exams?.LastRecorded,
                progress?.LastAccessed,
                lastSubmission
            }
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty()
            .Max();

        var hasActivity = lastActivity != default;
        var daysSince = hasActivity ? (int)(utcNow - lastActivity).TotalDays : 0;

        if (hasActivity && daysSince >= InactivityDaysThreshold)
        {
            score += 15;
            reasons.Add(new RiskReasonDto
            {
                Code = "activity.stale",
                MessageAr = "مفيش أي نشاط بقاله " + daysSince + " يوم",
                MessageEn = "No activity for " + daysSince + " days",
                Value = daysSince,
                Threshold = InactivityDaysThreshold
            });
        }

        score = Math.Clamp(score, 0, 100);

        var level = score >= HighRiskScore
            ? StudentRiskLevel.High
            : score >= MediumRiskScore
                ? StudentRiskLevel.Medium
                : score >= LowRiskScore
                    ? StudentRiskLevel.Low
                    : StudentRiskLevel.None;

        return new AtRiskStudentDto
        {
            StudentId = studentId,
            StudentName = studentName,
            RiskLevel = level,
            RiskScore = score,
            Reasons = reasons,
            AttendanceRate = attendanceRate,
            AbsenceCount = attendance?.Absent ?? 0,
            HomeworkCompletionRate = homeworkRate,
            MissingHomeworkCount = missing,
            AverageExamPercentage = exams is { } data ? Math.Round(data.Average, 1) : null,
            FailedExamCount = exams?.Failed ?? 0,
            AverageCourseProgress = Math.Round(courseProgress, 1),
            LastActivityAt = hasActivity ? lastActivity : null,
            DaysSinceLastActivity = daysSince
        };
    }
}
