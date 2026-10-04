using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Discounts.Dtos;

public sealed record DiscountDto
{
    public Guid Id { get; init; }

    public Guid TeacherId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DiscountType Type { get; init; }

    public string TypeName => Type.ToString();

    public decimal Value { get; init; }

    public decimal? MinimumAmount { get; init; }

    public decimal? MaximumDiscount { get; init; }

    public DateTime StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public int? UsageLimit { get; init; }

    public int UsedCount { get; init; }

    public int? RemainingUses => UsageLimit.HasValue ? Math.Max(0, UsageLimit.Value - UsedCount) : null;

    public bool IsActive { get; init; }

    /// <summary>True when the code is active, inside its window and under its usage limit.</summary>
    public bool IsCurrentlyValid { get; init; }

    public DateTime CreatedAt { get; init; }
}

/// <summary>Result of validating a promo code against a specific basket.</summary>
public sealed record DiscountValidationResultDto
{
    public bool IsValid { get; init; }

    public string? FailureReason { get; init; }

    public string Code { get; init; } = string.Empty;

    public DiscountType? Type { get; init; }

    public decimal OriginalAmount { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal FinalAmount { get; init; }

    public string Currency { get; init; } = "EGP";
}
