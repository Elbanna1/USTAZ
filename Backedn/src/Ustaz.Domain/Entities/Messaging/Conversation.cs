using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities.Messaging;

/// <summary>
/// A private thread inside one workspace. Access is always decided by participation,
/// never by knowing the conversation id.
/// </summary>
public class Conversation : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public ConversationType Type { get; set; } = ConversationType.StudentTeacher;

    public string? Subject { get; set; }

    /// <summary>Optional context: the group the thread is about.</summary>
    public Guid? GroupId { get; set; }

    public Group? Group { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public string? LastMessagePreview { get; set; }

    public bool IsArchived { get; set; }

    public ICollection<ConversationParticipant> Participants { get; set; } = [];

    public ICollection<Message> Messages { get; set; } = [];

    public bool HasParticipant(Guid userId) =>
        Participants.Any(participant => participant.UserId == userId && !participant.HasLeft);
}
