namespace Ustaz.Application.Features.Shared.Dtos;

/// <summary>Everything the frontend needs to render a price badge without recomputing anything.</summary>
public sealed record PricingDto
{
    public decimal Price { get; init; }

    public decimal? OriginalPrice { get; init; }

    public int DiscountPercentage { get; init; }

    public bool HasDiscount { get; init; }

    public bool IsFree { get; init; }

    public string Currency { get; init; } = "EGP";
}
