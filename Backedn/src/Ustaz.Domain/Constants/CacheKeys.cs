namespace Ustaz.Domain.Constants;

/// <summary>Centralised cache key factory so invalidation stays consistent.</summary>
public static class CacheKeys
{
    public const string SubjectsAll = "subjects:all";
    public const string GradesAll = "grades:all";
    public const string FeaturedTeachers = "teachers:featured";
    public const string FeaturedGroups = "groups:featured";
    public const string PopularSubjects = "subjects:popular";

    public static string TeacherProfile(Guid teacherId) => $"teacher:{teacherId}:profile";
    public static string TeacherDashboard(Guid teacherId) => $"teacher:{teacherId}:dashboard";
    public static string StudentDashboard(Guid studentId) => $"student:{studentId}:dashboard";
    public static string GroupDetails(Guid groupId) => $"group:{groupId}:details";
    public static string CourseDetails(Guid courseId) => $"course:{courseId}:details";
    public static string TeacherListPage(string signature) => $"teachers:list:{signature}";

    public const string TeachersTag = "teachers";
    public const string GroupsTag = "groups";
    public const string CoursesTag = "courses";
}
