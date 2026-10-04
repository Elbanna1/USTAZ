using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities.Messaging;

public class MessageAttachment : BaseEntity
{
    public Guid MessageId { get; set; }

    public Message Message { get; set; } = null!;

    public Guid TenantId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FileUrl { get; set; } = string.Empty;

    public string? FilePublicId { get; set; }

    public MaterialFileType FileType { get; set; } = MaterialFileType.Document;

    public long? FileSizeInBytes { get; set; }
}
