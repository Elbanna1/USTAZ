using Ustaz.Domain.Enums;

namespace Ustaz.Application.Common.Notifications;

/// <summary>
/// What a user receives before they have touched their preferences.
///
/// The bell is always on. Email is on only where missing the message has a real cost — a
/// security event, money, a result, a deadline — and off for the chatty categories, because
/// an inbox full of "you were marked present" is how people learn to ignore a sender.
/// </summary>
public static class NotificationDefaults
{
    public static NotificationChannel ChannelsFor(NotificationCategory category) => category switch
    {
        NotificationCategory.Security => NotificationChannel.InApp | NotificationChannel.Email,
        NotificationCategory.Payment => NotificationChannel.InApp | NotificationChannel.Email,
        NotificationCategory.Subscription => NotificationChannel.InApp | NotificationChannel.Email,
        NotificationCategory.Certificate => NotificationChannel.InApp | NotificationChannel.Email,
        NotificationCategory.Grade => NotificationChannel.InApp | NotificationChannel.Email,
        NotificationCategory.Exam => NotificationChannel.InApp | NotificationChannel.Email,
        NotificationCategory.Homework => NotificationChannel.InApp | NotificationChannel.Email,
        NotificationCategory.Enrollment => NotificationChannel.InApp | NotificationChannel.Email,

        // Messages do get an email, but only when the recipient is not connected — the
        // template itself carries that rule.
        NotificationCategory.Message => NotificationChannel.InApp | NotificationChannel.Email,

        // Attendance, announcements, schedule, courses and system notices stay
        // in-app unless the user asks for them by mail.
        _ => NotificationChannel.InApp
    };
}
