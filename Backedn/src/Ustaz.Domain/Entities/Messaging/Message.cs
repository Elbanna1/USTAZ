using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Identity;

namespace Ustaz.Domain.Entities.Messaging;

public class Message : BaseEntity
{
    public Guid ConversationId { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public Guid TenantId { get; set; }

    public Guid SenderUserId { get; set; }

    public ApplicationUser Sender { get; set; } = null!;

    public string Body { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public DateTime? EditedAt { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<MessageAttachment> Attachments { get; set; } = [];
}
