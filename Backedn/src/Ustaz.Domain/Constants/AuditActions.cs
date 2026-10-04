namespace Ustaz.Domain.Constants;

/// <summary>Canonical audit action names so log queries stay stable.</summary>
public static class AuditActions
{
    public const string Login = "auth.login";
    public const string LoginFailed = "auth.login_failed";
    public const string PasswordReset = "auth.password_reset";

    public const string StudentCreated = "student.created";
    public const string StudentUpdated = "student.updated";
    public const string StudentRemoved = "student.removed";

    /// <summary>A workspace member enrolled a student into a group on their behalf.</summary>
    public const string StudentEnrolled = "student.enrolled";

    public const string AttendanceRecorded = "attendance.recorded";
    public const string AttendanceUpdated = "attendance.updated";

    public const string GradeRecorded = "grade.recorded";
    public const string GradeUpdated = "grade.updated";

    public const string HomeworkCreated = "homework.created";
    public const string HomeworkGraded = "homework.graded";
    public const string HomeworkPublished = "homework.published";
    public const string HomeworkClosed = "homework.closed";
    public const string HomeworkReturned = "homework.returned";
    public const string HomeworkBulkGraded = "homework.bulk_graded";

    public const string PaymentRecorded = "payment.recorded";
    public const string PaymentRefunded = "payment.refunded";

    public const string SubscriptionChanged = "subscription.changed";
    public const string SubscriptionCanceled = "subscription.canceled";
    public const string SubscriptionReactivated = "subscription.reactivated";

    public const string MemberInvited = "team.member_invited";
    public const string MemberRoleChanged = "team.member_role_changed";
    public const string MemberStatusChanged = "team.member_status_changed";
    public const string MemberRemoved = "team.member_removed";
    public const string PermissionsChanged = "team.permissions_changed";

    public const string TenantSettingsChanged = "tenant.settings_changed";
    public const string TenantBrandingChanged = "tenant.branding_changed";
    public const string TenantDomainChanged = "tenant.domain_changed";
    public const string WebsiteChanged = "tenant.website_changed";

    public const string AdminTeacherVerified = "admin.teacher_verified";

    /// <summary>Platform-level authority changed hands. The most sensitive action there is.</summary>
    public const string PlatformAdminGranted = "platform.admin_granted";

    public const string PlatformAdminRevoked = "platform.admin_revoked";
    public const string AdminUserStateChanged = "admin.user_state_changed";
    public const string AdminReviewModerated = "admin.review_moderated";
}
