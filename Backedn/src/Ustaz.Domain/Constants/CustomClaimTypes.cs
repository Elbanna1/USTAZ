namespace Ustaz.Domain.Constants;

/// <summary>Application specific JWT claim types.</summary>
public static class CustomClaimTypes
{
    public const string UserId = "uid";
    public const string TeacherId = "teacherId";
    public const string StudentId = "studentId";
    public const string DisplayName = "displayName";
    public const string IsVerified = "isVerified";
    public const string Permission = "permission";

    /// <summary>Parent profile id when the caller holds the Parent role.</summary>
    public const string ParentId = "parentId";

    /// <summary>Workspace the caller is currently acting inside.</summary>
    public const string TenantId = "tenantId";

    /// <summary>Slug of the active workspace, handy for the client to build links.</summary>
    public const string TenantSlug = "tenantSlug";

    /// <summary>The caller's role inside the active workspace (Owner, Assistant, ...).</summary>
    public const string TenantRole = "tenantRole";
}
