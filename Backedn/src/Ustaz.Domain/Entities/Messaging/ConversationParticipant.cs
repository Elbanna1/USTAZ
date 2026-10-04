using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Identity;

namespace Ustaz.Domain.Entities.Messaging;

public class ConversationParticipant : BaseEntity
{
    public Guid ConversationId { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LeftAt { get; set; }

    public DateTime? LastReadAt { get; set; }

    public int UnreadCount { get; set; }

    public bool IsMuted { get; set; }

    public bool HasLeft => LeftAt is not null;
}
