using Microsoft.EntityFrameworkCore;
using Shouldly;
using Ustaz.Application.Features.Billing;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Entities.Billing;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Tests.Handlers;

/// <summary>
/// A workspace owner should learn that their plan is about to lapse before it does, and that
/// they are running out of headroom before a create call fails. Each warning is keyed to the
/// period or month it refers to, so a daily pass does not become a daily nag.
/// </summary>
public class SubscriptionReminderTests : IDisposable
{
    private readonly TestHarness _harness = new();

    private DispatchSubscriptionRemindersCommandHandler Handler() =>
        new(_harness.Context, _harness.PlanLimits, _harness.Clock, _harness.RealNotifications);

    private async Task<TenantSubscription> SubscriptionAsync() =>
        await _harness.Context.TenantSubscriptions
            .FirstAsync(subscription => subscription.TenantId == _harness.Tenant.Id);

    private Guid OwnerUserId => _harness.Teacher.UserId;

    [Fact]
    public async Task An_owner_is_warned_before_the_period_ends()
    {
        var subscription = await SubscriptionAsync();
        subscription.CurrentPeriodEnd = _harness.UtcNow.AddDays(3);
        await _harness.Context.SaveChangesAsync();

        await Handler().Handle(new DispatchSubscriptionRemindersCommand(), CancellationToken.None);

        var notification = await _harness.Context.Notifications
            .FirstAsync(entry => entry.Event == "subscription.expiring");

        notification.UserId.ShouldBe(OwnerUserId);
        notification.Title.ShouldBe("الاشتراك قرب يخلص");
    }

    [Fact]
    public async Task A_period_that_is_still_far_off_says_nothing()
    {
        var subscription = await SubscriptionAsync();
        subscription.CurrentPeriodEnd = _harness.UtcNow.AddDays(40);
        await _harness.Context.SaveChangesAsync();

        await Handler().Handle(new DispatchSubscriptionRemindersCommand(), CancellationToken.None);

        (await _harness.Context.Notifications.CountAsync(entry => entry.Event == "subscription.expiring"))
            .ShouldBe(0);
    }

    [Fact]
    public async Task A_lapsed_period_is_reported_as_expired_rather_than_expiring()
    {
        var subscription = await SubscriptionAsync();
        subscription.CurrentPeriodEnd = _harness.UtcNow.AddDays(-1);
        await _harness.Context.SaveChangesAsync();

        await Handler().Handle(new DispatchSubscriptionRemindersCommand(), CancellationToken.None);

        (await _harness.Context.Notifications.CountAsync(entry => entry.Event == "subscription.expired"))
            .ShouldBe(1);

        (await _harness.Context.Notifications.CountAsync(entry => entry.Event == "subscription.expiring"))
            .ShouldBe(0);
    }

    [Fact]
    public async Task The_warning_is_not_repeated_on_the_next_pass()
    {
        var subscription = await SubscriptionAsync();
        subscription.CurrentPeriodEnd = _harness.UtcNow.AddDays(3);
        await _harness.Context.SaveChangesAsync();

        var handler = Handler();

        await handler.Handle(new DispatchSubscriptionRemindersCommand(), CancellationToken.None);
        await handler.Handle(new DispatchSubscriptionRemindersCommand(), CancellationToken.None);

        (await _harness.Context.Notifications.CountAsync(entry => entry.Event == "subscription.expiring"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Approaching_a_quota_is_mentioned_once_per_month()
    {
        // The seeded plan allows 100 students; put the workspace at 85.
        var plan = await _harness.Context.SubscriptionPlans.FirstAsync();
        plan.MaxStudents = 10;
        await _harness.Context.SaveChangesAsync();

        for (var index = 0; index < 8; index++)
        {
            _harness.Context.TenantStudents.Add(new TenantStudent
            {
                Id = Guid.NewGuid(),
                TenantId = _harness.Tenant.Id,
                StudentId = Guid.NewGuid(),
                IsActive = true,
                JoinedAt = _harness.UtcNow
            });
        }

        await _harness.Context.SaveChangesAsync();

        var handler = Handler();

        await handler.Handle(new DispatchSubscriptionRemindersCommand(), CancellationToken.None);
        await handler.Handle(new DispatchSubscriptionRemindersCommand(), CancellationToken.None);

        var warnings = await _harness.Context.Notifications
            .Where(entry => entry.Event == "subscription.usage_limit_approaching")
            .ToListAsync();

        warnings.Count.ShouldBe(1);
        warnings[0].Message.ShouldContain("طالب");
    }

    [Fact]
    public async Task Hitting_a_quota_is_reported_as_reached_rather_than_approaching()
    {
        var plan = await _harness.Context.SubscriptionPlans.FirstAsync();
        plan.MaxStudents = 2;
        await _harness.Context.SaveChangesAsync();

        // The harness already seeds one workspace student; add the second to fill the plan.
        _harness.Context.TenantStudents.Add(new TenantStudent
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.Tenant.Id,
            StudentId = Guid.NewGuid(),
            IsActive = true,
            JoinedAt = _harness.UtcNow
        });

        await _harness.Context.SaveChangesAsync();

        await Handler().Handle(new DispatchSubscriptionRemindersCommand(), CancellationToken.None);

        (await _harness.Context.Notifications.CountAsync(entry => entry.Event == "subscription.usage_limit_reached"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task An_unlimited_plan_produces_no_quota_noise()
    {
        var plan = await _harness.Context.SubscriptionPlans.FirstAsync();
        plan.MaxStudents = null;
        plan.MaxGroups = null;
        plan.MaxCourses = null;
        await _harness.Context.SaveChangesAsync();

        await Handler().Handle(new DispatchSubscriptionRemindersCommand(), CancellationToken.None);

        (await _harness.Context.Notifications.CountAsync(entry => entry.Event!.StartsWith("subscription.usage")))
            .ShouldBe(0);
    }

    public void Dispose() => _harness.Dispose();
}
