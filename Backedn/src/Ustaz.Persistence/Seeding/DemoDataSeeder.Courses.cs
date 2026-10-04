using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.ValueObjects;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    private List<Course> BuildCourses(TeacherProfile ahmed, TeacherProfile mariam, TeacherProfile omar, TeacherProfile nour) =>
    [
        BuildCourse(ahmed, PhysicsId, ThirdSecondaryId,
            "الفيزياء الحديثة من الصفر",
            "كورس مسجل يغطي الفيزياء الحديثة خطوة بخطوة مع أمثلة محلولة وتدريبات بعد كل درس.",
            499m, 699m, 30),

        BuildCourse(mariam, MathematicsId, SecondSecondaryId,
            "الجبر والهندسة الفراغية",
            "شرح مبسط للجبر والهندسة الفراغية مع تدريبات تفاعلية بعد كل درس.",
            399m, null, 0),

        BuildCourse(omar, ChemistryId, ThirdSecondaryId,
            "الكيمياء العضوية بالتفصيل",
            "كل ما تحتاجه في الكيمياء العضوية مع خرائط ذهنية وملخصات نهائية.",
            449m, 599m, 25),

        BuildCourse(nour, EnglishId, FirstSecondaryId,
            "قواعد اللغة الإنجليزية الشاملة",
            "شرح قواعد اللغة الإنجليزية من المستوى الأساسي حتى المتقدم مع تطبيقات عملية.",
            0m, null, 0)
    ];

    private Course BuildCourse(
        TeacherProfile teacher,
        Guid subjectId,
        Guid gradeId,
        string title,
        string description,
        decimal price,
        decimal? originalPrice,
        int discountPercentage)
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            TenantId = teacher.TenantId,
            TeacherId = teacher.Id,
            SubjectId = subjectId,
            GradeId = gradeId,
            Title = title,
            Slug = Slug.CreateUnique(title).Value,
            Description = description,
            ThumbnailUrl = "https://res.cloudinary.com/demo/image/upload/ustaz/courses/placeholder.jpg",
            Price = price,
            OriginalPrice = originalPrice,
            DiscountPercentage = discountPercentage,
            IsPublished = true,
            IsFeatured = discountPercentage > 0
        };

        var sectionTitles = new[]
        {
            "المقدمة والتأسيس",
            "الشرح التفصيلي",
            "المراجعة وحل المسائل"
        };

        var sectionOrder = 1;
        var totalMinutes = 0;
        var totalLessons = 0;

        foreach (var sectionTitle in sectionTitles)
        {
            var section = new CourseSection
            {
                Id = Guid.NewGuid(),
                Title = sectionTitle,
                Description = "يحتوي هذا القسم على دروس مسجلة بجودة عالية.",
                SortOrder = sectionOrder
            };

            for (var lessonIndex = 1; lessonIndex <= 3; lessonIndex++)
            {
                var duration = 20 + (lessonIndex * 5);
                section.Lessons.Add(new CourseLesson
                {
                    Id = Guid.NewGuid(),
                    Title = sectionTitle + " - الدرس " + lessonIndex,
                    Description = "درس مسجل مع ملف تدريبات مرفق.",
                    VideoUrl = "https://res.cloudinary.com/demo/video/upload/ustaz/lessons/sample.mp4",
                    DurationInMinutes = duration,
                    SortOrder = lessonIndex,
                    IsPreview = sectionOrder == 1 && lessonIndex == 1,
                    IsPublished = true
                });

                totalMinutes += duration;
                totalLessons++;
            }

            course.Sections.Add(section);
            sectionOrder++;
        }

        course.LessonsCount = totalLessons;
        course.DurationInMinutes = totalMinutes;

        return course;
    }

    private List<Discount> BuildDiscounts(TeacherProfile ahmed, TeacherProfile mariam, TeacherProfile omar, TeacherProfile nour) =>
    [
        BuildDiscount(ahmed, "WELCOME10", "خصم ترحيبي للطلاب الجدد", DiscountType.Percentage, 10m, 300m, 150m, 100),
        BuildDiscount(mariam, "MATH50", "خصم ثابت على اشتراك الشهر الأول", DiscountType.FixedAmount, 50m, 400m, null, 50),
        BuildDiscount(omar, "CHEM15", "خصم المراجعة النهائية", DiscountType.Percentage, 15m, null, 200m, null),
        BuildDiscount(nour, "ENGLISH20", "خصم الباقة الشاملة", DiscountType.Percentage, 20m, 400m, 120m, 30)
    ];

    private Discount BuildDiscount(
        TeacherProfile teacher,
        string code,
        string description,
        DiscountType type,
        decimal value,
        decimal? minimumAmount,
        decimal? maximumDiscount,
        int? usageLimit) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = teacher.TenantId,
            TeacherId = teacher.Id,
            Code = code,
            Description = description,
            Type = type,
            Value = value,
            MinimumAmount = minimumAmount,
            MaximumDiscount = maximumDiscount,
            StartDate = _utcNow.Date,
            EndDate = _utcNow.Date.AddMonths(3),
            UsageLimit = usageLimit,
            UsedCount = 0,
            IsActive = true
        };
}
