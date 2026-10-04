using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities.Education;

/// <summary>A file the teacher attached to a homework assignment.</summary>
public class HomeworkAttachment : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid HomeworkId { get; set; }

    public Homework Homework { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;

    public string FileUrl { get; set; } = string.Empty;

    public string? FilePublicId { get; set; }

    public MaterialFileType FileType { get; set; } = MaterialFileType.Pdf;

    public long? FileSizeInBytes { get; set; }
}
