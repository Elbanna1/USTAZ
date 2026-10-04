using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities;

/// <summary>An in-app notification targeted at a single user.</summary>
public class Notification : BaseEntity
{
    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; } = NotificationType.General;

    /// <summary>Functional grouping used by the user's notification preferences.</summary>
    public NotificationCategory Category { get; set; } = NotificationCategory.System;

    /// <summary>Set when the notification came from a specific workspace.</summary>
    public Guid? TenantId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    /// <summary>Optional deep link the frontend can navigate to, e.g. /groups/{id}.</summary>
    public string? ActionUrl { get; set; }

    /// <summary>Identifier of the entity that triggered the notification.</summary>
    public Guid? ReferenceId { get; set; }

    /// <summary>Name of the entity <see cref="ReferenceId"/> points at, e.g. "Exam".</summary>
    public string? RelatedEntityType { get; set; }

    /// <summary>
    /// The catalog key this notification was generated from, e.g. "homework.graded". Storing
    /// it makes notifications groupable and lets the client render per-event UI without
    /// parsing the Arabic body.
    /// </summary>
    public string? Event { get; set; }

    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

    /// <summary>English rendering, for users who read the app in English.</summary>
    public string? TitleEn { get; set; }

    public string? MessageEn { get; set; }

    /// <summary>Small JSON payload of the values the template was rendered with.</summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Stable key for "the same thing happening again", e.g. one absence alert per student
    /// per day. A unique index on (UserId, DeduplicationKey) is what actually stops the
    /// duplicate, so a retry or a concurrent dispatch cannot slip a second copy through.
    /// </summary>
    public string? DeduplicationKey { get; set; }

    public void MarkAsRead(DateTime utcNow)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAt = utcNow;
        UpdatedAt = utcNow;
    }
}
