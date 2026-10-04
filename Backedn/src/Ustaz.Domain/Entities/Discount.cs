using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities;

/// <summary>
/// A promo code owned by a teacher. All redemption rules (window, usage limit,
/// minimum basket, cap) are enforced inside the aggregate so every call site agrees.
/// </summary>
public class Discount : BaseEntity
{
    /// <summary>Denormalised from the owning teacher so every query can filter by tenant directly.</summary>
    public Guid TenantId { get; set; }

    public Guid TeacherId { get; set; }

    public TeacherProfile Teacher { get; set; } = null!;

    /// <summary>Always stored upper-cased and trimmed; unique per teacher.</summary>
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DiscountType Type { get; set; } = DiscountType.Percentage;

    /// <summary>Percentage (1-100) or a fixed EGP amount depending on <see cref="Type"/>.</summary>
    public decimal Value { get; set; }

    public decimal? MinimumAmount { get; set; }

    /// <summary>Caps the money taken off when <see cref="Type"/> is percentage based.</summary>
    public decimal? MaximumDiscount { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int? UsageLimit { get; set; }

    public int UsedCount { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<GroupEnrollment> GroupEnrollments { get; set; } = [];

    public ICollection<CourseEnrollment> CourseEnrollments { get; set; } = [];

    public bool HasReachedUsageLimit => UsageLimit.HasValue && UsedCount >= UsageLimit.Value;

    public bool IsExpired(DateTime utcNow) => EndDate.HasValue && utcNow > EndDate.Value;

    public bool HasStarted(DateTime utcNow) => utcNow >= StartDate;

    /// <summary>Non-throwing check used by listings; returns the reason when not redeemable.</summary>
    public bool CanBeRedeemed(DateTime utcNow, decimal amount, out string? failureReason)
    {
        if (!IsActive)
        {
            failureReason = "This discount code is not active.";
            return false;
        }

        if (!HasStarted(utcNow))
        {
            failureReason = $"This discount code becomes valid on {StartDate:yyyy-MM-dd}.";
            return false;
        }

        if (IsExpired(utcNow))
        {
            failureReason = "This discount code has expired.";
            return false;
        }

        if (HasReachedUsageLimit)
        {
            failureReason = "This discount code has reached its usage limit.";
            return false;
        }

        if (MinimumAmount.HasValue && amount < MinimumAmount.Value)
        {
            failureReason = $"This discount code requires a minimum amount of {MinimumAmount.Value:0.##} EGP.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public void EnsureRedeemable(DateTime utcNow, decimal amount)
    {
        if (!CanBeRedeemed(utcNow, amount, out var reason))
        {
            throw new BusinessRuleException("DISCOUNT_NOT_REDEEMABLE", reason!);
        }
    }

    /// <summary>Money taken off <paramref name="amount"/>, never negative and never above the amount itself.</summary>
    public decimal CalculateDiscountAmount(decimal amount)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        var discount = Type switch
        {
            DiscountType.Percentage => amount * (Value / 100m),
            DiscountType.FixedAmount => Value,
            _ => 0m
        };

        if (MaximumDiscount.HasValue)
        {
            discount = Math.Min(discount, MaximumDiscount.Value);
        }

        discount = Math.Min(discount, amount);
        return Math.Round(Math.Max(0m, discount), 2, MidpointRounding.AwayFromZero);
    }

    public decimal CalculateFinalPrice(decimal amount) =>
        Math.Round(Math.Max(0m, amount - CalculateDiscountAmount(amount)), 2, MidpointRounding.AwayFromZero);

    public void Redeem(DateTime utcNow, decimal amount)
    {
        EnsureRedeemable(utcNow, amount);
        UsedCount++;
        UpdatedAt = utcNow;
    }

    public void Activate(DateTime utcNow)
    {
        IsActive = true;
        UpdatedAt = utcNow;
    }

    public void Deactivate(DateTime utcNow)
    {
        IsActive = false;
        UpdatedAt = utcNow;
    }
}
