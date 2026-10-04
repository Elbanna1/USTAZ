using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Groups.Dtos;

public sealed record GroupMaterialDto
{
    public Guid Id { get; init; }

    public Guid GroupId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <summary>Null for non-enrolled viewers when the material is restricted.</summary>
    public string? FileUrl { get; init; }

    public MaterialFileType FileType { get; init; }

    public string FileTypeName => FileType.ToString();

    public long? FileSizeInBytes { get; init; }

    public bool IsEnrolledOnly { get; init; }

    public bool IsLocked { get; init; }

    public int SortOrder { get; init; }
}
