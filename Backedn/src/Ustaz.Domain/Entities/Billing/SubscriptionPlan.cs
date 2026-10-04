using Ustaz.Domain.Common;
using Ustaz.Domain.Constants;

namespace Ustaz.Domain.Entities.Billing;

/// <summary>
/// A USTAZ SaaS plan. Limits are stored as explicit columns because they are queried on every
/// quota check; boolean capabilities live in <see cref="Features"/>.
/// A limit of <c>null</c> means unlimited.
/// </summary>
public class SubscriptionPlan : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal MonthlyPrice { get; set; }

    public decimal? YearlyPrice { get; set; }

    public string Currency { get; set; } = "EGP";

    public int TrialDays { get; set; } = 14;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsPublic { get; set; } = true;

    // --- Quotas (null == unlimited) --------------------------------------
    public int? MaxStudents { get; set; }

    public int? MaxGroups { get; set; }

    public int? MaxCourses { get; set; }

    public int? MaxAssistants { get; set; }

    public int? MaxTeamMembers { get; set; }

    public int? MaxStorageMegabytes { get; set; }

    public ICollection<PlanFeature> Features { get; set; } = [];

    public ICollection<TenantSubscription> Subscriptions { get; set; } = [];

    public bool HasFeature(string featureKey) =>
        Features.Any(feature => feature.IsEnabled
            && string.Equals(feature.FeatureKey, featureKey, StringComparison.Ordinal));

    /// <summary>Price for the requested cycle; yearly falls back to 12x monthly when unset.</summary>
    public decimal PriceFor(Enums.BillingCycle cycle) => cycle switch
    {
        Enums.BillingCycle.Yearly => YearlyPrice ?? MonthlyPrice * 12m,
        _ => MonthlyPrice
    };

    public bool IsFree => MonthlyPrice <= 0m;
}

/// <summary>A boolean capability switched on for a plan, keyed by <see cref="PlanFeatures"/>.</summary>
public class PlanFeature : BaseEntity
{
    public Guid SubscriptionPlanId { get; set; }

    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;

    public string FeatureKey { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    /// <summary>Optional human readable value shown on the pricing page, e.g. "حتى 5 مساعدين".</summary>
    public string? DisplayValue { get; set; }
}
