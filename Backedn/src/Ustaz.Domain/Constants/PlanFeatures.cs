namespace Ustaz.Domain.Constants;

/// <summary>Boolean capabilities a SaaS plan can switch on for a tenant.</summary>
public static class PlanFeatures
{
    public const string CustomDomain = "custom_domain";
    public const string AdvancedAnalytics = "advanced_analytics";
    public const string Messaging = "messaging";
    public const string Attendance = "attendance";
    public const string Exams = "exams";
    public const string Homework = "homework";
    public const string WebsiteCustomization = "website_customization";
    public const string Announcements = "announcements";
    public const string EducationalPayments = "educational_payments";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        CustomDomain, AdvancedAnalytics, Messaging, Attendance, Exams,
        Homework, WebsiteCustomization, Announcements, EducationalPayments
    };
}
