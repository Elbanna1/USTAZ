using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Billing;

namespace Ustaz.Persistence.Seeding;

/// <summary>
/// The USTAZ SaaS price list. Fixed ids keep seeding idempotent, and operator edits to
/// pricing survive re-seeding because existing rows are never overwritten.
/// </summary>
internal static class PlanCatalog
{
    public static IReadOnlyList<SubscriptionPlan> Plans =>
    [
        Build(
            "33333333-0000-0000-0000-000000000001",
            "starter", "المبتدئ", "Starter",
            "للمدرس الفردي في بداية الطريق.",
            monthlyPrice: 0m, yearlyPrice: 0m, trialDays: 14, sortOrder: 1,
            maxStudents: 30, maxGroups: 3, maxCourses: 1,
            maxAssistants: 0, maxTeamMembers: 1, maxStorageMb: 512,
            features:
            [
                PlanFeatures.Attendance,
                PlanFeatures.Announcements
            ]),

        Build(
            "33333333-0000-0000-0000-000000000002",
            "professional", "الاحترافي", "Professional",
            "للمدرس الذي يدير عدة مجموعات ويحتاج فريق عمل.",
            monthlyPrice: 499m, yearlyPrice: 4990m, trialDays: 14, sortOrder: 2,
            maxStudents: 300, maxGroups: 20, maxCourses: 10,
            maxAssistants: 3, maxTeamMembers: 5, maxStorageMb: 5120,
            features:
            [
                PlanFeatures.Attendance,
                PlanFeatures.Exams,
                PlanFeatures.Homework,
                PlanFeatures.Messaging,
                PlanFeatures.Announcements,
                PlanFeatures.WebsiteCustomization,
                PlanFeatures.EducationalPayments
            ]),

        Build(
            "33333333-0000-0000-0000-000000000003",
            "center", "السنتر التعليمي", "Educational Centre",
            "للسناتر التعليمية متعددة المدرسين مع نطاق مخصص وتحليلات متقدمة.",
            monthlyPrice: 1499m, yearlyPrice: 14990m, trialDays: 14, sortOrder: 3,
            maxStudents: null, maxGroups: null, maxCourses: null,
            maxAssistants: 25, maxTeamMembers: 40, maxStorageMb: 51200,
            features:
            [
                PlanFeatures.Attendance,
                PlanFeatures.Exams,
                PlanFeatures.Homework,
                PlanFeatures.Messaging,
                PlanFeatures.Announcements,
                PlanFeatures.WebsiteCustomization,
                PlanFeatures.EducationalPayments,
                PlanFeatures.CustomDomain,
                PlanFeatures.AdvancedAnalytics
            ])
    ];

    private static SubscriptionPlan Build(
        string id,
        string code,
        string nameAr,
        string nameEn,
        string description,
        decimal monthlyPrice,
        decimal? yearlyPrice,
        int trialDays,
        int sortOrder,
        int? maxStudents,
        int? maxGroups,
        int? maxCourses,
        int? maxAssistants,
        int? maxTeamMembers,
        int? maxStorageMb,
        string[] features)
    {
        var planId = Guid.Parse(id);

        var plan = new SubscriptionPlan
        {
            Id = planId,
            Code = code,
            NameAr = nameAr,
            NameEn = nameEn,
            Description = description,
            MonthlyPrice = monthlyPrice,
            YearlyPrice = yearlyPrice,
            Currency = "EGP",
            TrialDays = trialDays,
            SortOrder = sortOrder,
            IsActive = true,
            IsPublic = true,
            MaxStudents = maxStudents,
            MaxGroups = maxGroups,
            MaxCourses = maxCourses,
            MaxAssistants = maxAssistants,
            MaxTeamMembers = maxTeamMembers,
            MaxStorageMegabytes = maxStorageMb
        };

        foreach (var feature in features)
        {
            // Derived from the plan code and the feature key, not random: the reference seeder
            // reconciles features against plans that already exist, which needs the same id to
            // come back on every run rather than a new row each time.
            plan.Features.Add(new PlanFeature
            {
                Id = SeedId.For("plan-feature", code + ":" + feature),
                SubscriptionPlanId = planId,
                FeatureKey = feature,
                IsEnabled = true
            });
        }

        return plan;
    }
}
