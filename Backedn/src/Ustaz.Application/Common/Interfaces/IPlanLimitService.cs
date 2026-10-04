namespace Ustaz.Application.Common.Interfaces;

/// <summary>Quota kinds a plan can cap.</summary>
public enum PlanQuota
{
    Students = 1,
    Groups = 2,
    Courses = 3,
    Assistants = 4,
    TeamMembers = 5
}

/// <summary>Snapshot of one quota: what the plan allows and what the tenant already uses.</summary>
public sealed record PlanQuotaUsage
{
    public required PlanQuota Quota { get; init; }

    /// <summary>Null means unlimited.</summary>
    public int? Limit { get; init; }

    public required int Used { get; init; }

    public bool IsUnlimited => Limit is null;

    public int? Remaining => Limit is null ? null : Math.Max(0, Limit.Value - Used);

    public bool HasRoom => Limit is null || Used < Limit.Value;
}

/// <summary>
/// Server-side enforcement of SaaS plan limits and feature flags.
///
/// Every quota check runs in the application layer against live counts, so a client calling
/// the API directly hits exactly the same wall as one using the UI.
/// </summary>
public interface IPlanLimitService
{
    /// <summary>
    /// Throws <c>PLAN_LIMIT_REACHED</c> when adding <paramref name="additional"/> more of the
    /// given resource would exceed the tenant's plan.
    /// </summary>
    Task EnsureQuotaAsync(
        Guid tenantId,
        PlanQuota quota,
        int additional = 1,
        CancellationToken cancellationToken = default);

    /// <summary>Throws <c>FEATURE_NOT_AVAILABLE</c> when the tenant's plan lacks the feature.</summary>
    Task EnsureFeatureAsync(
        Guid tenantId,
        string featureKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws when the tenant has no usable subscription (expired, or past the grace period).
    /// Read-only endpoints deliberately do not call this, so a lapsed tenant can still see
    /// their data and pay.
    /// </summary>
    Task EnsureActiveSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<PlanQuotaUsage> GetUsageAsync(
        Guid tenantId,
        PlanQuota quota,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanQuotaUsage>> GetAllUsageAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> HasFeatureAsync(Guid tenantId, string featureKey, CancellationToken cancellationToken = default);
}
