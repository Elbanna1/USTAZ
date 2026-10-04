using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    /// <summary>
    /// Assignments and what students did with them.
    ///
    /// Five assignments cover the whole lifecycle a homework screen renders: open and not yet
    /// due, overdue but still accepting late work, closed to further submissions, a draft, and
    /// one with a teacher attachment. The submissions cover every value of
    /// <see cref="HomeworkSubmissionStatus"/>, including a row returned for revision — the one
    /// state that grants a student another attempt whatever the assignment's policy says.
    ///
    /// Submissions are recorded through <see cref="HomeworkSubmission.Submit"/> and
    /// <see cref="HomeworkSubmission.Grade"/>, so lateness and percentages are decided by the
    /// domain from the deadline and the mark rather than written down by hand.
    /// </summary>
    private async Task SeedHomeworkAsync(CancellationToken cancellationToken)
    {
        var assignments = new List<Homework>();
        var attachments = new List<HomeworkAttachment>();

        // key, group, teacher, offset of the deadline in days, published, closed, late allowed
        (string Key, string GroupKey, string TeacherKey, string Title, string Instructions,
            int DueInDays, decimal MaximumScore, bool IsPublished, bool IsClosed, bool AllowLate, bool HasAttachment)[] plan =
        [
            ("physics-open", "physics-3sec-intensive", "ahmed",
                "واجب الوحدة الثانية - الكهربية",
                "حل المسائل من 1 إلى 12 في الكتاب المدرسي، وارفع صورة واضحة للحل.",
                4, 10m, true, false, true, false),

            ("physics-overdue", "physics-3sec-intensive", "ahmed",
                "واجب الوحدة الأولى - الميكانيكا",
                "حل تدريبات الفصل الأول مع توضيح الخطوات.",
                -6, 10m, true, false, true, false),

            ("physics-draft", "physics-3sec-intensive", "ahmed",
                "واجب الفيزياء الحديثة (مسودة)",
                "قيد التحضير.",
                10, 10m, false, false, true, false),

            ("math-closed", "math-3sec-calculus", "mariam",
                "واجب التفاضل - قواعد الاشتقاق",
                "حل جميع تدريبات قواعد الاشتقاق.",
                -14, 20m, true, true, false, false),

            ("center-math-attachment", "center-math-3sec", "hossam",
                "ورقة عمل التكامل بالتعويض",
                "حمّل ورقة العمل المرفقة وحل التمارين الخمسة الأولى.",
                3, 15m, true, false, true, true)
        ];

        foreach (var entry in plan)
        {
            var group = _groups[entry.GroupKey];
            var teacher = _teachers[entry.TeacherKey];
            var homeworkId = SeedId.For("demo:homework", entry.Key);
            var assignedAt = _utcNow.AddDays(entry.DueInDays - 7);

            var homework = new Homework
            {
                Id = homeworkId,
                TenantId = group.TenantId,
                TeacherId = teacher.Id,
                GroupId = group.Id,
                SubjectId = group.SubjectId,
                Title = entry.Title,
                Description = entry.Title,
                Instructions = entry.Instructions,
                AssignedAt = assignedAt,
                DueAt = _utcNow.AddDays(entry.DueInDays),
                MaximumScore = entry.MaximumScore,
                AllowLateSubmission = entry.AllowLate,
                AllowResubmission = entry.Key != "math-closed",
                MaxSubmissions = entry.Key == "physics-open" ? 3 : null,
                IsPublished = entry.IsPublished,
                PublishedAt = entry.IsPublished ? assignedAt : null,
                ClosedAt = entry.IsClosed ? _utcNow.AddDays(-10) : null,
                CreatedAt = assignedAt
            };

            assignments.Add(homework);

            if (entry.HasAttachment)
            {
                attachments.Add(new HomeworkAttachment
                {
                    Id = SeedId.For("demo:homework-attachment", homeworkId, 1),
                    TenantId = group.TenantId,
                    HomeworkId = homeworkId,
                    FileName = entry.Key + "-worksheet.pdf",
                    FileUrl = DemoMedia.SubjectDocument(SubjectKey(group.SubjectId), 2),
                    FileType = MaterialFileType.Pdf,
                    FileSizeInBytes = 310_000,
                    CreatedAt = assignedAt
                });
            }
        }

        await AddMissingAsync("Homeworks", assignments, cancellationToken);
        await AddMissingAsync("HomeworkAttachments", attachments, cancellationToken);
        await SeedSubmissionsAsync(cancellationToken);
    }

    private async Task SeedSubmissionsAsync(CancellationToken cancellationToken)
    {
        var homeworkIds = new[]
            {
                "physics-open", "physics-overdue", "math-closed", "center-math-attachment"
            }
            .Select(key => SeedId.For("demo:homework", key))
            .ToList();

        var assignments = await context.Homeworks
            .AsNoTracking()
            .Where(homework => homeworkIds.Contains(homework.Id))
            .ToDictionaryAsync(homework => homework.Id, cancellationToken);

        // homework key, student key, outcome
        (string HomeworkKey, string StudentKey, string Outcome)[] plan =
        [
            // Open assignment: one handed in on time, one started and not finished, one blank.
            ("physics-open", "salma", "submitted"),
            ("physics-open", "youssef", "not-submitted"),
            ("physics-open", "zeyad", "submitted"),

            // Overdue but late work accepted: one late and graded, one late and unmarked,
            // one that never arrived at all.
            ("physics-overdue", "salma", "late-graded"),
            ("physics-overdue", "zeyad", "late"),
            ("physics-overdue", "omnia", "not-submitted"),

            // Closed assignment, everything marked.
            ("math-closed", "salma", "graded"),
            ("math-closed", "zeyad", "graded"),

            // Sent back for revision: the student owes another attempt.
            ("center-math-attachment", "omnia", "returned"),
            ("center-math-attachment", "hassan", "submitted")
        ];

        var submissions = new List<HomeworkSubmission>();

        foreach (var (homeworkKey, studentKey, outcome) in plan)
        {
            var homeworkId = SeedId.For("demo:homework", homeworkKey);

            if (!assignments.TryGetValue(homeworkId, out var homework))
            {
                continue;
            }

            var student = _students[studentKey];
            var teacherUserId = _teachers.Values.First(teacher => teacher.Id == homework.TeacherId).UserId;

            var submission = new HomeworkSubmission
            {
                Id = SeedId.For("demo:homework-submission", homeworkId, student.Id),
                TenantId = homework.TenantId,
                HomeworkId = homeworkId,
                StudentId = student.Id,
                Status = HomeworkSubmissionStatus.NotSubmitted,
                MaximumScore = homework.MaximumScore,
                CreatedAt = homework.AssignedAt.AddDays(1)
            };

            // On-time submissions land before the deadline, late ones after it, and nothing
            // is dated in the future: a deadline four days away still has to be answered by a
            // submission that already happened. Submit decides the status from these dates, so
            // the stored status can never disagree with the timestamps beside it.
            var submittedAt = outcome.StartsWith("late", StringComparison.Ordinal)
                ? homework.DueAt.AddDays(2)
                : Earlier(homework.DueAt.AddHours(-12), _utcNow.AddHours(-6));

            if (outcome != "not-submitted")
            {
                submission.Submit(
                    homework,
                    submittedAt,
                    content: "تم حل الواجب بالكامل. الحل مرفق في الملف.",
                    // A student's own upload, which no demo run has actually produced. It
                    // points at the homework's subject handout so the "open the submitted
                    // file" action loads a real PDF rather than a dead path.
                    fileUrl: DemoMedia.SubjectDocument(SubjectKey(homework.SubjectId), 1),
                    filePublicId: null);
            }

            switch (outcome)
            {
                case "graded":
                    submission.Grade(
                        Math.Round(homework.MaximumScore * 0.85m, 2),
                        homework.MaximumScore,
                        "حل منظم وواضح. راجع الخطوة الثالثة في المسألة الخامسة.",
                        teacherUserId,
                        submittedAt.AddDays(2));
                    break;

                case "late-graded":
                    submission.Grade(
                        Math.Round(homework.MaximumScore * 0.6m, 2),
                        homework.MaximumScore,
                        "الحل صحيح لكن التسليم متأخر، لذلك خُصمت درجات.",
                        teacherUserId,
                        submittedAt.AddDays(2));
                    break;

                case "returned":
                    submission.ReturnForRevision(
                        "الحل غير مكتمل: التمارين من 3 إلى 5 ناقصة. أعد التسليم.",
                        teacherUserId,
                        submittedAt.AddHours(6));
                    break;
            }

            submissions.Add(submission);
        }

        await AddMissingAsync("HomeworkSubmissions", submissions, cancellationToken);
    }

    private static DateTime Earlier(DateTime first, DateTime second) => first <= second ? first : second;
}
