using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.ValueObjects;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    private List<Group> BuildGroups(TeacherProfile ahmed, TeacherProfile mariam, TeacherProfile omar, TeacherProfile nour) =>
    [
        BuildGroup(ahmed, PhysicsId, ThirdSecondaryId,
            "فيزياء الثانوية العامة - مجموعة مكثفة",
            "مراجعة شاملة لمنهج الفيزياء للصف الثالث الثانوي مع حل امتحانات السنوات السابقة.",
            GroupType.Intensive, TeachingMode.Hybrid, "مدينة نصر - القاهرة",
            850m, 1000m, 15, 30,
            [(DayOfWeek.Saturday, 16, 18), (DayOfWeek.Tuesday, 16, 18)]),

        BuildGroup(ahmed, PhysicsId, FirstSecondaryId,
            "أساسيات الفيزياء - الصف الأول الثانوي",
            "تأسيس قوي في الميكانيكا والكهربية مع تطبيقات عملية أسبوعية.",
            GroupType.Regular, TeachingMode.Offline, "مدينة نصر - القاهرة",
            500m, null, 0, 25,
            [(DayOfWeek.Monday, 17, 19)]),

        BuildGroup(mariam, MathematicsId, ThirdSecondaryId,
            "تفاضل وتكامل - الثانوية العامة",
            "شرح مفصل للتفاضل والتكامل مع بنك أسئلة متجدد كل أسبوع.",
            GroupType.Regular, TeachingMode.Online, null,
            700m, 800m, 12, 40,
            [(DayOfWeek.Sunday, 18, 20), (DayOfWeek.Wednesday, 18, 20)]),

        BuildGroup(omar, ChemistryId, SecondSecondaryId,
            "كيمياء الصف الثاني الثانوي",
            "منهج الكيمياء كاملاً مع تجارب معملية مصورة وملخصات قابلة للتحميل.",
            GroupType.Regular, TeachingMode.Offline, "سموحة - الإسكندرية",
            600m, null, 0, 20,
            [(DayOfWeek.Thursday, 15, 17)]),

        BuildGroup(nour, EnglishId, ThirdSecondaryId,
            "مراجعة نهائية لغة إنجليزية",
            "مراجعة القواعد والقطع والترجمة مع نماذج امتحانات محاكية للامتحان الرسمي.",
            GroupType.Revision, TeachingMode.Hybrid, "المنصورة",
            450m, 550m, 18, 35,
            [(DayOfWeek.Friday, 10, 12)])
    ];

    private Group BuildGroup(
        TeacherProfile teacher,
        Guid subjectId,
        Guid gradeId,
        string name,
        string description,
        GroupType type,
        TeachingMode mode,
        string? location,
        decimal monthlyPrice,
        decimal? originalPrice,
        int discountPercentage,
        int capacity,
        (DayOfWeek Day, int StartHour, int EndHour)[] sessions)
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            TenantId = teacher.TenantId,
            TeacherId = teacher.Id,
            SubjectId = subjectId,
            GradeId = gradeId,
            Name = name,
            Slug = Slug.CreateUnique(name).Value,
            Description = description,
            Type = type,
            TeachingMode = mode,
            Location = location,
            MonthlyPrice = monthlyPrice,
            OriginalPrice = originalPrice,
            DiscountPercentage = discountPercentage,
            Capacity = capacity,
            EnrolledStudentsCount = 0,
            StartDate = _utcNow.Date.AddDays(7),
            EndDate = _utcNow.Date.AddMonths(6),
            IsPublished = true,
            IsActive = true,
            WhatsAppLink = "https://chat.whatsapp.com/DemoUstazGroup" + Guid.NewGuid().ToString("N")[..8]
        };

        var order = 1;
        foreach (var session in sessions)
        {
            group.Schedules.Add(new GroupSchedule
            {
                Id = Guid.NewGuid(),
                DayOfWeek = session.Day,
                StartTime = new TimeOnly(session.StartHour, 0),
                EndTime = new TimeOnly(session.EndHour, 0),
                Location = mode == TeachingMode.Online ? null : location,
                MeetingUrl = mode == TeachingMode.Offline ? null : "https://meet.google.com/ustaz-demo",
                IsActive = true
            });
        }

        foreach (var title in new[] { "الوحدة الأولى: المفاهيم الأساسية", "الوحدة الثانية: التطبيقات", "الوحدة الثالثة: المراجعة النهائية" })
        {
            group.Curriculum.Add(new GroupCurriculum
            {
                Id = Guid.NewGuid(),
                Title = title,
                Description = "شرح تفصيلي مع تدريبات وواجبات أسبوعية.",
                SortOrder = order++
            });
        }

        group.Materials.Add(new GroupMaterial
        {
            Id = Guid.NewGuid(),
            Title = "ملزمة المجموعة",
            Description = "ملف PDF يحتوي على الملخصات والتدريبات.",
            FileUrl = "https://res.cloudinary.com/demo/raw/upload/ustaz/materials/sample.pdf",
            FileType = MaterialFileType.Pdf,
            SortOrder = 1,
            IsEnrolledOnly = true
        });

        return group;
    }
}
