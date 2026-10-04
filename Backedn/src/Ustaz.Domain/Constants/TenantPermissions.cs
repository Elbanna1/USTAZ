using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Constants;

/// <summary>
/// Granular capabilities that can be granted to a workspace member. These are tenant scoped
/// and are always resolved server-side from <c>TenantMemberPermissions</c>; a permission
/// supplied by the client is never trusted.
/// </summary>
public static class TenantPermissions
{
    public const string StudentsView = "Students.View";
    public const string StudentsManage = "Students.Manage";

    public const string GroupsView = "Groups.View";
    public const string GroupsManage = "Groups.Manage";

    public const string CoursesView = "Courses.View";
    public const string CoursesManage = "Courses.Manage";

    public const string ScheduleView = "Schedule.View";
    public const string ScheduleManage = "Schedule.Manage";

    public const string AttendanceView = "Attendance.View";
    public const string AttendanceManage = "Attendance.Manage";

    public const string ExamsView = "Exams.View";
    public const string ExamsManage = "Exams.Manage";

    public const string GradesView = "Grades.View";
    public const string GradesManage = "Grades.Manage";

    public const string HomeworkView = "Homework.View";
    public const string HomeworkManage = "Homework.Manage";

    public const string AnnouncementsView = "Announcements.View";
    public const string AnnouncementsManage = "Announcements.Manage";

    public const string MessagesView = "Messages.View";
    public const string MessagesManage = "Messages.Manage";

    public const string PaymentsView = "Payments.View";
    public const string PaymentsManage = "Payments.Manage";

    public const string AnalyticsView = "Analytics.View";

    public const string WebsiteView = "Website.View";
    public const string WebsiteManage = "Website.Manage";

    public const string DiscountsView = "Discounts.View";
    public const string DiscountsManage = "Discounts.Manage";

    public const string NotificationsView = "Notifications.View";
    public const string NotificationsManage = "Notifications.Manage";

    /// <summary>Managing the workspace itself: settings, branding, domains.</summary>
    public const string TenantManage = "Tenant.Manage";

    /// <summary>Inviting, removing and re-roling workspace members.</summary>
    public const string TeamManage = "Team.Manage";

    /// <summary>Managing the USTAZ SaaS subscription (checkout, plan changes, cancellation).</summary>
    public const string BillingManage = "Billing.Manage";

    public const string BillingView = "Billing.View";

    /// <summary>Every permission the system knows about. Used to validate grants.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        StudentsView, StudentsManage,
        GroupsView, GroupsManage,
        CoursesView, CoursesManage,
        ScheduleView, ScheduleManage,
        AttendanceView, AttendanceManage,
        ExamsView, ExamsManage,
        GradesView, GradesManage,
        HomeworkView, HomeworkManage,
        AnnouncementsView, AnnouncementsManage,
        MessagesView, MessagesManage,
        PaymentsView, PaymentsManage,
        AnalyticsView,
        WebsiteView, WebsiteManage,
        DiscountsView, DiscountsManage,
        NotificationsView, NotificationsManage,
        TenantManage, TeamManage, BillingManage, BillingView
    };

    /// <summary>
    /// Permissions that control the workspace itself. An assistant can never hold these,
    /// which is what stops privilege escalation and ownership transfer.
    /// </summary>
    public static readonly IReadOnlySet<string> OwnerOnly = new HashSet<string>(StringComparer.Ordinal)
    {
        TenantManage, TeamManage, BillingManage
    };

    /// <summary>The set a member of the given workspace role implicitly holds.</summary>
    public static IReadOnlySet<string> ForRole(TenantMemberRole role) => role switch
    {
        // The owner holds everything, unconditionally.
        TenantMemberRole.Owner => All,

        // A workspace admin runs day-to-day operations but never billing or ownership.
        TenantMemberRole.Admin => new HashSet<string>(All.Except(new[] { TenantManage, BillingManage }), StringComparer.Ordinal),

        // Moderators curate public-facing content and community.
        TenantMemberRole.Moderator => new HashSet<string>(StringComparer.Ordinal)
        {
            StudentsView, GroupsView, CoursesView, ScheduleView,
            AnnouncementsView, AnnouncementsManage,
            MessagesView, MessagesManage,
            AnalyticsView, WebsiteView
        },

        // Content managers own the website and catalogue content.
        TenantMemberRole.ContentManager => new HashSet<string>(StringComparer.Ordinal)
        {
            GroupsView, GroupsManage, CoursesView, CoursesManage,
            ScheduleView, ScheduleManage,
            WebsiteView, WebsiteManage,
            AnnouncementsView, AnnouncementsManage,
            AnalyticsView
        },

        // Assistants start with nothing; the owner grants each capability explicitly.
        TenantMemberRole.Assistant => new HashSet<string>(StringComparer.Ordinal),

        _ => new HashSet<string>(StringComparer.Ordinal)
    };

    public static bool IsKnown(string permission) => All.Contains(permission);
}
