using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Entities.Billing;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Common.Security;

/// <inheritdoc />
public sealed class PlanLimitService(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IPlanLimitService
{
    public async Task EnsureQuotaAsync(
        Guid tenantId,
        PlanQuota quota,
        int additional = 1,
        CancellationToken cancellationToken = default)
    {
        var usage = await GetUsageAsync(tenantId, quota, cancellationToken);

        if (usage.IsUnlimited)
        {
            return;
        }

        if (usage.Used + additional > usage.Limit!.Value)
        {
            throw new BusinessRuleException(
                "PLAN_LIMIT_REACHED",
                "Your plan allows " + usage.Limit.Value + " " + Describe(quota) + " and you are using "
                + usage.Used + ". Upgrade your plan to add more.");
        }
    }

    public async Task EnsureFeatureAsync(
        Guid tenantId,
        string featureKey,
        CancellationToken cancellationToken = default)
    {
        if (!await HasFeatureAsync(tenantId, featureKey, cancellationToken))
        {
            throw new BusinessRuleException(
                "FEATURE_NOT_AVAILABLE",
                "Your current plan does not include this feature. Upgrade to enable it.");
        }
    }

    public async Task EnsureActiveSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var subscription = await LoadSubscriptionAsync(tenantId, cancellationToken);

        if (subscription is null)
        {
            throw new BusinessRuleException(
                "SUBSCRIPTION_REQUIRED",
                "This workspace has no active USTAZ subscription.");
        }

        if (!subscription.IsUsable(dateTimeProvider.UtcNow))
        {
            throw new BusinessRuleException(
                "SUBSCRIPTION_INACTIVE",
                "Your USTAZ subscription is not active. Renew it to continue using this feature.");
        }
    }

    public async Task<PlanQuotaUsage> GetUsageAsync(
        Guid tenantId,
        PlanQuota quota,
        CancellationToken cancellationToken = default)
    {
        var subscription = await LoadSubscriptionAsync(tenantId, cancellationToken);

        return await BuildUsageAsync(tenantId, quota, subscription?.Plan, cancellationToken);
    }

    public async Task<IReadOnlyList<PlanQuotaUsage>> GetAllUsageAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // The plan is the same for every quota, so it is read once rather than per quota.
        var subscription = await LoadSubscriptionAsync(tenantId, cancellationToken);
        var quotas = Enum.GetValues<PlanQuota>();
        var results = new List<PlanQuotaUsage>(quotas.Length);

        foreach (var quota in quotas)
        {
            results.Add(await BuildUsageAsync(tenantId, quota, subscription?.Plan, cancellationToken));
        }

        return results;
    }

    private async Task<PlanQuotaUsage> BuildUsageAsync(
        Guid tenantId,
        PlanQuota quota,
        SubscriptionPlan? plan,
        CancellationToken cancellationToken)
    {
        var limit = quota switch
        {
            PlanQuota.Students => plan?.MaxStudents,
            PlanQuota.Groups => plan?.MaxGroups,
            PlanQuota.Courses => plan?.MaxCourses,
            PlanQuota.Assistants => plan?.MaxAssistants,
            PlanQuota.TeamMembers => plan?.MaxTeamMembers,
            _ => null
        };

        var used = await CountAsync(tenantId, quota, cancellationToken);

        return new PlanQuotaUsage { Quota = quota, Limit = limit, Used = used };
    }

    public async Task<bool> HasFeatureAsync(
        Guid tenantId,
        string featureKey,
        CancellationToken cancellationToken = default)
    {
        var subscription = await LoadSubscriptionAsync(tenantId, cancellationToken);

        if (subscription is null)
        {
            return false;
        }

        // An expired subscription loses its premium features, not just its quotas.
        if (!subscription.IsUsable(dateTimeProvider.UtcNow))
        {
            return false;
        }

        return subscription.Plan.Features.Any(feature =>
            feature.IsEnabled && string.Equals(feature.FeatureKey, featureKey, StringComparison.Ordinal));
    }

    private Task<TenantSubscription?> LoadSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken) =>
        context.TenantSubscriptions
            .AsNoTracking()
            .Include(subscription => subscription.Plan)
                .ThenInclude(plan => plan.Features)
            .FirstOrDefaultAsync(subscription => subscription.TenantId == tenantId, cancellationToken);

    private Task<int> CountAsync(Guid tenantId, PlanQuota quota, CancellationToken cancellationToken) => quota switch
    {
        PlanQuota.Students => context.TenantStudents
            .AsNoTracking()
            .CountAsync(link => link.TenantId == tenantId && link.IsActive, cancellationToken),

        PlanQuota.Groups => context.Groups
            .AsNoTracking()
            .CountAsync(group => group.TenantId == tenantId && group.IsActive, cancellationToken),

        PlanQuota.Courses => context.Courses
            .AsNoTracking()
            .CountAsync(course => course.TenantId == tenantId, cancellationToken),

        PlanQuota.Assistants => context.TenantMembers
            .AsNoTracking()
            .CountAsync(
                member => member.TenantId == tenantId
                    && member.Role == TenantMemberRole.Assistant
                    && (member.Status == TenantMemberStatus.Active || member.Status == TenantMemberStatus.Invited),
                cancellationToken),

        // The owner occupies a seat, so the count deliberately includes them.
        PlanQuota.TeamMembers => context.TenantMembers
            .AsNoTracking()
            .CountAsync(
                member => member.TenantId == tenantId
                    && (member.Status == TenantMemberStatus.Active || member.Status == TenantMemberStatus.Invited),
                cancellationToken),

        _ => Task.FromResult(0)
    };

    private static string Describe(PlanQuota quota) => quota switch
    {
        PlanQuota.Students => "students",
        PlanQuota.Groups => "groups",
        PlanQuota.Courses => "courses",
        PlanQuota.Assistants => "assistants",
        PlanQuota.TeamMembers => "team members",
        _ => "items"
    };
}
