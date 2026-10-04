using Shouldly;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Tests.Domain;

public class DiscountTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Discount Build(
        DiscountType type = DiscountType.Percentage,
        decimal value = 10m,
        decimal? minimumAmount = null,
        decimal? maximumDiscount = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? usageLimit = null,
        int usedCount = 0,
        bool isActive = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            Code = "WELCOME10",
            Type = type,
            Value = value,
            MinimumAmount = minimumAmount,
            MaximumDiscount = maximumDiscount,
            StartDate = startDate ?? Now.AddDays(-10),
            EndDate = endDate,
            UsageLimit = usageLimit,
            UsedCount = usedCount,
            IsActive = isActive
        };

    [Fact]
    public void Percentage_discount_takes_the_right_share()
    {
        var discount = Build(DiscountType.Percentage, 25m);

        discount.CalculateDiscountAmount(800m).ShouldBe(200m);
        discount.CalculateFinalPrice(800m).ShouldBe(600m);
    }

    [Fact]
    public void Fixed_discount_takes_a_flat_amount()
    {
        var discount = Build(DiscountType.FixedAmount, 50m);

        discount.CalculateDiscountAmount(800m).ShouldBe(50m);
        discount.CalculateFinalPrice(800m).ShouldBe(750m);
    }

    [Fact]
    public void Percentage_discount_is_capped_by_maximum_discount()
    {
        var discount = Build(DiscountType.Percentage, 50m, maximumDiscount: 150m);

        // 50% of 1000 would be 500, but the cap wins.
        discount.CalculateDiscountAmount(1000m).ShouldBe(150m);
        discount.CalculateFinalPrice(1000m).ShouldBe(850m);
    }

    [Fact]
    public void Discount_never_exceeds_the_basket_amount()
    {
        var discount = Build(DiscountType.FixedAmount, 500m);

        discount.CalculateDiscountAmount(300m).ShouldBe(300m);
        discount.CalculateFinalPrice(300m).ShouldBe(0m);
    }

    [Fact]
    public void Zero_amount_yields_no_discount()
    {
        Build(DiscountType.Percentage, 50m).CalculateDiscountAmount(0m).ShouldBe(0m);
    }

    [Fact]
    public void Expired_discount_cannot_be_redeemed()
    {
        var discount = Build(endDate: Now.AddDays(-1));

        discount.CanBeRedeemed(Now, 500m, out var reason).ShouldBeFalse();
        reason!.ShouldContain("expired");

        Should.Throw<BusinessRuleException>(() => discount.EnsureRedeemable(Now, 500m));
    }

    [Fact]
    public void Discount_that_has_not_started_cannot_be_redeemed()
    {
        var discount = Build(startDate: Now.AddDays(3));

        discount.CanBeRedeemed(Now, 500m, out var reason).ShouldBeFalse();
        reason.ShouldNotBeNull();
    }

    [Fact]
    public void Inactive_discount_cannot_be_redeemed()
    {
        var discount = Build(isActive: false);

        discount.CanBeRedeemed(Now, 500m, out var reason).ShouldBeFalse();
        reason!.ShouldContain("not active");
    }

    [Fact]
    public void Discount_at_its_usage_limit_cannot_be_redeemed()
    {
        var discount = Build(usageLimit: 5, usedCount: 5);

        discount.HasReachedUsageLimit.ShouldBeTrue();
        discount.CanBeRedeemed(Now, 500m, out var reason).ShouldBeFalse();
        reason!.ShouldContain("usage limit");
    }

    [Fact]
    public void Minimum_amount_is_enforced()
    {
        var discount = Build(minimumAmount: 400m);

        discount.CanBeRedeemed(Now, 300m, out var reason).ShouldBeFalse();
        reason!.ShouldContain("minimum");

        discount.CanBeRedeemed(Now, 400m, out _).ShouldBeTrue();
    }

    [Fact]
    public void Redeeming_increments_the_usage_counter()
    {
        var discount = Build(usageLimit: 2);

        discount.Redeem(Now, 500m);
        discount.UsedCount.ShouldBe(1);

        discount.Redeem(Now, 500m);
        discount.UsedCount.ShouldBe(2);

        Should.Throw<BusinessRuleException>(() => discount.Redeem(Now, 500m));
    }

    [Fact]
    public void Deactivate_and_activate_flip_the_flag()
    {
        var discount = Build();

        discount.Deactivate(Now);
        discount.IsActive.ShouldBeFalse();

        discount.Activate(Now);
        discount.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Rounding_uses_two_decimals_away_from_zero()
    {
        var discount = Build(DiscountType.Percentage, 33m);

        // 33% of 101 = 33.33
        discount.CalculateDiscountAmount(101m).ShouldBe(33.33m);
    }
}
