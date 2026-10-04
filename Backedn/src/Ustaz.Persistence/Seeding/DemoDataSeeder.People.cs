using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    /// <summary>
    /// The demo teaching staff. Invented personas, not real people.
    ///
    /// The verification column is the point of the list: search, the public directory and the
    /// publish guard all behave differently per state, and every one of the five states in
    /// <see cref="VerificationStatus"/> appears here so none of those branches is untested.
    /// </summary>
    private static readonly TeacherDefinition[] TeacherDefinitions =
    [
        new("ahmed", "ahmed", "ahmed.mohamed@ustaz.demo", "أحمد", "محمد", "أ. أحمد محمد",
            "مدرس فيزياء للثانوية العامة", 14, "القاهرة", "+201012345601",
            [nameof(PhysicsId)], VerificationStatus.Verified, IsFeatured: true),

        new("mariam", "mariam", "mariam.hassan@ustaz.demo", "مريم", "حسن", "أ. مريم حسن",
            "مدرسة رياضيات وتفاضل وتكامل", 9, "الجيزة", "+201012345602",
            [nameof(MathematicsId)], VerificationStatus.Verified, IsFeatured: true),

        new("omar", "omar", "omar.khaled@ustaz.demo", "عمر", "خالد", "أ. عمر خالد",
            "مدرس كيمياء عضوية وغير عضوية", 11, "الإسكندرية", "+201012345603",
            [nameof(ChemistryId)], VerificationStatus.Verified, IsFeatured: false),

        new("nour", "nour", "nour.ahmed@ustaz.demo", "نور", "أحمد", "أ. نور أحمد",
            "مدرسة لغة إنجليزية ومحادثة", 7, "المنصورة", "+201012345604",
            [nameof(EnglishId)], VerificationStatus.Verified, IsFeatured: false),

        new("hossam", "center", "hossam.fathy@ustaz.demo", "حسام", "فتحي", "أ. حسام فتحي",
            "مدير سنتر النخبة ومدرس رياضيات", 18, "القاهرة", "+201012345605",
            [nameof(MathematicsId), nameof(PhysicsId)], VerificationStatus.Verified, IsFeatured: true),

        new("yasmin", "yasmin", "yasmin.saad@ustaz.demo", "ياسمين", "سعد", "أ. ياسمين سعد",
            "مدرسة أحياء وجيولوجيا", 6, "القاهرة", "+201012345606",
            [nameof(BiologyId)], VerificationStatus.Verified, IsFeatured: false),

        new("fatma", "fatma", "fatma.zaki@ustaz.demo", "فاطمة", "زكي", "أ. فاطمة زكي",
            "مدرسة لغة عربية ونحو", 4, "القاهرة", "+201012345607",
            [nameof(ArabicId)], VerificationStatus.Pending, IsFeatured: false),

        new("tarek", "tarek", "tarek.lotfy@ustaz.demo", "طارق", "لطفي", "أ. طارق لطفي",
            "مدرس أحياء للثانوية العامة", 5, "طنطا", "+201012345608",
            [nameof(BiologyId)], VerificationStatus.UnderReview, IsFeatured: false),

        // Deliberately missing a bio, so IsReadyForVerification is false and the
        // "complete your profile first" path has a real account behind it.
        new("dalia", "dalia", "dalia.mostafa@ustaz.demo", "داليا", "مصطفى", "أ. داليا مصطفى",
            "مدرسة جغرافيا", 3, "أسيوط", "+201012345609",
            [nameof(GeographyId)], VerificationStatus.NotSubmitted, IsFeatured: false, HasBio: false),

        new("sameh", "sameh", "sameh.nabil@ustaz.demo", "سامح", "نبيل", "أ. سامح نبيل",
            "مدرس رياضيات مساعد", 2, "أسيوط", "+201012345610",
            [nameof(MathematicsId)], VerificationStatus.Rejected, IsFeatured: false)
    ];

    private sealed record TeacherDefinition(
        string Key,
        string TenantKey,
        string Email,
        string FirstName,
        string LastName,
        string DisplayName,
        string Headline,
        int YearsOfExperience,
        string City,
        string Phone,
        string[] SubjectNames,
        VerificationStatus Verification,
        bool IsFeatured,
        bool HasBio = true);

    private static Guid SubjectByName(string name) => name switch
    {
        nameof(MathematicsId) => MathematicsId,
        nameof(PhysicsId) => PhysicsId,
        nameof(ChemistryId) => ChemistryId,
        nameof(BiologyId) => BiologyId,
        nameof(ArabicId) => ArabicId,
        nameof(EnglishId) => EnglishId,
        nameof(GeographyId) => GeographyId,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown demo subject.")
    };

    private async Task SeedTeachersAsync(string password, CancellationToken cancellationToken)
    {
        var profiles = new List<TeacherProfile>();

        foreach (var definition in TeacherDefinitions)
        {
            var userId = SeedId.For("demo:teacher-user", definition.Key);

            await EnsureUserAsync(
                userId,
                definition.Email,
                password,
                RoleNames.Teacher,
                user =>
                {
                    user.FirstName = definition.FirstName;
                    user.LastName = definition.LastName;
                    user.DisplayName = definition.DisplayName;
                    user.City = definition.City;
                    user.PhoneNumber = definition.Phone;
                    user.PhoneNumberConfirmed = true;
                    user.IsVerified = definition.Verification == VerificationStatus.Verified;
                    user.ProfileImageUrl = DemoMedia.Avatar(definition.Key);
                    user.CreatedAt = _utcNow.AddMonths(-8);
                },
                cancellationToken);

            profiles.Add(BuildTeacherProfile(definition, userId));
        }

        var persisted = await EnsureAsync("TeacherProfiles", profiles, cancellationToken);

        foreach (var definition in TeacherDefinitions)
        {
            var id = SeedId.For("demo:teacher", definition.Key);
            _teachers[definition.Key] = persisted.First(profile => profile.Id == id);
        }

        await SeedTeacherSubjectsAndGradesAsync(cancellationToken);
        await SeedTeacherExtrasAsync(cancellationToken);
    }

    private TeacherProfile BuildTeacherProfile(TeacherDefinition definition, Guid userId)
    {
        var verifiedAt = definition.Verification == VerificationStatus.Verified
            ? _utcNow.AddMonths(-7)
            : (DateTime?)null;

        var submittedAt = definition.Verification switch
        {
            VerificationStatus.NotSubmitted => null,
            _ => (DateTime?)_utcNow.AddMonths(-7).AddDays(-2)
        };

        return new TeacherProfile
        {
            Id = SeedId.For("demo:teacher", definition.Key),
            UserId = userId,
            TenantId = _tenants[definition.TenantKey].Id,
            Headline = definition.Headline,
            Bio = definition.HasBio
                ? definition.DisplayName + " - " + definition.Headline + ". خبرة "
                  + definition.YearsOfExperience
                  + " سنة في التدريس مع التركيز على الفهم العميق وحل المسائل وبناء أساس قوي لكل طالب."
                : null,
            YearsOfExperience = definition.YearsOfExperience,
            City = definition.City,
            WhatsAppNumber = definition.Phone,
            ProfileImageUrl = DemoMedia.Avatar(definition.Key),
            CoverImageUrl = DemoMedia.TeacherCover(definition.Key),
            IsFeatured = definition.IsFeatured,
            IsVerified = definition.Verification == VerificationStatus.Verified,
            VerificationStatus = definition.Verification,
            VerifiedAt = verifiedAt,
            VerificationSubmittedAt = submittedAt,
            VerificationNotes = definition.Verification == VerificationStatus.Rejected
                ? "المستندات المرفقة غير واضحة. يرجى إعادة إرسال صورة البطاقة وشهادة الخبرة."
                : null,
            PreferredTeachingMode = definition.Key switch
            {
                "mariam" => TeachingMode.Online,
                "omar" => TeachingMode.Offline,
                _ => TeachingMode.Hybrid
            },
            CreatedAt = _utcNow.AddMonths(-8)
        };
    }

    private async Task SeedTeacherSubjectsAndGradesAsync(CancellationToken cancellationToken)
    {
        var subjects = new List<TeacherSubject>();
        var grades = new List<TeacherGrade>();

        foreach (var definition in TeacherDefinitions)
        {
            var teacherId = _teachers[definition.Key].Id;

            foreach (var subjectName in definition.SubjectNames)
            {
                var subjectId = SubjectByName(subjectName);

                subjects.Add(new TeacherSubject
                {
                    Id = SeedId.For("demo:teacher-subject", teacherId, subjectId),
                    TeacherId = teacherId,
                    SubjectId = subjectId,
                    CreatedAt = _utcNow.AddMonths(-8)
                });
            }

            // Not every teacher covers every grade: the grade filter has to be able to
            // return a narrower result than "all teachers".
            var gradeIds = definition.Key switch
            {
                "ahmed" => new[] { FirstSecondaryId, SecondSecondaryId, ThirdSecondaryId },
                "mariam" => [SecondSecondaryId, ThirdSecondaryId],
                "omar" => [SecondSecondaryId, ThirdSecondaryId],
                "nour" => [FirstSecondaryId, SecondSecondaryId, ThirdSecondaryId],
                "hossam" => [ThirdSecondaryId],
                "yasmin" => [SecondSecondaryId, ThirdSecondaryId],
                "fatma" => [PreparatoryId, FirstSecondaryId],
                "tarek" => [FirstSecondaryId],
                "dalia" => [PreparatoryId],
                _ => [PrimaryId, PreparatoryId]
            };

            foreach (var gradeId in gradeIds)
            {
                grades.Add(new TeacherGrade
                {
                    Id = SeedId.For("demo:teacher-grade", teacherId, gradeId),
                    TeacherId = teacherId,
                    GradeId = gradeId,
                    CreatedAt = _utcNow.AddMonths(-8)
                });
            }
        }

        await AddMissingAsync("TeacherSubjects", subjects, cancellationToken);
        await AddMissingAsync("TeacherGrades", grades, cancellationToken);
    }

    private async Task SeedTeacherExtrasAsync(CancellationToken cancellationToken)
    {
        var achievements = new List<TeacherAchievement>();
        var links = new List<TeacherSocialLink>();

        foreach (var definition in TeacherDefinitions.Where(candidate =>
                     candidate.Verification == VerificationStatus.Verified))
        {
            var teacherId = _teachers[definition.Key].Id;

            achievements.Add(new TeacherAchievement
            {
                Id = SeedId.For("demo:achievement", teacherId, 1),
                TeacherId = teacherId,
                Title = "نتائج متميزة في الثانوية العامة",
                Description = "إشراف على طلاب حققوا مراكز متقدمة على مستوى المحافظة.",
                ImageUrl = DemoMedia.AchievementImage(1),
                Year = _utcNow.Year - 1,
                SortOrder = 1,
                CreatedAt = _utcNow.AddMonths(-8)
            });

            achievements.Add(new TeacherAchievement
            {
                Id = SeedId.For("demo:achievement", teacherId, 2),
                TeacherId = teacherId,
                Title = "دورة تدريبية في طرق التدريس الحديثة",
                Description = "شهادة معتمدة في استخدام التعلم النشط داخل الفصل.",
                ImageUrl = DemoMedia.AchievementImage(2),
                Year = _utcNow.Year - 3,
                SortOrder = 2,
                CreatedAt = _utcNow.AddMonths(-8)
            });

            links.Add(new TeacherSocialLink
            {
                Id = SeedId.For("demo:social", teacherId, (int)SocialPlatform.WhatsApp),
                TeacherId = teacherId,
                Platform = SocialPlatform.WhatsApp,
                Url = WhatsAppLink(definition.Phone),
                SortOrder = 1,
                CreatedAt = _utcNow.AddMonths(-8)
            });

            links.Add(new TeacherSocialLink
            {
                Id = SeedId.For("demo:social", teacherId, (int)SocialPlatform.YouTube),
                TeacherId = teacherId,
                Platform = SocialPlatform.YouTube,
                Url = "https://www.youtube.com/@ustaz-demo-" + definition.Key,
                SortOrder = 2,
                CreatedAt = _utcNow.AddMonths(-8)
            });
        }

        await AddMissingAsync("TeacherAchievements", achievements, cancellationToken);
        await AddMissingAsync("TeacherSocialLinks", links, cancellationToken);
    }

    // -----------------------------------------------------------------------
    // Students
    // -----------------------------------------------------------------------

    /// <summary>
    /// Twelve students. Enough to page a list more than once, spread across four cities and
    /// three grades so filtering, sorting and search by name or school all have both matching
    /// and non-matching rows to work with.
    /// </summary>
    private static readonly (string Key, string Email, string First, string Last, string City, string School, string GradeName)[]
        StudentDefinitions =
        [
            ("salma", "salma.ibrahim@ustaz.demo", "سلمى", "إبراهيم", "القاهرة", "مدرسة النصر الثانوية", nameof(ThirdSecondaryId)),
            ("youssef", "youssef.tarek@ustaz.demo", "يوسف", "طارق", "الجيزة", "مدرسة الأورمان الثانوية", nameof(ThirdSecondaryId)),
            ("habiba", "habiba.samir@ustaz.demo", "حبيبة", "سمير", "الإسكندرية", "مدرسة العباسية الثانوية", nameof(SecondSecondaryId)),
            ("kareem", "kareem.adel@ustaz.demo", "كريم", "عادل", "المنصورة", "مدرسة المنصورة الثانوية", nameof(ThirdSecondaryId)),
            ("mohamed", "mohamed.gamal@ustaz.demo", "محمد", "جمال", "القاهرة", "مدرسة الحرية الثانوية", nameof(FirstSecondaryId)),
            ("aya", "aya.hossam@ustaz.demo", "آية", "حسام", "القاهرة", "مدرسة النصر الثانوية", nameof(SecondSecondaryId)),
            ("zeyad", "zeyad.magdy@ustaz.demo", "زياد", "مجدي", "الجيزة", "مدرسة الأورمان الثانوية", nameof(ThirdSecondaryId)),
            ("farida", "farida.nabil@ustaz.demo", "فريدة", "نبيل", "الإسكندرية", "مدرسة سموحة الثانوية", nameof(FirstSecondaryId)),
            ("omnia", "omnia.rashad@ustaz.demo", "أمنية", "رشاد", "القاهرة", "مدرسة المعادي الثانوية", nameof(ThirdSecondaryId)),
            ("hassan", "hassan.sobhy@ustaz.demo", "حسن", "صبحي", "القاهرة", "مدرسة المعادي الثانوية", nameof(SecondSecondaryId)),
            ("malak", "malak.wael@ustaz.demo", "ملك", "وائل", "طنطا", "مدرسة طنطا الثانوية", nameof(FirstSecondaryId)),
            ("amir", "amir.yehia@ustaz.demo", "أمير", "يحيى", "أسيوط", "مدرسة أسيوط الثانوية", nameof(PreparatoryId))
        ];

    private static Guid GradeByName(string name) => name switch
    {
        nameof(PrimaryId) => PrimaryId,
        nameof(PreparatoryId) => PreparatoryId,
        nameof(FirstSecondaryId) => FirstSecondaryId,
        nameof(SecondSecondaryId) => SecondSecondaryId,
        nameof(ThirdSecondaryId) => ThirdSecondaryId,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown demo grade.")
    };

    private async Task SeedStudentsAsync(string password, CancellationToken cancellationToken)
    {
        var profiles = new List<StudentProfile>();
        var index = 0;

        foreach (var definition in StudentDefinitions)
        {
            index++;
            var userId = SeedId.For("demo:student-user", definition.Key);
            var localIndex = index;

            await EnsureUserAsync(
                userId,
                definition.Email,
                password,
                RoleNames.Student,
                user =>
                {
                    user.FirstName = definition.First;
                    user.LastName = definition.Last;
                    user.DisplayName = definition.First + " " + definition.Last;
                    user.City = definition.City;
                    user.PhoneNumber = "+2011000000" + localIndex.ToString("D2");
                    user.ProfileImageUrl = DemoMedia.Avatar(definition.Key);
                    // One inactive account, so the suspended-user path has a subject.
                    user.IsActive = definition.Key != "amir";
                    user.CreatedAt = _utcNow.AddMonths(-6).AddDays(localIndex);
                },
                cancellationToken);

            profiles.Add(new StudentProfile
            {
                Id = SeedId.For("demo:student", definition.Key),
                UserId = userId,
                GradeId = GradeByName(definition.GradeName),
                SchoolName = definition.School,
                City = definition.City,
                PhoneNumber = "+2011000000" + localIndex.ToString("D2"),
                ParentPhoneNumber = "+2012000000" + localIndex.ToString("D2"),
                DateOfBirth = DateOnly.FromDateTime(_utcNow.AddYears(-17).AddDays(localIndex * 11)),
                CreatedAt = _utcNow.AddMonths(-6).AddDays(localIndex)
            });
        }

        var persisted = await EnsureAsync("StudentProfiles", profiles, cancellationToken);

        foreach (var definition in StudentDefinitions)
        {
            var id = SeedId.For("demo:student", definition.Key);
            _students[definition.Key] = persisted.First(profile => profile.Id == id);
        }

        await SeedNotificationPreferencesAsync(cancellationToken);
    }

    /// <summary>
    /// Explicit notification preferences for three accounts. A missing row means "use the
    /// category default", so most users correctly have none — these exist so the preference
    /// screen, and the dispatcher's suppression path, both have something to read.
    /// </summary>
    private async Task SeedNotificationPreferencesAsync(CancellationToken cancellationToken)
    {
        var preferences = new List<NotificationPreference>
        {
            // In-app only: email muted for homework.
            Preference("salma", _students["salma"].UserId, NotificationCategory.Homework, NotificationChannel.InApp),
            // Everything on, including WhatsApp.
            Preference("youssef", _students["youssef"].UserId, NotificationCategory.Attendance,
                NotificationChannel.InApp | NotificationChannel.Email | NotificationChannel.WhatsApp),
            // Announcements switched off entirely.
            new()
            {
                Id = SeedId.For("demo:notification-preference", "habiba:announcement"),
                UserId = _students["habiba"].UserId,
                Category = NotificationCategory.Announcement,
                Channels = NotificationChannel.None,
                IsEnabled = false,
                CreatedAt = _utcNow.AddMonths(-2)
            }
        };

        await AddMissingAsync("NotificationPreferences", preferences, cancellationToken);

        NotificationPreference Preference(
            string key, Guid userId, NotificationCategory category, NotificationChannel channels) =>
            new()
            {
                Id = SeedId.For("demo:notification-preference", key + ":" + category),
                UserId = userId,
                Category = category,
                Channels = channels,
                IsEnabled = true,
                CreatedAt = _utcNow.AddMonths(-2)
            };
    }

    // -----------------------------------------------------------------------
    // Guardians
    // -----------------------------------------------------------------------

    /// <summary>
    /// Four guardians, covering every shape the parent domain supports: one following two
    /// children, one following a single child, one whose link is still unconfirmed (and who
    /// therefore has no access to anything), and one with no children linked at all.
    ///
    /// The unconfirmed and childless guardians are the important ones. Parent-facing queries
    /// all join through <see cref="ParentStudentLink"/>, so these two accounts are what prove
    /// a guardian cannot read a family they are not part of.
    /// </summary>
    private async Task SeedParentsAsync(string password, CancellationToken cancellationToken)
    {
        (string Key, string Email, string First, string Last, string City, string Occupation)[] definitions =
        [
            ("ibrahim", "ibrahim.said@ustaz.demo", "إبراهيم", "سعيد", "القاهرة", "مهندس"),
            ("samir", "samir.fouad@ustaz.demo", "سمير", "فؤاد", "الإسكندرية", "طبيب"),
            ("adel", "adel.mansour@ustaz.demo", "عادل", "منصور", "المنصورة", "محاسب"),
            ("nadia", "nadia.kamel@ustaz.demo", "نادية", "كامل", "الجيزة", "مدرسة")
        ];

        var profiles = new List<ParentProfile>();
        var index = 0;

        foreach (var definition in definitions)
        {
            index++;
            var userId = SeedId.For("demo:parent-user", definition.Key);
            var phone = "+2013000000" + index.ToString("D2");

            await EnsureUserAsync(
                userId,
                definition.Email,
                password,
                RoleNames.Parent,
                user =>
                {
                    user.FirstName = definition.First;
                    user.LastName = definition.Last;
                    user.DisplayName = definition.First + " " + definition.Last;
                    user.City = definition.City;
                    user.PhoneNumber = phone;
                    user.ProfileImageUrl = DemoMedia.Avatar(definition.Key);
                    user.CreatedAt = _utcNow.AddMonths(-5);
                },
                cancellationToken);

            profiles.Add(new ParentProfile
            {
                Id = SeedId.For("demo:parent", definition.Key),
                UserId = userId,
                PhoneNumber = phone,
                AlternatePhoneNumber = definition.Key == "ibrahim" ? "+201340000099" : null,
                City = definition.City,
                Occupation = definition.Occupation,
                CreatedAt = _utcNow.AddMonths(-5)
            });
        }

        var persisted = await EnsureAsync("ParentProfiles", profiles, cancellationToken);

        foreach (var definition in definitions)
        {
            var id = SeedId.For("demo:parent", definition.Key);
            _parents[definition.Key] = persisted.First(profile => profile.Id == id);
        }

        var links = new List<ParentStudentLink>
        {
            Link("ibrahim", "salma", "والد", isConfirmed: true, isPrimary: true),
            Link("ibrahim", "youssef", "والد", isConfirmed: true, isPrimary: false),
            Link("samir", "habiba", "والد", isConfirmed: true, isPrimary: true),
            // Awaiting approval: grants no access to anything until the student confirms it.
            Link("adel", "kareem", "والد", isConfirmed: false, isPrimary: false)
            // nadia is deliberately left with no links at all.
        };

        await AddMissingAsync("ParentStudentLinks", links, cancellationToken);

        ParentStudentLink Link(string parentKey, string studentKey, string relationship, bool isConfirmed, bool isPrimary)
        {
            var parentId = _parents[parentKey].Id;
            var studentId = _students[studentKey].Id;

            return new ParentStudentLink
            {
                Id = SeedId.For("demo:parent-link", parentId, studentId),
                ParentId = parentId,
                StudentId = studentId,
                Relationship = relationship,
                IsConfirmed = isConfirmed,
                ConfirmedAt = isConfirmed ? _utcNow.AddMonths(-5).AddDays(1) : null,
                IsPrimaryContact = isPrimary,
                CreatedAt = _utcNow.AddMonths(-5)
            };
        }
    }

    /// <summary>
    /// Registers students against the workspaces whose groups and courses they are in. This
    /// is the row a workspace's student list reads, so it has to exist for every student the
    /// workspace can see — and must not exist for any student it cannot.
    /// </summary>
    private async Task SeedTenantStudentsAsync(CancellationToken cancellationToken)
    {
        var memberships = new List<TenantStudent>();
        var seen = new HashSet<(Guid TenantId, Guid StudentId)>();
        var code = 1;

        foreach (var enrollment in _groupEnrollments.Values)
        {
            var group = _groups.Values.First(candidate => candidate.Id == enrollment.GroupId);
            Add(group.TenantId, enrollment.StudentId);
        }

        foreach (var enrollment in _courseEnrollments.Values)
        {
            var course = _courses.Values.First(candidate => candidate.Id == enrollment.CourseId);
            Add(course.TenantId, enrollment.StudentId);
        }

        await AddMissingAsync("TenantStudents", memberships, cancellationToken);

        void Add(Guid tenantId, Guid studentId)
        {
            if (!seen.Add((tenantId, studentId)))
            {
                return;
            }

            memberships.Add(new TenantStudent
            {
                Id = SeedId.For("demo:tenant-student", tenantId, studentId),
                TenantId = tenantId,
                StudentId = studentId,
                IsActive = true,
                JoinedAt = _utcNow.AddMonths(-4),
                StudentCode = "ST-" + (code++).ToString("D4"),
                CreatedAt = _utcNow.AddMonths(-4)
            });
        }
    }
}
