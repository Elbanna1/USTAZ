namespace Ustaz.Domain.Constants;

public static class PolicyNames
{
    public const string AdminOnly = "AdminOnly";
    public const string TeacherOnly = "TeacherOnly";
    public const string StudentOnly = "StudentOnly";
    public const string VerifiedTeacherOnly = "VerifiedTeacherOnly";
    public const string TeacherOrAdmin = "TeacherOrAdmin";
    public const string AuthenticatedUser = "AuthenticatedUser";

    /// <summary>Caller holds the platform Parent role.</summary>
    public const string ParentOnly = "ParentOnly";

    /// <summary>Caller is acting inside a workspace (any member role).</summary>
    public const string TenantMember = "TenantMember";
}
