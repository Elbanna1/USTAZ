namespace Ustaz.Application.Common.Models;

/// <summary>Provider agnostic description of a stored media asset.</summary>
public sealed record MediaUploadResult
{
    public required string Url { get; init; }

    public required string PublicId { get; init; }

    public string? SecureUrl { get; init; }

    public string? Format { get; init; }

    public long Bytes { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public string? ResourceType { get; init; }
}
