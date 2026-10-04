using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities.Education;

/// <summary>
/// Per-user, per-category delivery preferences. A missing row means "use the category
/// default", so a new category never silently goes dark.
/// </summary>
public class NotificationPreference : BaseEntity
{
    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public NotificationCategory Category { get; set; }

    public NotificationChannel Channels { get; set; } = NotificationChannel.InApp;

    public bool IsEnabled { get; set; } = true;

    public bool Allows(NotificationChannel channel) => IsEnabled && Channels.HasFlag(channel);
}
