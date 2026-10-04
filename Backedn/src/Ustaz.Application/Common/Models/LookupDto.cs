namespace Ustaz.Application.Common.Models;

/// <summary>Minimal id/name pair used inside composite responses and dropdowns.</summary>
public sealed record LookupDto
{
    public Guid Id { get; init; }

    public string NameAr { get; init; } = string.Empty;

    public string NameEn { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? Icon { get; init; }
}
