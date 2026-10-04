using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;

namespace Ustaz.Persistence.Seeding;

/// <summary>
/// Deterministic reference data. Fixed ids keep seeding idempotent across runs.
///
/// These are the rows every dropdown, filter and search facet in the product reads from, so
/// the catalogue has to cover the whole Egyptian curriculum the platform targets rather than
/// just the subjects the demo teachers happen to teach. Existing ids are never reassigned:
/// a subject's id is referenced by teacher, group and course rows, and by URLs that are
/// already in the wild.
/// </summary>
internal static class SeedCatalog
{
    public static IReadOnlyList<Subject> Subjects =>
    [
        // --- Core secondary sciences and languages -------------------------------
        Subject("11111111-0000-0000-0000-000000000001", "الرياضيات", "Mathematics", "mathematics", "calculator", "#2563EB", 1,
            "الجبر والهندسة وحساب المثلثات والتفاضل والتكامل."),
        Subject("11111111-0000-0000-0000-000000000002", "الفيزياء", "Physics", "physics", "atom", "#7C3AED", 2,
            "الميكانيكا والكهربية والموجات والفيزياء الحديثة."),
        Subject("11111111-0000-0000-0000-000000000003", "الكيمياء", "Chemistry", "chemistry", "flask-conical", "#059669", 3,
            "الكيمياء العضوية وغير العضوية والتحليلية."),
        Subject("11111111-0000-0000-0000-000000000004", "الأحياء", "Biology", "biology", "dna", "#DC2626", 4,
            "علم الخلية والوراثة وعلم وظائف الأعضاء."),
        Subject("11111111-0000-0000-0000-000000000005", "اللغة العربية", "Arabic", "arabic", "book-open", "#D97706", 5,
            "النحو والبلاغة والأدب والنصوص والقراءة."),
        Subject("11111111-0000-0000-0000-000000000006", "اللغة الإنجليزية", "English", "english", "languages", "#0891B2", 6,
            "القواعد والمفردات والقطع والترجمة والمحادثة."),

        // --- Humanities branch --------------------------------------------------
        Subject("11111111-0000-0000-0000-000000000007", "التاريخ", "History", "history", "scroll", "#92400E", 7,
            "التاريخ المصري والعربي والحديث والمعاصر."),
        Subject("11111111-0000-0000-0000-000000000008", "الجغرافيا", "Geography", "geography", "globe", "#0F766E", 8,
            "الجغرافيا الطبيعية والبشرية والاقتصادية والخرائط."),
        Subject("11111111-0000-0000-0000-000000000009", "الفلسفة والمنطق", "Philosophy and Logic", "philosophy", "brain", "#6D28D9", 9,
            "الفلسفة العامة والمنطق الصوري والرمزي."),
        Subject("11111111-0000-0000-0000-00000000000a", "علم النفس والاجتماع", "Psychology and Sociology", "psychology", "users-round", "#BE185D", 10,
            "علم النفس العام وعلم الاجتماع للصف الثاني والثالث الثانوي."),

        // --- Secondary electives ------------------------------------------------
        Subject("11111111-0000-0000-0000-00000000000b", "الجيولوجيا وعلوم البيئة", "Geology and Environmental Science", "geology", "mountain", "#78350F", 11,
            "الجيولوجيا التركيبية والتاريخية وعلوم البيئة."),
        Subject("11111111-0000-0000-0000-00000000000c", "الإحصاء", "Statistics", "statistics", "chart-bar", "#1D4ED8", 12,
            "الإحصاء الوصفي والاحتمالات لشعبة الرياضيات."),
        Subject("11111111-0000-0000-0000-00000000000d", "اللغة الفرنسية", "French", "french", "languages", "#4338CA", 13,
            "اللغة الفرنسية كلغة أجنبية ثانية: القواعد والمحادثة."),
        Subject("11111111-0000-0000-0000-00000000000e", "اللغة الألمانية", "German", "german", "languages", "#B45309", 14,
            "اللغة الألمانية كلغة أجنبية ثانية: القواعد والمحادثة."),
        Subject("11111111-0000-0000-0000-00000000000f", "الحاسب الآلي وتكنولوجيا المعلومات", "Computer Science and IT", "computer-science", "monitor", "#0E7490", 15,
            "أساسيات الحاسب وقواعد البيانات والبرمجة."),

        // --- Primary and preparatory stages -------------------------------------
        Subject("11111111-0000-0000-0000-000000000010", "العلوم", "Science", "science", "microscope", "#047857", 16,
            "العلوم العامة للمرحلتين الابتدائية والإعدادية."),
        Subject("11111111-0000-0000-0000-000000000011", "الدراسات الاجتماعية", "Social Studies", "social-studies", "map", "#A16207", 17,
            "الدراسات الاجتماعية للمرحلتين الابتدائية والإعدادية."),
        Subject("11111111-0000-0000-0000-000000000012", "التربية الدينية", "Religious Education", "religious-education", "book-heart", "#15803D", 18,
            "التربية الإسلامية والتربية المسيحية.")
    ];

    public static IReadOnlyList<EducationalGrade> Grades =>
    [
        Grade("22222222-0000-0000-0000-000000000001", "المرحلة الابتدائية", "Primary", "primary", EducationalStage.Primary, 1),
        Grade("22222222-0000-0000-0000-000000000002", "المرحلة الإعدادية", "Preparatory", "preparatory", EducationalStage.Preparatory, 2),
        Grade("22222222-0000-0000-0000-000000000003", "الصف الأول الثانوي", "First Secondary", "first-secondary", EducationalStage.Secondary, 3),
        Grade("22222222-0000-0000-0000-000000000004", "الصف الثاني الثانوي", "Second Secondary", "second-secondary", EducationalStage.Secondary, 4),
        Grade("22222222-0000-0000-0000-000000000005", "الصف الثالث الثانوي", "Third Secondary", "third-secondary", EducationalStage.Secondary, 5)
    ];

    private static Subject Subject(
        string id, string nameAr, string nameEn, string slug, string icon, string color, int sortOrder, string description) =>
        new()
        {
            Id = Guid.Parse(id),
            NameAr = nameAr,
            NameEn = nameEn,
            Slug = slug,
            Icon = icon,
            ColorHex = color,
            SortOrder = sortOrder,
            Description = description,
            IsActive = true
        };

    private static EducationalGrade Grade(
        string id, string nameAr, string nameEn, string slug, EducationalStage stage, int sortOrder) =>
        new()
        {
            Id = Guid.Parse(id),
            NameAr = nameAr,
            NameEn = nameEn,
            Slug = slug,
            Stage = stage,
            SortOrder = sortOrder,
            IsActive = true
        };
}
