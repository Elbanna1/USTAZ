namespace Ustaz.Domain.Enums;

/// <summary>
/// Role a user holds inside one workspace. Distinct from the platform level
/// <see cref="Ustaz.Domain.Constants.RoleNames"/>, which govern the whole of USTAZ.
/// </summary>
public enum TenantMemberRole
{
    Owner = 1,
    Admin = 2,
    Assistant = 3,
    Moderator = 4,
    ContentManager = 5
}
