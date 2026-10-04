namespace Ustaz.Domain.Constants;

/// <summary>Canonical role names. Never use raw strings for roles anywhere else.</summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
    public const string Student = "Student";

    /// <summary>A guardian who follows one or more linked students.</summary>
    public const string Parent = "Parent";

    public static readonly IReadOnlyList<string> All = [Admin, Teacher, Student, Parent];
}
