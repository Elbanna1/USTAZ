using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    /// <summary>
    /// Who is in which group and which course, and how far through.
    ///
    /// Two invariants are honoured rather than asserted. A group's
    /// <see cref="Group.EnrolledStudentsCount"/> is recomputed from the enrollments that
    /// actually occupy a seat, so the counter and the rows agree; and a course enrollment's
    /// percentage is derived from the lesson-progress rows with the same formula the
    /// application uses, so no enrollment claims progress its lessons do not support.
    /// </summary>
    private async Task SeedEnrollmentsAsync(CancellationToken cancellationToken)
    {
        await SeedGroupEnrollmentsAsync(cancellationToken);
        await SeedCourseEnrollmentsAsync(cancellationToken);
        await SeedTenantStudentsAsync(cancellationToken);
        await SeedLessonProgressAsync(cancellationToken);
    }

    /// <summary>Student key, group key, status. Order is the enrollment order.</summary>
    private static readonly (string StudentKey, string GroupKey, EnrollmentStatus Status)[] GroupEnrollmentPlan =
    [
        ("salma", "physics-3sec-intensive", EnrollmentStatus.Active),
        ("youssef", "physics-3sec-intensive", EnrollmentStatus.Active),
        ("zeyad", "physics-3sec-intensive", EnrollmentStatus.Active),
        ("omnia", "physics-3sec-intensive", EnrollmentStatus.Active),
        // Requested a place and still waiting: occupies a seat, has no access yet.
        ("kareem", "physics-3sec-intensive", EnrollmentStatus.Pending),

        ("farida", "physics-1sec-basics", EnrollmentStatus.Active),
        ("mohamed", "physics-1sec-basics", EnrollmentStatus.Active),

        ("habiba", "chemistry-2sec", EnrollmentStatus.Active),
        // Left the group: frees the seat, keeps the history.
        ("aya", "chemistry-2sec", EnrollmentStatus.Cancelled),

        ("salma", "math-3sec-calculus", EnrollmentStatus.Active),
        ("zeyad", "math-3sec-calculus", EnrollmentStatus.Active),
        ("youssef", "math-3sec-calculus", EnrollmentStatus.Completed),

        ("omnia", "center-math-3sec", EnrollmentStatus.Active),
        ("hassan", "center-math-3sec", EnrollmentStatus.Active),
        ("malak", "center-math-3sec", EnrollmentStatus.Active),

        // Exactly fills the six seats of center-biology-2sec.
        ("habiba", "center-biology-2sec", EnrollmentStatus.Active),
        ("aya", "center-biology-2sec", EnrollmentStatus.Active),
        ("hassan", "center-biology-2sec", EnrollmentStatus.Active),
        ("farida", "center-biology-2sec", EnrollmentStatus.Active),
        ("mohamed", "center-biology-2sec", EnrollmentStatus.Active),
        ("malak", "center-biology-2sec", EnrollmentStatus.Active)
    ];

    private async Task SeedGroupEnrollmentsAsync(CancellationToken cancellationToken)
    {
        var enrollments = new List<GroupEnrollment>();
        var index = 0;

        foreach (var (studentKey, groupKey, status) in GroupEnrollmentPlan)
        {
            index++;
            var student = _students[studentKey];
            var group = _groups[groupKey];
            var enrolledAt = _utcNow.AddDays(-60 + index);

            // One enrollment redeems a discount code, so the discounted-price path is stored
            // rather than only computed.
            var usesDiscount = studentKey == "salma" && groupKey == "physics-3sec-intensive";
            var discountAmount = usesDiscount ? Math.Round(group.MonthlyPrice * 0.10m, 2) : 0m;

            enrollments.Add(new GroupEnrollment
            {
                Id = SeedId.For("demo:group-enrollment", group.Id, student.Id),
                StudentId = student.Id,
                GroupId = group.Id,
                Status = status,
                EnrolledAt = enrolledAt,
                CancelledAt = status == EnrollmentStatus.Cancelled ? _utcNow.AddDays(-14) : null,
                CancellationReason = status == EnrollmentStatus.Cancelled
                    ? "تغيير المواعيد الدراسية"
                    : null,
                CompletedAt = status == EnrollmentStatus.Completed ? _utcNow.AddDays(-7) : null,
                PricePaid = group.MonthlyPrice,
                DiscountAmount = discountAmount,
                FinalPrice = group.MonthlyPrice - discountAmount,
                DiscountId = usesDiscount ? SeedId.For("demo:discount", "ahmed:WELCOME10") : null,
                DiscountCode = usesDiscount ? "WELCOME10" : null,
                CreatedAt = enrolledAt
            });
        }

        await AddMissingAsync("GroupEnrollments", enrollments, cancellationToken);

        var ids = enrollments.Select(enrollment => enrollment.Id).ToList();

        var persisted = await context.GroupEnrollments
            .Where(enrollment => ids.Contains(enrollment.Id))
            .ToListAsync(cancellationToken);

        foreach (var (studentKey, groupKey, _) in GroupEnrollmentPlan)
        {
            var id = SeedId.For("demo:group-enrollment", _groups[groupKey].Id, _students[studentKey].Id);
            _groupEnrollments[groupKey + "/" + studentKey] = persisted.First(enrollment => enrollment.Id == id);
        }

        await ReconcileGroupSeatCountsAsync(cancellationToken);
    }

    /// <summary>
    /// Brings every demo group's seat counter in line with its enrollments. Recomputed rather
    /// than incremented, so re-running the seeder cannot inflate the count.
    /// </summary>
    private async Task ReconcileGroupSeatCountsAsync(CancellationToken cancellationToken)
    {
        var groupIds = _groups.Values.Select(group => group.Id).ToList();

        var occupied = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => groupIds.Contains(enrollment.GroupId)
                && (enrollment.Status == EnrollmentStatus.Pending || enrollment.Status == EnrollmentStatus.Active))
            .GroupBy(enrollment => enrollment.GroupId)
            .Select(grouping => new { GroupId = grouping.Key, Count = grouping.Count() })
            .ToListAsync(cancellationToken);

        var counts = occupied.ToDictionary(entry => entry.GroupId, entry => entry.Count);

        var tracked = await context.Groups
            .Where(group => groupIds.Contains(group.Id))
            .ToListAsync(cancellationToken);

        var updated = 0;

        foreach (var group in tracked)
        {
            var expected = counts.TryGetValue(group.Id, out var count) ? count : 0;

            if (group.EnrolledStudentsCount == expected)
            {
                continue;
            }

            group.EnrolledStudentsCount = expected;
            group.UpdatedAt = _utcNow;
            updated++;
        }

        if (updated > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            report.Record("Groups", 0, updated, note: "seat counts reconciled");
        }
    }

    /// <summary>
    /// Student key, course key, how many of the nine lessons they have finished, and status.
    /// The two hundred-percent rows are what make the certificate module possible.
    /// </summary>
    private static readonly (string StudentKey, string CourseKey, int CompletedLessons, EnrollmentStatus Status)[]
        CourseEnrollmentPlan =
        [
            ("salma", "physics-modern", 4, EnrollmentStatus.Active),
            ("youssef", "physics-modern", 9, EnrollmentStatus.Completed),
            ("zeyad", "physics-modern", 1, EnrollmentStatus.Active),
            ("habiba", "chemistry-organic", 6, EnrollmentStatus.Active),
            // Enrolled, never started: the zero-progress empty state.
            ("mohamed", "physics-foundations-free", 0, EnrollmentStatus.Active),
            ("malak", "physics-foundations-free", 5, EnrollmentStatus.Active),
            ("farida", "math-algebra-geometry", 2, EnrollmentStatus.Active),
            ("aya", "math-algebra-geometry", 1, EnrollmentStatus.Cancelled),
            ("omnia", "center-math-revision", 9, EnrollmentStatus.Completed),
            ("hassan", "center-math-revision", 3, EnrollmentStatus.Active)
        ];

    private async Task SeedCourseEnrollmentsAsync(CancellationToken cancellationToken)
    {
        var enrollments = new List<CourseEnrollment>();
        var index = 0;

        foreach (var (studentKey, courseKey, completedLessons, status) in CourseEnrollmentPlan)
        {
            index++;
            var student = _students[studentKey];
            var course = _courses[courseKey];
            var enrolledAt = _utcNow.AddDays(-45 + index);

            enrollments.Add(new CourseEnrollment
            {
                Id = SeedId.For("demo:course-enrollment", course.Id, student.Id),
                StudentId = student.Id,
                CourseId = course.Id,
                Status = status,
                EnrolledAt = enrolledAt,
                CancelledAt = status == EnrollmentStatus.Cancelled ? _utcNow.AddDays(-10) : null,
                CancellationReason = status == EnrollmentStatus.Cancelled ? "طلب الطالب إلغاء الاشتراك" : null,
                CompletedAt = status == EnrollmentStatus.Completed ? _utcNow.AddDays(-5) : null,
                PricePaid = course.Price,
                DiscountAmount = 0m,
                FinalPrice = course.Price,
                // Set properly by SeedLessonProgressAsync from the rows it writes.
                ProgressPercentage = 0,
                LastAccessedAt = completedLessons == 0 ? null : _utcNow.AddDays(-index),
                CreatedAt = enrolledAt
            });
        }

        await AddMissingAsync("CourseEnrollments", enrollments, cancellationToken);

        var ids = enrollments.Select(enrollment => enrollment.Id).ToList();

        var persisted = await context.CourseEnrollments
            .Where(enrollment => ids.Contains(enrollment.Id))
            .ToListAsync(cancellationToken);

        foreach (var (studentKey, courseKey, _, _) in CourseEnrollmentPlan)
        {
            var id = SeedId.For("demo:course-enrollment", _courses[courseKey].Id, _students[studentKey].Id);
            _courseEnrollments[courseKey + "/" + studentKey] = persisted.First(enrollment => enrollment.Id == id);
        }

        await ReconcileCourseStudentCountsAsync(cancellationToken);
    }

    private async Task ReconcileCourseStudentCountsAsync(CancellationToken cancellationToken)
    {
        var courseIds = _courses.Values.Select(course => course.Id).ToList();

        var enrolled = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => courseIds.Contains(enrollment.CourseId)
                && enrollment.Status != EnrollmentStatus.Cancelled)
            .GroupBy(enrollment => enrollment.CourseId)
            .Select(grouping => new { CourseId = grouping.Key, Count = grouping.Count() })
            .ToListAsync(cancellationToken);

        var counts = enrolled.ToDictionary(entry => entry.CourseId, entry => entry.Count);

        var tracked = await context.Courses
            .Where(course => courseIds.Contains(course.Id))
            .ToListAsync(cancellationToken);

        var updated = 0;

        foreach (var course in tracked)
        {
            var expected = counts.TryGetValue(course.Id, out var count) ? count : 0;

            if (course.StudentCount == expected)
            {
                continue;
            }

            course.StudentCount = expected;
            course.UpdatedAt = _utcNow;
            updated++;
        }

        if (updated > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            report.Record("Courses", 0, updated, note: "student counts reconciled");
        }
    }

    /// <summary>
    /// Writes one progress row per lesson a student has reached, then derives the enrollment
    /// percentage from them with the application's own formula. Lessons are completed in
    /// order, which is what a real watch history looks like.
    /// </summary>
    private async Task SeedLessonProgressAsync(CancellationToken cancellationToken)
    {
        var lessonsByCourse = await context.CourseLessons
            .AsNoTracking()
            .Where(lesson => lesson.IsPublished)
            .Select(lesson => new
            {
                lesson.Id,
                CourseId = lesson.Section.CourseId,
                SectionOrder = lesson.Section.SortOrder,
                lesson.SortOrder,
                lesson.DurationInMinutes
            })
            .ToListAsync(cancellationToken);

        var ordered = lessonsByCourse
            .GroupBy(lesson => lesson.CourseId)
            .ToDictionary(
                grouping => grouping.Key,
                grouping => grouping
                    .OrderBy(lesson => lesson.SectionOrder)
                    .ThenBy(lesson => lesson.SortOrder)
                    .ToList());

        var progressRows = new List<CourseLessonProgress>();

        foreach (var (studentKey, courseKey, completedLessons, _) in CourseEnrollmentPlan)
        {
            if (completedLessons == 0)
            {
                continue;
            }

            var enrollment = _courseEnrollments[courseKey + "/" + studentKey];

            if (!ordered.TryGetValue(enrollment.CourseId, out var lessons))
            {
                continue;
            }

            var day = 0;

            foreach (var lesson in lessons.Take(completedLessons))
            {
                day++;

                progressRows.Add(new CourseLessonProgress
                {
                    Id = SeedId.For("demo:lesson-progress", enrollment.Id, lesson.Id),
                    CourseEnrollmentId = enrollment.Id,
                    StudentId = enrollment.StudentId,
                    CourseId = enrollment.CourseId,
                    LessonId = lesson.Id,
                    IsCompleted = true,
                    CompletedAt = _utcNow.AddDays(-40 + day),
                    WatchedSeconds = lesson.DurationInMinutes * 60,
                    LastAccessedAt = _utcNow.AddDays(-40 + day),
                    CreatedAt = _utcNow.AddDays(-40 + day)
                });
            }

            // One lesson left part-watched, so "resume where you left off" has a real position.
            var nextLesson = lessons.Skip(completedLessons).FirstOrDefault();

            if (nextLesson is not null)
            {
                progressRows.Add(new CourseLessonProgress
                {
                    Id = SeedId.For("demo:lesson-progress", enrollment.Id, nextLesson.Id),
                    CourseEnrollmentId = enrollment.Id,
                    StudentId = enrollment.StudentId,
                    CourseId = enrollment.CourseId,
                    LessonId = nextLesson.Id,
                    IsCompleted = false,
                    WatchedSeconds = nextLesson.DurationInMinutes * 60 / 3,
                    LastAccessedAt = _utcNow.AddDays(-40 + day + 1),
                    CreatedAt = _utcNow.AddDays(-40 + day + 1)
                });
            }
        }

        await AddMissingAsync("CourseLessonProgress", progressRows, cancellationToken);
        await ReconcileCourseProgressAsync(cancellationToken);
    }

    private async Task ReconcileCourseProgressAsync(CancellationToken cancellationToken)
    {
        var enrollmentIds = _courseEnrollments.Values.Select(enrollment => enrollment.Id).ToList();

        var totals = await context.CourseLessons
            .AsNoTracking()
            .Where(lesson => lesson.IsPublished)
            .GroupBy(lesson => lesson.Section.CourseId)
            .Select(grouping => new { CourseId = grouping.Key, Total = grouping.Count() })
            .ToListAsync(cancellationToken);

        var totalByCourse = totals.ToDictionary(entry => entry.CourseId, entry => entry.Total);

        var completed = await context.CourseLessonProgress
            .AsNoTracking()
            .Where(progress => enrollmentIds.Contains(progress.CourseEnrollmentId)
                && progress.IsCompleted
                && progress.Lesson.IsPublished)
            .GroupBy(progress => progress.CourseEnrollmentId)
            .Select(grouping => new { EnrollmentId = grouping.Key, Count = grouping.Count() })
            .ToListAsync(cancellationToken);

        var completedByEnrollment = completed.ToDictionary(entry => entry.EnrollmentId, entry => entry.Count);

        var tracked = await context.CourseEnrollments
            .Where(enrollment => enrollmentIds.Contains(enrollment.Id))
            .ToListAsync(cancellationToken);

        var updated = 0;

        foreach (var enrollment in tracked)
        {
            var total = totalByCourse.TryGetValue(enrollment.CourseId, out var totalLessons) ? totalLessons : 0;
            var done = completedByEnrollment.TryGetValue(enrollment.Id, out var doneCount) ? doneCount : 0;

            // The same expression the application uses, so a seeded percentage is one the
            // running system would have produced.
            var percentage = total == 0
                ? 0
                : (int)Math.Round(done * 100d / total, MidpointRounding.AwayFromZero);

            if (enrollment.ProgressPercentage == percentage)
            {
                continue;
            }

            // A cancelled enrollment keeps the progress it had; UpdateProgress would promote it.
            if (enrollment.Status == EnrollmentStatus.Cancelled)
            {
                enrollment.ProgressPercentage = percentage;
                enrollment.UpdatedAt = _utcNow;
            }
            else
            {
                enrollment.UpdateProgress(percentage, _utcNow);
                enrollment.LastAccessedAt = done == 0 ? null : _utcNow.AddDays(-3);

                if (enrollment.Status == EnrollmentStatus.Completed)
                {
                    enrollment.CompletedAt ??= _utcNow.AddDays(-5);
                }
            }

            updated++;
        }

        if (updated > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            report.Record("CourseEnrollments", 0, updated, note: "progress derived from lesson rows");
        }
    }
}
