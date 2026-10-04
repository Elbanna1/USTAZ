using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    /// <summary>
    /// Reviews, saved items, leads, announcements and the activity feed.
    ///
    /// Reviews are the part that needs care. One review per student per target is a unique
    /// index, not a convention, so the fixtures respect it; every reviewer is someone who was
    /// actually enrolled, because an eligibility check that demo data walks around is an
    /// eligibility check nobody will trust; and the rating snapshots on teachers, groups and
    /// courses are recomputed from the approved rows rather than written by hand.
    ///
    /// These are invented opinions from invented students. They are never presented as real
    /// customer feedback, and this mode never reaches a production database.
    /// </summary>
    private async Task SeedEngagementAsync(CancellationToken cancellationToken)
    {
        await SeedReviewsAsync(cancellationToken);
        await SeedFavoritesAsync(cancellationToken);
        await SeedInquiriesAsync(cancellationToken);
        await SeedAnnouncementsAsync(cancellationToken);
        await SeedActivityFeedAsync(cancellationToken);
    }

    private async Task SeedReviewsAsync(CancellationToken cancellationToken)
    {
        // student, teacher, optional group, optional course, rating, approved
        (string StudentKey, string TeacherKey, string? GroupKey, string? CourseKey, int Rating, bool IsApproved, string Comment)[] plan =
        [
            ("salma", "ahmed", "physics-3sec-intensive", null, 5, true,
                "شرح ممتاز وبسيط جداً، استفدت كثيراً من المجموعة والمراجعات قبل الامتحان."),
            ("youssef", "ahmed", "physics-3sec-intensive", null, 4, true,
                "الأستاذ متابع للطلاب أول بأول والواجبات منظمة."),
            ("zeyad", "ahmed", "physics-3sec-intensive", null, 5, true,
                "المستوى تحسن بشكل واضح بعد شهرين من الاشتراك."),
            // Waiting on moderation: the workspace has auto-approval switched off.
            ("omnia", "hossam", "center-math-3sec", null, 3, false,
                "المجموعة كبيرة شوية والمتابعة الفردية محدودة."),
            ("hassan", "hossam", "center-math-3sec", null, 5, true,
                "النظام منظم جداً والامتحانات الأسبوعية مفيدة."),
            ("salma", "ahmed", null, "physics-modern", 5, true,
                "الكورس المسجل مكمل للمجموعة، أقدر أرجع لأي درس في أي وقت."),
            ("habiba", "omar", null, "chemistry-organic", 4, true,
                "الملخصات والخرائط الذهنية وفرت عليّ وقت كبير."),
            ("habiba", "omar", "chemistry-2sec", null, 4, true,
                "التجارب المصورة قربت لي المنهج كثيراً."),
            // Teacher-level review with no group or course: the third shape of the unique index.
            ("farida", "mariam", null, null, 5, true,
                "أفضل مدرسة رياضيات تعاملت معها. الشرح أونلاين واضح ومنظم.")
        ];

        var reviews = new List<Review>();
        var index = 0;

        foreach (var (studentKey, teacherKey, groupKey, courseKey, rating, isApproved, comment) in plan)
        {
            index++;
            var student = _students[studentKey];
            var teacher = _teachers[teacherKey];
            var groupId = groupKey is null ? (Guid?)null : _groups[groupKey].Id;
            var courseId = courseKey is null ? (Guid?)null : _courses[courseKey].Id;

            var review = new Review
            {
                Id = SeedId.For("demo:review", student.Id, groupId ?? courseId ?? teacher.Id),
                StudentId = student.Id,
                TeacherId = teacher.Id,
                GroupId = groupId,
                CourseId = courseId,
                Rating = rating,
                Comment = comment,
                IsApproved = isApproved,
                ApprovedAt = isApproved ? _utcNow.AddDays(-20 + index) : null,
                ModerationNote = isApproved ? null : "في انتظار مراجعة إدارة المساحة.",
                CreatedAt = _utcNow.AddDays(-21 + index)
            };

            review.EnsureRatingIsValid();
            reviews.Add(review);
        }

        await AddMissingAsync("Reviews", reviews, cancellationToken);
        await ReconcileRatingSnapshotsAsync(cancellationToken);
    }

    /// <summary>
    /// Recomputes the stored averages on teachers, groups and courses from the approved
    /// reviews. Stored snapshots exist so a listing can sort by rating in SQL; the only way
    /// they stay trustworthy is to derive them, which is what the product does on every
    /// approval and what this does here.
    /// </summary>
    private async Task ReconcileRatingSnapshotsAsync(CancellationToken cancellationToken)
    {
        var approved = await context.Reviews
            .AsNoTracking()
            .Where(review => review.IsApproved)
            .Select(review => new { review.TeacherId, review.GroupId, review.CourseId, review.Rating })
            .ToListAsync(cancellationToken);

        var updated = 0;

        var teacherStats = approved
            .GroupBy(review => review.TeacherId)
            .ToDictionary(
                grouping => grouping.Key,
                grouping => (Average: (decimal)grouping.Average(review => review.Rating), Count: grouping.Count()));

        var teacherIds = teacherStats.Keys.ToList();

        foreach (var teacher in await context.TeacherProfiles
                     .Where(profile => teacherIds.Contains(profile.Id))
                     .ToListAsync(cancellationToken))
        {
            var (average, count) = teacherStats[teacher.Id];
            var rounded = Math.Round(average, 2, MidpointRounding.AwayFromZero);

            if (teacher.Rating == rounded && teacher.ReviewCount == count)
            {
                continue;
            }

            teacher.ApplyRatingSnapshot(average, count, _utcNow);
            updated++;
        }

        var groupStats = approved
            .Where(review => review.GroupId.HasValue)
            .GroupBy(review => review.GroupId!.Value)
            .ToDictionary(
                grouping => grouping.Key,
                grouping => (Average: (decimal)grouping.Average(review => review.Rating), Count: grouping.Count()));

        var groupIds = groupStats.Keys.ToList();

        foreach (var group in await context.Groups
                     .Where(entity => groupIds.Contains(entity.Id))
                     .ToListAsync(cancellationToken))
        {
            var (average, count) = groupStats[group.Id];
            var rounded = Math.Round(average, 2, MidpointRounding.AwayFromZero);

            if (group.Rating == rounded && group.ReviewCount == count)
            {
                continue;
            }

            group.ApplyRatingSnapshot(average, count, _utcNow);
            updated++;
        }

        var courseStats = approved
            .Where(review => review.CourseId.HasValue)
            .GroupBy(review => review.CourseId!.Value)
            .ToDictionary(
                grouping => grouping.Key,
                grouping => (Average: (decimal)grouping.Average(review => review.Rating), Count: grouping.Count()));

        var courseIds = courseStats.Keys.ToList();

        foreach (var course in await context.Courses
                     .Where(entity => courseIds.Contains(entity.Id))
                     .ToListAsync(cancellationToken))
        {
            var (average, count) = courseStats[course.Id];
            var rounded = Math.Round(average, 2, MidpointRounding.AwayFromZero);

            if (course.Rating == rounded && course.ReviewCount == count)
            {
                continue;
            }

            course.ApplyRatingSnapshot(average, count, _utcNow);
            updated++;
        }

        // The teacher's student count is a headline figure on every profile card; derive it
        // from the enrollments rather than leaving it at zero.
        var studentCounts = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Status == EnrollmentStatus.Active)
            .GroupBy(enrollment => enrollment.Group.TeacherId)
            .Select(grouping => new
            {
                TeacherId = grouping.Key,
                Count = grouping.Select(enrollment => enrollment.StudentId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var countsByTeacher = studentCounts.ToDictionary(entry => entry.TeacherId, entry => entry.Count);
        var allTeacherIds = _teachers.Values.Select(teacher => teacher.Id).ToList();

        foreach (var teacher in await context.TeacherProfiles
                     .Where(profile => allTeacherIds.Contains(profile.Id))
                     .ToListAsync(cancellationToken))
        {
            var expected = countsByTeacher.TryGetValue(teacher.Id, out var count) ? count : 0;

            if (teacher.StudentsCount == expected)
            {
                continue;
            }

            teacher.StudentsCount = expected;
            teacher.UpdatedAt = _utcNow;
            updated++;
        }

        if (updated > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            report.Record("RatingSnapshots", 0, updated, note: "derived from approved reviews and enrollments");
        }
    }

    private async Task SeedFavoritesAsync(CancellationToken cancellationToken)
    {
        (string StudentKey, string TeacherKey)[] teacherFavorites =
        [
            ("salma", "ahmed"), ("salma", "mariam"),
            ("youssef", "ahmed"),
            ("habiba", "omar"),
            ("farida", "mariam"),
            ("omnia", "hossam")
        ];

        (string StudentKey, string CourseKey)[] courseFavorites =
        [
            ("salma", "physics-modern"), ("salma", "math-algebra-geometry"),
            ("zeyad", "physics-modern"),
            ("mohamed", "physics-foundations-free"),
            ("hassan", "center-math-revision")
        ];

        await AddMissingAsync(
            "TeacherFavorites",
            teacherFavorites
                .Select(entry =>
                {
                    var studentId = _students[entry.StudentKey].Id;
                    var teacherId = _teachers[entry.TeacherKey].Id;

                    return new TeacherFavorite
                    {
                        Id = SeedId.For("demo:teacher-favorite", studentId, teacherId),
                        StudentId = studentId,
                        TeacherId = teacherId,
                        CreatedAt = _utcNow.AddDays(-30)
                    };
                })
                .ToList(),
            cancellationToken);

        await AddMissingAsync(
            "CourseFavorites",
            courseFavorites
                .Select(entry =>
                {
                    var studentId = _students[entry.StudentKey].Id;
                    var courseId = _courses[entry.CourseKey].Id;

                    return new CourseFavorite
                    {
                        Id = SeedId.For("demo:course-favorite", studentId, courseId),
                        StudentId = studentId,
                        CourseId = courseId,
                        CreatedAt = _utcNow.AddDays(-25)
                    };
                })
                .ToList(),
            cancellationToken);
    }

    /// <summary>
    /// Leads sent to teachers, in every status the workflow has. One comes from a guest with
    /// no account, which is the case that makes <see cref="Inquiry.StudentId"/> nullable.
    /// </summary>
    private async Task SeedInquiriesAsync(CancellationToken cancellationToken)
    {
        (string Key, string TeacherKey, string? StudentKey, string? GroupKey, InquiryStatus Status, string Name, string Message)[] plan =
        [
            ("new-salma", "ahmed", "salma", "physics-3sec-intensive", InquiryStatus.New,
                "سلمى إبراهيم", "هل يوجد مكان في مجموعة السبت؟ وما هي مواعيد المراجعة النهائية؟"),
            ("contacted-guest", "ahmed", null, "physics-1sec-basics", InquiryStatus.Contacted,
                "أم محمد", "ابني في أولى ثانوي ومحتاج تأسيس في الفيزياء. ما هي الأسعار؟"),
            ("converted-kareem", "ahmed", "kareem", "physics-3sec-intensive", InquiryStatus.Converted,
                "كريم عادل", "أريد الانضمام للمجموعة المكثفة."),
            ("closed-guest", "mariam", null, null, InquiryStatus.Closed,
                "زائر", "هل تقدمون دروس في الإحصاء؟"),
            ("new-center", "hossam", "malak", "center-math-3sec", InquiryStatus.New,
                "ملك وائل", "هل المجموعة متاحة أونلاين أم حضوري فقط؟")
        ];

        var inquiries = new List<Inquiry>();
        var index = 0;

        foreach (var (key, teacherKey, studentKey, groupKey, status, name, message) in plan)
        {
            index++;
            var teacher = _teachers[teacherKey];

            var inquiry = new Inquiry
            {
                Id = SeedId.For("demo:inquiry", key),
                StudentId = studentKey is null ? null : _students[studentKey].Id,
                TenantId = teacher.TenantId,
                TeacherId = teacher.Id,
                GroupId = groupKey is null ? null : _groups[groupKey].Id,
                Name = name,
                PhoneNumber = "+2015000000" + index.ToString("D2"),
                Email = studentKey is null ? null : _users[_students[studentKey].UserId].Email,
                Message = message,
                Status = InquiryStatus.New,
                CreatedAt = _utcNow.AddDays(-12 + index)
            };

            if (status != InquiryStatus.New)
            {
                inquiry.ChangeStatus(
                    status,
                    _utcNow.AddDays(-10 + index),
                    status switch
                    {
                        InquiryStatus.Contacted => "تم التواصل هاتفياً وإرسال الأسعار.",
                        InquiryStatus.Converted => "انضم للمجموعة بالفعل.",
                        _ => "غير متاح حالياً في هذه المادة."
                    });
            }

            inquiries.Add(inquiry);
        }

        await AddMissingAsync("Inquiries", inquiries, cancellationToken);
    }

    private async Task SeedAnnouncementsAsync(CancellationToken cancellationToken)
    {
        var ahmed = _tenants["ahmed"];
        var centre = _tenants["center"];

        var announcements = new List<Announcement>
        {
            new()
            {
                Id = SeedId.For("demo:announcement", "ahmed-pinned"),
                TenantId = ahmed.Id,
                CreatedByUserId = ahmed.OwnerUserId,
                Title = "مواعيد المراجعة النهائية",
                Body = "تبدأ المراجعات النهائية الأسبوع القادم. البرنامج الكامل مرفق مع كل مجموعة.",
                ImageUrl = DemoMedia.AnnouncementImage("ahmed-pinned"),
                Audience = AnnouncementAudience.TenantWide,
                IsPinned = true,
                IsPublished = true,
                PublishedAt = _utcNow.AddDays(-4),
                CreatedAt = _utcNow.AddDays(-4)
            },
            new()
            {
                Id = SeedId.For("demo:announcement", "ahmed-group"),
                TenantId = ahmed.Id,
                CreatedByUserId = ahmed.OwnerUserId,
                GroupId = _groups["physics-3sec-intensive"].Id,
                Title = "تغيير موعد حصة الثلاثاء",
                Body = "حصة الثلاثاء القادم ستكون الساعة 5 مساءً بدلاً من 4.",
                Audience = AnnouncementAudience.Group,
                IsPublished = true,
                PublishedAt = _utcNow.AddDays(-2),
                CreatedAt = _utcNow.AddDays(-2)
            },
            new()
            {
                Id = SeedId.For("demo:announcement", "ahmed-parents"),
                TenantId = ahmed.Id,
                CreatedByUserId = ahmed.OwnerUserId,
                Title = "اجتماع أولياء الأمور",
                Body = "اجتماع لمناقشة مستويات الطلاب يوم الجمعة الساعة 11 صباحاً.",
                ImageUrl = DemoMedia.AnnouncementImage("ahmed-parents"),
                Audience = AnnouncementAudience.Parents,
                IsPublished = true,
                PublishedAt = _utcNow.AddDays(-6),
                CreatedAt = _utcNow.AddDays(-6)
            },
            // Expired, so the visibility window has something to hide.
            new()
            {
                Id = SeedId.For("demo:announcement", "ahmed-expired"),
                TenantId = ahmed.Id,
                CreatedByUserId = ahmed.OwnerUserId,
                Title = "إعلان قديم عن امتحان منتصف الترم",
                Body = "امتحان منتصف الترم يوم الأحد. انتهت صلاحية هذا الإعلان.",
                Audience = AnnouncementAudience.Students,
                IsPublished = true,
                PublishedAt = _utcNow.AddDays(-30),
                ExpiresAt = _utcNow.AddDays(-15),
                CreatedAt = _utcNow.AddDays(-30)
            },
            // Draft.
            new()
            {
                Id = SeedId.For("demo:announcement", "center-draft"),
                TenantId = centre.Id,
                CreatedByUserId = centre.OwnerUserId,
                Title = "خطة العام الدراسي القادم (مسودة)",
                Body = "قيد الإعداد.",
                Audience = AnnouncementAudience.TenantWide,
                IsPublished = false,
                CreatedAt = _utcNow.AddDays(-1)
            },
            new()
            {
                Id = SeedId.For("demo:announcement", "center-pinned"),
                TenantId = centre.Id,
                CreatedByUserId = centre.OwnerUserId,
                Title = "فتح التسجيل للعام الدراسي الجديد",
                Body = "التسجيل مفتوح لكل المجموعات. الأماكن محدودة.",
                ImageUrl = DemoMedia.AnnouncementImage("center-pinned"),
                Audience = AnnouncementAudience.TenantWide,
                IsPinned = true,
                IsPublished = true,
                PublishedAt = _utcNow.AddDays(-8),
                CreatedAt = _utcNow.AddDays(-8)
            }
        };

        await AddMissingAsync("Announcements", announcements, cancellationToken);
    }

    /// <summary>
    /// The activity timeline.
    ///
    /// Entries are written from things that actually happened in the seeded data — a homework
    /// handed in, an exam finished, a certificate issued — rather than invented, so the feed
    /// and the records it describes agree. Each carries a deduplication key, which is the
    /// unique index that makes recording idempotent; metadata holds display values only.
    /// </summary>
    private async Task SeedActivityFeedAsync(CancellationToken cancellationToken)
    {
        var events = new List<ActivityEvent>();

        var submissions = await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(submission => submission.SubmissionCount > 0)
            .Select(submission => new
            {
                submission.Id,
                submission.TenantId,
                submission.StudentId,
                submission.SubmittedAt,
                submission.Status,
                StudentName = submission.Student.User.DisplayName,
                Title = submission.Homework.Title
            })
            .ToListAsync(cancellationToken);

        foreach (var submission in submissions)
        {
            events.Add(new ActivityEvent
            {
                Id = SeedId.For("demo:activity", "homework-submitted:" + submission.Id.ToString("N")),
                TenantId = submission.TenantId,
                Type = ActivityTypes.HomeworkSubmitted,
                ActorUserId = null,
                ActorName = submission.StudentName,
                StudentId = submission.StudentId,
                RelatedEntityType = "HomeworkSubmission",
                RelatedEntityId = submission.Id,
                Metadata = "{\"title\":\"" + Escape(submission.Title) + "\"}",
                Visibility = ActivityVisibility.Shared,
                OccurredAt = submission.SubmittedAt ?? _utcNow,
                DeduplicationKey = "homework.submitted:" + submission.Id.ToString("N"),
                CreatedAt = submission.SubmittedAt ?? _utcNow
            });
        }

        var attempts = await context.ExamAttempts
            .AsNoTracking()
            .Where(attempt => attempt.Status == ExamAttemptStatus.Graded)
            .Select(attempt => new
            {
                attempt.Id,
                attempt.TenantId,
                attempt.StudentId,
                attempt.SubmittedAt,
                attempt.Score,
                attempt.MaximumScore,
                StudentName = attempt.Student.User.DisplayName,
                Title = attempt.Exam.Title
            })
            .ToListAsync(cancellationToken);

        foreach (var attempt in attempts)
        {
            events.Add(new ActivityEvent
            {
                Id = SeedId.For("demo:activity", "exam-completed:" + attempt.Id.ToString("N")),
                TenantId = attempt.TenantId,
                Type = ActivityTypes.ExamCompleted,
                ActorName = attempt.StudentName,
                StudentId = attempt.StudentId,
                RelatedEntityType = "ExamAttempt",
                RelatedEntityId = attempt.Id,
                Metadata = "{\"title\":\"" + Escape(attempt.Title) + "\",\"score\":" + attempt.Score
                           + ",\"max\":" + attempt.MaximumScore + "}",
                Visibility = ActivityVisibility.Shared,
                OccurredAt = attempt.SubmittedAt ?? _utcNow,
                DeduplicationKey = "exam.completed:" + attempt.Id.ToString("N"),
                CreatedAt = attempt.SubmittedAt ?? _utcNow
            });
        }

        var certificates = await context.Certificates
            .AsNoTracking()
            .Select(certificate => new
            {
                certificate.Id,
                certificate.TenantId,
                certificate.StudentId,
                certificate.StudentName,
                certificate.CourseTitle,
                certificate.IssuedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var certificate in certificates)
        {
            events.Add(new ActivityEvent
            {
                Id = SeedId.For("demo:activity", "certificate:" + certificate.Id.ToString("N")),
                TenantId = certificate.TenantId,
                Type = ActivityTypes.CertificateIssued,
                ActorName = certificate.StudentName,
                StudentId = certificate.StudentId,
                RelatedEntityType = "Certificate",
                RelatedEntityId = certificate.Id,
                Metadata = "{\"course\":\"" + Escape(certificate.CourseTitle) + "\"}",
                Visibility = ActivityVisibility.Shared,
                OccurredAt = certificate.IssuedAt,
                DeduplicationKey = "certificate.issued:" + certificate.Id.ToString("N"),
                CreatedAt = certificate.IssuedAt
            });
        }

        // Workspace-only entries: commercial events a student has no business seeing in a feed.
        foreach (var (teacherKey, teacher) in _teachers.Where(entry =>
                     entry.Key is "ahmed" or "mariam" or "hossam"))
        {
            events.Add(new ActivityEvent
            {
                Id = SeedId.For("demo:activity", "teacher-verified:" + teacherKey),
                TenantId = teacher.TenantId,
                Type = ActivityTypes.TeacherVerified,
                ActorUserId = teacher.UserId,
                ActorName = _users[teacher.UserId].DisplayName,
                RelatedEntityType = "TeacherProfile",
                RelatedEntityId = teacher.Id,
                Visibility = ActivityVisibility.Workspace,
                OccurredAt = teacher.VerifiedAt ?? _utcNow.AddMonths(-7),
                DeduplicationKey = "teacher.verified:" + teacher.Id.ToString("N"),
                CreatedAt = teacher.VerifiedAt ?? _utcNow.AddMonths(-7)
            });
        }

        await AddMissingAsync("ActivityEvents", events, cancellationToken);

        static string Escape(string value) => value.Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
