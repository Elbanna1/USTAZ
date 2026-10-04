using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Billing;

/// <summary>
/// Warns a workspace owner before their subscription lapses, once it has, and when they are
/// running out of plan headroom.
///
/// Subscription state is evaluated lazily everywhere else — a lapsed plan is refused at the
/// point of use — but nobody enjoys finding that out mid-lesson. Each warning is keyed on the
/// period or the month it refers to, so the owner hears about a given lapse exactly once.
/// </summary>
public sealed record DispatchSubscriptionRemindersCommand : IRequest<int>
{
    /// <summary>How many days before the period ends to warn.</summary>
    public int WarnWithinDays { get; init; } = 7;

    /// <summary>Percentage of a quota at which to mention it, e.g. 80%.</summary>
    public int UsageWarningPercentage { get; init; } = 80;
}

public sealed class DispatchSubscriptionRemindersCommandHandler(
    IApplicationDbContext context,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications)
    : IRequestHandler<DispatchSubscriptionRemindersCommand, int>
{
    public async Task<int> Handle(
        DispatchSubscriptionRemindersCommand command,
        CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;
        var horizon = utcNow.AddDays(Math.Clamp(command.WarnWithinDays, 1, 60));

        // One projection over live subscriptions; the owner id comes along for the ride.
        var subscriptions = await context.TenantSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.Status == SubscriptionStatus.Active
                || subscription.Status == SubscriptionStatus.Trial
                || subscription.Status == SubscriptionStatus.PastDue)
            .Select(subscription => new
            {
                subscription.Id,
                subscription.TenantId,
                subscription.Status,
                subscription.CurrentPeriodEnd,
                PlanName = subscription.Plan.NameAr,
                OwnerUserId = context.Tenants
                    .Where(tenant => tenant.Id == subscription.TenantId)
                    .Select(tenant => (Guid?)tenant.OwnerUserId)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var sent = 0;

        foreach (var subscription in subscriptions)
        {
            if (subscription.OwnerUserId is null)
            {
                continue;
            }

            var period = subscription.CurrentPeriodEnd.ToString("yyyyMMdd");

            if (subscription.CurrentPeriodEnd < utcNow)
            {
                sent += await notifications.SendAsync(
                    subscription.OwnerUserId.Value,
                    new NotificationRequest
                    {
                        Event = NotificationEvents.SubscriptionExpired,
                        TenantId = subscription.TenantId,
                        ReferenceId = subscription.Id,
                        DeduplicationKey = "subscription-expired:" + subscription.Id + ":" + period,
                        Values = new Dictionary<string, string> { ["plan"] = subscription.PlanName }
                    },
                    cancellationToken)
                    ? 1
                    : 0;

                continue;
            }

            if (subscription.CurrentPeriodEnd <= horizon)
            {
                sent += await notifications.SendAsync(
                    subscription.OwnerUserId.Value,
                    new NotificationRequest
                    {
                        Event = NotificationEvents.SubscriptionExpiring,
                        TenantId = subscription.TenantId,
                        ReferenceId = subscription.Id,
                        DeduplicationKey = "subscription-expiring:" + subscription.Id + ":" + period,
                        Values = new Dictionary<string, string>
                        {
                            ["plan"] = subscription.PlanName,
                            ["date"] = subscription.CurrentPeriodEnd.ToString("yyyy-MM-dd")
                        }
                    },
                    cancellationToken)
                    ? 1
                    : 0;
            }

            sent += await WarnAboutQuotasAsync(
                subscription.TenantId,
                subscription.OwnerUserId.Value,
                command.UsageWarningPercentage,
                utcNow,
                cancellationToken);
        }

        return sent;
    }

    /// <summary>
    /// Mentions a quota the workspace is close to, or has hit. Keyed per month so a workspace
    /// sitting at its ceiling is told once, not every time the pass runs.
    /// </summary>
    private async Task<int> WarnAboutQuotasAsync(
        Guid tenantId,
        Guid ownerUserId,
        int warningPercentage,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var month = utcNow.ToString("yyyyMM");
        var threshold = Math.Clamp(warningPercentage, 50, 99);
        var sent = 0;

        foreach (var quota in new[] { PlanQuota.Students, PlanQuota.Groups, PlanQuota.Courses })
        {
            var usage = await planLimits.GetUsageAsync(tenantId, quota, cancellationToken);

            if (usage.IsUnlimited || usage.Limit is not { } limit || limit <= 0)
            {
                continue;
            }

            var resource = Describe(quota);

            if (usage.Used >= limit)
            {
                sent += await notifications.SendAsync(
                    ownerUserId,
                    new NotificationRequest
                    {
                        Event = NotificationEvents.UsageLimitReached,
                        TenantId = tenantId,
                        DeduplicationKey = "quota-reached:" + tenantId + ":" + quota + ":" + month,
                        Values = new Dictionary<string, string>
                        {
                            ["limit"] = limit.ToString(),
                            ["resource"] = resource
                        }
                    },
                    cancellationToken)
                    ? 1
                    : 0;

                continue;
            }

            if (usage.Used * 100 / limit >= threshold)
            {
                sent += await notifications.SendAsync(
                    ownerUserId,
                    new NotificationRequest
                    {
                        Event = NotificationEvents.UsageLimitApproaching,
                        TenantId = tenantId,
                        DeduplicationKey = "quota-near:" + tenantId + ":" + quota + ":" + month,
                        Values = new Dictionary<string, string>
                        {
                            ["used"] = usage.Used.ToString(),
                            ["limit"] = limit.ToString(),
                            ["resource"] = resource
                        }
                    },
                    cancellationToken)
                    ? 1
                    : 0;
            }
        }

        return sent;
    }

    private static string Describe(PlanQuota quota) => quota switch
    {
        PlanQuota.Students => "طالب",
        PlanQuota.Groups => "مجموعة",
        PlanQuota.Courses => "كورس",
        PlanQuota.Assistants => "مساعد",
        _ => "عنصر"
    };
}
