namespace Ustaz.Domain.Enums;

/// <summary>Delivery channels a user can enable per notification category.</summary>
[Flags]
public enum NotificationChannel
{
    None = 0,
    InApp = 1,
    Email = 2,
    WhatsApp = 4
}
