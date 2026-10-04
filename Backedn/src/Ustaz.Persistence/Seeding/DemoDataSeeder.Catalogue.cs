using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.ValueObjects;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    /// <summary>
    /// Groups and recorded courses.
    ///
    /// Publication state is the axis that matters. A public listing must show only published
    /// rows belonging to an active workspace whose teacher is verified, and the only way to
    /// know that filter is right is to seed rows it has to exclude: drafts, a group inside a
    /// workspace with nothing published, and content owned by an unverified teacher who could
    /// not have published it in the first place.
    /// </summary>
    private async Task SeedCatalogueAsync(CancellationToken cancellationToken)
    {
        await SeedGroupsAsync(cancellationToken);
        await SeedCoursesAsync(cancellationToken);
        await SeedDiscountsAsync(cancellationToken);
    }

    private sealed record GroupDefinition(
        string Key,
        string TeacherKey,
        Guid SubjectId,
        Guid GradeId,
        string Name,
        string Description,
        GroupType Type,
        TeachingMode Mode,
        string? Location,
        decimal MonthlyPrice,
        decimal? OriginalPrice,
        int DiscountPercentage,
        int Capacity,
        bool IsPublished,
        (DayOfWeek Day, int StartHour, int EndHour)[] Sessions);

    private GroupDefinition[] GroupDefinitions =>
    [
        new("physics-3sec-intensive", "ahmed", PhysicsId, ThirdSecondaryId,
            "فيزياء الثانوية العامة - مجموعة مكثفة",
            "مراجعة شاملة لمنهج الفيزياء للصف الثالث الثانوي مع حل امتحانات السنوات السابقة.",
            GroupType.Intensive, TeachingMode.Hybrid, "مدينة نصر - القاهرة",
            850m, 1000m, 15, 30, IsPublished: true,
            [(DayOfWeek.Saturday, 16, 18), (DayOfWeek.Tuesday, 16, 18)]),

        new("physics-1sec-basics", "ahmed", PhysicsId, FirstSecondaryId,
            "أساسيات الفيزياء - الصف الأول الثانوي",
            "تأسيس قوي في الميكانيكا والكهربية مع تطبيقات عملية أسبوعية.",
            GroupType.Regular, TeachingMode.Offline, "مدينة نصر - القاهرة",
            500m, null, 0, 25, IsPublished: true,
            [(DayOfWeek.Monday, 17, 19)]),

        // Draft inside an otherwise fully published workspace: the teacher's own list shows
        // it, the public listing must not.
        new("physics-2sec-draft", "ahmed", PhysicsId, SecondSecondaryId,
            "فيزياء الصف الثاني الثانوي - مسودة",
            "مجموعة قيد التحضير لم تُنشر بعد.",
            GroupType.Regular, TeachingMode.Offline, "مدينة نصر - القاهرة",
            600m, null, 0, 25, IsPublished: false,
            [(DayOfWeek.Wednesday, 15, 17)]),

        new("math-3sec-calculus", "mariam", MathematicsId, ThirdSecondaryId,
            "تفاضل وتكامل - الثانوية العامة",
            "شرح مفصل للتفاضل والتكامل مع بنك أسئلة متجدد كل أسبوع.",
            GroupType.Regular, TeachingMode.Online, null,
            700m, 800m, 12, 40, IsPublished: true,
            [(DayOfWeek.Sunday, 18, 20), (DayOfWeek.Wednesday, 18, 20)]),

        new("chemistry-2sec", "omar", ChemistryId, SecondSecondaryId,
            "كيمياء الصف الثاني الثانوي",
            "منهج الكيمياء كاملاً مع تجارب معملية مصورة وملخصات قابلة للتحميل.",
            GroupType.Regular, TeachingMode.Offline, "سموحة - الإسكندرية",
            600m, null, 0, 20, IsPublished: true,
            [(DayOfWeek.Thursday, 15, 17)]),

        // nour's workspace: everything stays unpublished, so the "workspace with no public
        // content" fixture has a real, empty public site.
        new("english-3sec-revision", "nour", EnglishId, ThirdSecondaryId,
            "مراجعة نهائية لغة إنجليزية",
            "مراجعة القواعد والقطع والترجمة مع نماذج امتحانات محاكية للامتحان الرسمي.",
            GroupType.Revision, TeachingMode.Hybrid, "المنصورة",
            450m, 550m, 18, 35, IsPublished: false,
            [(DayOfWeek.Friday, 10, 12)]),

        new("center-math-3sec", "hossam", MathematicsId, ThirdSecondaryId,
            "رياضيات الثانوية العامة - سنتر النخبة",
            "مجموعة إعداد لامتحان الثانوية العامة مع اختبارات أسبوعية وتقارير متابعة للأهل.",
            GroupType.ExamPreparation, TeachingMode.Offline, "وسط البلد - القاهرة",
            900m, 1100m, 18, 50, IsPublished: true,
            [(DayOfWeek.Saturday, 10, 13), (DayOfWeek.Monday, 10, 13)]),

        // Capacity 6 with six active enrollments: the "full group" fixture, so the
        // out-of-seats path has something to refuse.
        new("center-biology-2sec", "yasmin", BiologyId, SecondSecondaryId,
            "أحياء الصف الثاني الثانوي - مجموعة محدودة",
            "مجموعة صغيرة بعدد محدود للمتابعة الفردية.",
            GroupType.Regular, TeachingMode.Offline, "وسط البلد - القاهرة",
            750m, null, 0, 6, IsPublished: true,
            [(DayOfWeek.Sunday, 14, 16)]),

        // Owned by a teacher still under review, who therefore cannot publish at all.
        new("biology-1sec-pending", "tarek", BiologyId, FirstSecondaryId,
            "أحياء الصف الأول الثانوي",
            "مجموعة في انتظار توثيق حساب المدرس.",
            GroupType.Regular, TeachingMode.Offline, "طنطا",
            400m, null, 0, 20, IsPublished: false,
            [(DayOfWeek.Tuesday, 13, 15)])
    ];

    private async Task SeedGroupsAsync(CancellationToken cancellationToken)
    {
        var groups = new List<Group>();

        foreach (var definition in GroupDefinitions)
        {
            var teacher = _teachers[definition.TeacherKey];

            groups.Add(new Group
            {
                Id = SeedId.For("demo:group", definition.Key),
                TenantId = teacher.TenantId,
                TeacherId = teacher.Id,
                SubjectId = definition.SubjectId,
                GradeId = definition.GradeId,
                Name = definition.Name,
                // Deterministic: the slug is part of a public URL, and a new random suffix on
                // every run would break every link a tester or a fixture had written down.
                Slug = Slug.Create(definition.Name, definition.Key).Value,
                Description = definition.Description,
                Type = definition.Type,
                TeachingMode = definition.Mode,
                Location = definition.Location,
                MonthlyPrice = definition.MonthlyPrice,
                OriginalPrice = definition.OriginalPrice,
                DiscountPercentage = definition.DiscountPercentage,
                Capacity = definition.Capacity,
                EnrolledStudentsCount = 0,
                StartDate = _utcNow.Date.AddMonths(-3),
                EndDate = _utcNow.Date.AddMonths(4),
                IsPublished = definition.IsPublished,
                IsActive = true,
                WhatsAppLink = "https://chat.whatsapp.com/ustaz-demo-" + definition.Key,
                CreatedAt = _utcNow.AddMonths(-4)
            });
        }

        var persisted = await EnsureAsync("Groups", groups, cancellationToken);

        foreach (var definition in GroupDefinitions)
        {
            var id = SeedId.For("demo:group", definition.Key);
            _groups[definition.Key] = persisted.First(group => group.Id == id);
        }

        await SeedGroupDetailAsync(cancellationToken);
    }

    private async Task SeedGroupDetailAsync(CancellationToken cancellationToken)
    {
        var schedules = new List<GroupSchedule>();
        var curriculum = new List<GroupCurriculum>();
        var materials = new List<GroupMaterial>();

        foreach (var definition in GroupDefinitions)
        {
            var group = _groups[definition.Key];
            var slot = 0;

            foreach (var session in definition.Sessions)
            {
                slot++;

                schedules.Add(new GroupSchedule
                {
                    Id = SeedId.For("demo:group-schedule", group.Id, slot),
                    GroupId = group.Id,
                    DayOfWeek = session.Day,
                    StartTime = new TimeOnly(session.StartHour, 0),
                    EndTime = new TimeOnly(session.EndHour, 0),
                    Location = definition.Mode == TeachingMode.Online ? null : definition.Location,
                    MeetingUrl = definition.Mode == TeachingMode.Offline
                        ? null
                        : "https://meet.google.com/ustaz-demo-" + definition.Key,
                    IsActive = true,
                    CreatedAt = group.CreatedAt
                });
            }

            var unit = 0;

            foreach (var title in new[]
                     {
                         "الوحدة الأولى: المفاهيم الأساسية",
                         "الوحدة الثانية: التطبيقات وحل المسائل",
                         "الوحدة الثالثة: المراجعة النهائية"
                     })
            {
                unit++;

                curriculum.Add(new GroupCurriculum
                {
                    Id = SeedId.For("demo:group-curriculum", group.Id, unit),
                    GroupId = group.Id,
                    Title = title,
                    Description = "شرح تفصيلي مع تدريبات وواجبات أسبوعية.",
                    SortOrder = unit,
                    CreatedAt = group.CreatedAt
                });
            }

            materials.Add(new GroupMaterial
            {
                Id = SeedId.For("demo:group-material", group.Id, 1),
                GroupId = group.Id,
                Title = "ملزمة المجموعة",
                Description = "ملف PDF يحتوي على الملخصات والتدريبات.",
                FileUrl = DemoMedia.SubjectDocument(SubjectKey(definition.SubjectId), 1),
                FileType = MaterialFileType.Pdf,
                FileSizeInBytes = 2_400_000,
                SortOrder = 1,
                IsEnrolledOnly = true,
                CreatedAt = group.CreatedAt
            });

            // A public material too, so the enrolled-only filter has both sides.
            materials.Add(new GroupMaterial
            {
                Id = SeedId.For("demo:group-material", group.Id, 2),
                GroupId = group.Id,
                Title = "نموذج امتحان تجريبي",
                Description = "نموذج مجاني متاح لغير المشتركين للتعرف على مستوى المجموعة.",
                FileUrl = DemoMedia.SubjectDocument(SubjectKey(definition.SubjectId), 2),
                FileType = MaterialFileType.Pdf,
                FileSizeInBytes = 540_000,
                SortOrder = 2,
                IsEnrolledOnly = false,
                CreatedAt = group.CreatedAt
            });
        }

        await AddMissingAsync("GroupSchedules", schedules, cancellationToken);
        await AddMissingAsync("GroupCurriculums", curriculum, cancellationToken);
        await AddMissingAsync("GroupMaterials", materials, cancellationToken);
    }

    private sealed record CourseDefinition(
        string Key,
        string TeacherKey,
        Guid SubjectId,
        Guid GradeId,
        string Title,
        string Description,
        decimal Price,
        decimal? OriginalPrice,
        int DiscountPercentage,
        bool IsPublished,
        bool IsFeatured);

    private static readonly CourseDefinition[] CourseDefinitions =
    [
        new("physics-modern", "ahmed", default, default,
            "الفيزياء الحديثة من الصفر",
            "كورس مسجل يغطي الفيزياء الحديثة خطوة بخطوة مع أمثلة محلولة وتدريبات بعد كل درس.",
            499m, 699m, 30, IsPublished: true, IsFeatured: true),

        // Free and published: the "free courses" filter needs a row it must return.
        new("physics-foundations-free", "ahmed", default, default,
            "تأسيس الفيزياء - مجاناً",
            "كورس تأسيسي مجاني يشرح أساسيات القياس والمتجهات لطلاب الصف الأول الثانوي.",
            0m, null, 0, IsPublished: true, IsFeatured: false),

        new("math-algebra-geometry", "mariam", default, default,
            "الجبر والهندسة الفراغية",
            "شرح مبسط للجبر والهندسة الفراغية مع تدريبات تفاعلية بعد كل درس.",
            399m, null, 0, IsPublished: true, IsFeatured: false),

        new("chemistry-organic", "omar", default, default,
            "الكيمياء العضوية بالتفصيل",
            "كل ما تحتاجه في الكيمياء العضوية مع خرائط ذهنية وملخصات نهائية.",
            449m, 599m, 25, IsPublished: true, IsFeatured: true),

        // Unpublished, in the workspace that publishes nothing.
        new("english-grammar", "nour", default, default,
            "قواعد اللغة الإنجليزية الشاملة",
            "شرح قواعد اللغة الإنجليزية من المستوى الأساسي حتى المتقدم مع تطبيقات عملية.",
            0m, null, 0, IsPublished: false, IsFeatured: false),

        new("center-math-revision", "hossam", default, default,
            "مراجعة الرياضيات النهائية - سنتر النخبة",
            "مراجعة مكثفة بالفيديو لكل فروع الرياضيات مع بنك أسئلة شامل.",
            799m, 999m, 20, IsPublished: true, IsFeatured: true)
    ];

    /// <summary>
    /// Subject and grade per course, kept beside the definitions rather than inside them
    /// because the reference ids are instance-level constants on this class.
    /// </summary>
    private (Guid SubjectId, Guid GradeId) CourseTaxonomy(string key) => key switch
    {
        "physics-modern" => (PhysicsId, ThirdSecondaryId),
        "physics-foundations-free" => (PhysicsId, FirstSecondaryId),
        "math-algebra-geometry" => (MathematicsId, SecondSecondaryId),
        "chemistry-organic" => (ChemistryId, ThirdSecondaryId),
        "english-grammar" => (EnglishId, FirstSecondaryId),
        "center-math-revision" => (MathematicsId, ThirdSecondaryId),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown demo course.")
    };

    private async Task SeedCoursesAsync(CancellationToken cancellationToken)
    {
        var courses = new List<Course>();

        foreach (var definition in CourseDefinitions)
        {
            var teacher = _teachers[definition.TeacherKey];
            var (subjectId, gradeId) = CourseTaxonomy(definition.Key);

            courses.Add(new Course
            {
                Id = SeedId.For("demo:course", definition.Key),
                TenantId = teacher.TenantId,
                TeacherId = teacher.Id,
                SubjectId = subjectId,
                GradeId = gradeId,
                Title = definition.Title,
                Slug = Slug.Create(definition.Title, definition.Key).Value,
                Description = definition.Description,
                ThumbnailUrl = DemoMedia.CourseThumbnail(definition.Key),
                Price = definition.Price,
                OriginalPrice = definition.OriginalPrice,
                DiscountPercentage = definition.DiscountPercentage,
                IsPublished = definition.IsPublished,
                IsFeatured = definition.IsFeatured,
                CreatedAt = _utcNow.AddMonths(-4)
            });
        }

        var persisted = await EnsureAsync("Courses", courses, cancellationToken);

        foreach (var definition in CourseDefinitions)
        {
            var id = SeedId.For("demo:course", definition.Key);
            _courses[definition.Key] = persisted.First(course => course.Id == id);
        }

        await SeedCourseContentAsync(cancellationToken);
    }

    private async Task SeedCourseContentAsync(CancellationToken cancellationToken)
    {
        var sections = new List<CourseSection>();
        var lessons = new List<CourseLesson>();

        string[] sectionTitles =
        [
            "المقدمة والتأسيس",
            "الشرح التفصيلي",
            "المراجعة وحل المسائل"
        ];

        foreach (var definition in CourseDefinitions)
        {
            var course = _courses[definition.Key];
            var sectionOrder = 0;

            foreach (var sectionTitle in sectionTitles)
            {
                sectionOrder++;
                var sectionId = SeedId.For("demo:course-section", course.Id, sectionOrder);

                sections.Add(new CourseSection
                {
                    Id = sectionId,
                    CourseId = course.Id,
                    Title = sectionTitle,
                    Description = "يحتوي هذا القسم على دروس مسجلة بجودة عالية.",
                    SortOrder = sectionOrder,
                    CreatedAt = course.CreatedAt
                });

                for (var lessonIndex = 1; lessonIndex <= 3; lessonIndex++)
                {
                    lessons.Add(new CourseLesson
                    {
                        Id = SeedId.For("demo:course-lesson", sectionId, lessonIndex),
                        SectionId = sectionId,
                        Title = sectionTitle + " - الدرس " + lessonIndex,
                        Description = "درس مسجل مع ملف تدريبات مرفق.",
                        VideoUrl = DemoMedia.LessonVideo(definition.Key, sectionOrder, lessonIndex),
                        DurationInMinutes = 20 + (lessonIndex * 5),
                        SortOrder = lessonIndex,
                        IsPreview = sectionOrder == 1 && lessonIndex == 1,
                        IsPublished = true,
                        CreatedAt = course.CreatedAt
                    });
                }
            }
        }

        await AddMissingAsync("CourseSections", sections, cancellationToken);
        await AddMissingAsync("CourseLessons", lessons, cancellationToken);

        await RecalculateCourseTotalsAsync(cancellationToken);
    }

    /// <summary>
    /// Lets the domain derive the lesson count and runtime from the content that is actually
    /// stored, rather than seeding a hand-written total that could disagree with it.
    /// </summary>
    private async Task RecalculateCourseTotalsAsync(CancellationToken cancellationToken)
    {
        var courseIds = _courses.Values.Select(course => course.Id).ToList();

        var tracked = await context.Courses
            .Where(course => courseIds.Contains(course.Id))
            .Include(course => course.Sections)
            .ThenInclude(section => section.Lessons)
            .ToListAsync(cancellationToken);

        var updated = 0;

        foreach (var course in tracked)
        {
            var lessonCount = course.Sections.SelectMany(section => section.Lessons).Count();
            var minutes = course.Sections.SelectMany(section => section.Lessons).Sum(lesson => lesson.DurationInMinutes);

            if (course.LessonsCount == lessonCount && course.DurationInMinutes == minutes)
            {
                continue;
            }

            course.RecalculateContentTotals(_utcNow);
            updated++;
        }

        if (updated > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            report.Record("Courses", 0, updated, note: "content totals recalculated");
        }
    }

    private async Task SeedDiscountsAsync(CancellationToken cancellationToken)
    {
        var discounts = new List<Discount>
        {
            Build("ahmed", "WELCOME10", "خصم ترحيبي للطلاب الجدد", DiscountType.Percentage, 10m, 300m, 150m, 100, isActive: true,
                startsAgo: 30, endsIn: 60),
            Build("mariam", "MATH50", "خصم ثابت على اشتراك الشهر الأول", DiscountType.FixedAmount, 50m, 400m, null, 50, isActive: true,
                startsAgo: 15, endsIn: 45),
            Build("omar", "CHEM15", "خصم المراجعة النهائية", DiscountType.Percentage, 15m, null, 200m, null, isActive: true,
                startsAgo: 10, endsIn: 90),
            Build("nour", "ENGLISH20", "خصم الباقة الشاملة", DiscountType.Percentage, 20m, 400m, 120m, 30, isActive: true,
                startsAgo: 5, endsIn: 30),
            Build("hossam", "ELITE25", "خصم سنتر النخبة للأشقاء", DiscountType.Percentage, 25m, 800m, 300m, 20, isActive: true,
                startsAgo: 20, endsIn: 40),
            // Expired, so the "no longer valid" branch of redemption has a code to refuse.
            Build("ahmed", "SUMMER2024", "خصم صيفي منتهي", DiscountType.Percentage, 30m, null, null, 50, isActive: true,
                startsAgo: 200, endsIn: -120),
            // Switched off by the teacher while still inside its date window.
            Build("mariam", "PAUSED10", "خصم موقوف مؤقتاً", DiscountType.Percentage, 10m, null, null, null, isActive: false,
                startsAgo: 7, endsIn: 60)
        };

        await AddMissingAsync("Discounts", discounts, cancellationToken);

        Discount Build(
            string teacherKey,
            string code,
            string description,
            DiscountType type,
            decimal value,
            decimal? minimumAmount,
            decimal? maximumDiscount,
            int? usageLimit,
            bool isActive,
            int startsAgo,
            int endsIn)
        {
            var teacher = _teachers[teacherKey];

            return new Discount
            {
                Id = SeedId.For("demo:discount", teacherKey + ":" + code),
                TenantId = teacher.TenantId,
                TeacherId = teacher.Id,
                Code = code,
                Description = description,
                Type = type,
                Value = value,
                MinimumAmount = minimumAmount,
                MaximumDiscount = maximumDiscount,
                StartDate = _utcNow.Date.AddDays(-startsAgo),
                EndDate = _utcNow.Date.AddDays(endsIn),
                UsageLimit = usageLimit,
                UsedCount = 0,
                IsActive = isActive,
                CreatedAt = _utcNow.AddDays(-startsAgo)
            };
        }
    }
}
