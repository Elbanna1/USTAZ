using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities;

/// <summary>A downloadable/linked resource attached to a group.</summary>
public class GroupMaterial : BaseEntity
{
    public Guid GroupId { get; set; }

    public Group Group { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string FileUrl { get; set; } = string.Empty;

    public string? FilePublicId { get; set; }

    public MaterialFileType FileType { get; set; } = MaterialFileType.Pdf;

    public long? FileSizeInBytes { get; set; }

    public int SortOrder { get; set; }

    /// <summary>When true only enrolled students may read the URL.</summary>
    public bool IsEnrolledOnly { get; set; } = true;
}
