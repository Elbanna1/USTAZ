using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ustaz.Application.Common.Emails;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;

namespace Ustaz.Infrastructure.Services.Messaging;

/// <summary>
/// Creates in-app notifications and fans them out to the channels each user has enabled.
///
/// Preferences are honoured per category: a user who turned a category off receives nothing,
/// and a user who enabled Email additionally gets an email. The in-app row is always written
/// when the category is enabled, so the bell badge stays the source of truth.
///
/// Two rules make this safe to call from anywhere:
/// 1. A <see cref="NotificationPriority.Critical"/> notification (security, failed payment,
///    expired subscription) ignores preferences — those carry consequences the user must see.
/// 2. A request carrying a deduplication key is written at most once per recipient, so a
///    retried handler or a nightly reminder job cannot produce a second copy.
/// </summary>
public sealed class NotificationDispatcher(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ITransactionalEmailService transactionalEmail,
    IRealtimeNotifier realtimeNotifier,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    public Task NotifyAsync(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        string? actionUrl = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default) =>
        NotifyAsync(userId, title, message, type, MapCategory(type), null, actionUrl, referenceId, cancellationToken);

    public async Task NotifyAsync(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        NotificationCategory category,
        Guid? tenantId = null,
        string? actionUrl = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Title = title,
            Message = message,
            Type = type,
            Category = category,
            Priority = NotificationPriority.Normal,
            ActionUrl = actionUrl,
            ReferenceId = referenceId,
            IsRead = false,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await DeliverAsync(notification, values: null, cancellationToken);
    }

    public Task NotifyManyAsync(
        IEnumerable<Guid> userIds,
        string title,
        string message,
        NotificationType type,
        NotificationCategory category,
        Guid? tenantId = null,
        string? actionUrl = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default) =>
        DeliverManyAsync(
            userIds,
            userId => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TenantId = tenantId,
                Title = title,
                Message = message,
                Type = type,
                Category = category,
                Priority = NotificationPriority.Normal,
                ActionUrl = actionUrl,
                ReferenceId = referenceId,
                IsRead = false,
                CreatedAt = dateTimeProvider.UtcNow
            },
            category,
            NotificationPriority.Normal,
            deduplicationKey: null,
            values: null,
            cancellationToken);

    public async Task<bool> SendAsync(
        Guid userId,
        NotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var template = NotificationCatalog.Get(request.Event);
        var values = request.Values;

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = request.TenantId,
            Event = template.Event,
            Type = template.Type,
            Category = template.Category,
            Priority = template.Priority,
            Title = NotificationTemplate.Render(template.TitleAr, values),
            Message = NotificationTemplate.Render(template.BodyAr, values),
            TitleEn = NotificationTemplate.Render(template.TitleEn, values),
            MessageEn = NotificationTemplate.Render(template.BodyEn, values),
            ActionUrl = request.ActionUrl
                ?? (template.ActionUrl is null ? null : NotificationTemplate.Render(template.ActionUrl, values)),
            ReferenceId = request.ReferenceId,
            RelatedEntityType = template.RelatedEntityType,
            DeduplicationKey = request.DeduplicationKey,
            Metadata = values is { Count: > 0 } ? JsonSerializer.Serialize(values) : null,
            IsRead = false,
            CreatedAt = dateTimeProvider.UtcNow
        };

        return await DeliverAsync(notification, values, cancellationToken);
    }

    public Task<int> SendManyAsync(
        IEnumerable<Guid> userIds,
        NotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var template = NotificationCatalog.Get(request.Event);
        var values = request.Values;

        // Render once: the copy is identical for everyone in the fan-out.
        var titleAr = NotificationTemplate.Render(template.TitleAr, values);
        var bodyAr = NotificationTemplate.Render(template.BodyAr, values);
        var titleEn = NotificationTemplate.Render(template.TitleEn, values);
        var bodyEn = NotificationTemplate.Render(template.BodyEn, values);
        var actionUrl = request.ActionUrl
            ?? (template.ActionUrl is null ? null : NotificationTemplate.Render(template.ActionUrl, values));
        var metadata = values is { Count: > 0 } ? JsonSerializer.Serialize(values) : null;

        return DeliverManyAsync(
            userIds,
            userId => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TenantId = request.TenantId,
                Event = template.Event,
                Type = template.Type,
                Category = template.Category,
                Priority = template.Priority,
                Title = titleAr,
                Message = bodyAr,
                TitleEn = titleEn,
                MessageEn = bodyEn,
                ActionUrl = actionUrl,
                ReferenceId = request.ReferenceId,
                RelatedEntityType = template.RelatedEntityType,
                DeduplicationKey = request.DeduplicationKey,
                Metadata = metadata,
                IsRead = false,
                CreatedAt = dateTimeProvider.UtcNow
            },
            template.Category,
            template.Priority,
            request.DeduplicationKey,
            values,
            cancellationToken);
    }

    /// <summary>
    /// Fans one notification out to many recipients in a fixed number of queries.
    ///
    /// Announcing to a whole class used to cost three round trips per student; here the
    /// preferences and the existing deduplication keys are each fetched once, every row is
    /// inserted in a single save, and only the live push is per user.
    /// </summary>
    private async Task<int> DeliverManyAsync(
        IEnumerable<Guid> userIds,
        Func<Guid, Notification> build,
        NotificationCategory category,
        NotificationPriority priority,
        string? deduplicationKey,
        IReadOnlyDictionary<string, string>? values,
        CancellationToken cancellationToken)
    {
        var recipients = userIds.Distinct().ToList();

        if (recipients.Count == 0)
        {
            return 0;
        }

        var mandatory = priority == NotificationPriority.Critical;

        var preferences = await context.NotificationPreferences
            .AsNoTracking()
            .Where(entry => recipients.Contains(entry.UserId) && entry.Category == category)
            .Select(entry => new { entry.UserId, entry.Channels, entry.IsEnabled })
            .ToDictionaryAsync(entry => entry.UserId, cancellationToken);

        var suppressed = new HashSet<Guid>();

        if (deduplicationKey is not null)
        {
            var alreadyNotified = await context.Notifications
                .AsNoTracking()
                .Where(existing => recipients.Contains(existing.UserId)
                    && existing.DeduplicationKey == deduplicationKey)
                .Select(existing => existing.UserId)
                .ToListAsync(cancellationToken);

            suppressed = alreadyNotified.ToHashSet();
        }

        var pending = new List<Notification>();
        var emailRecipients = new List<Notification>();

        foreach (var userId in recipients)
        {
            if (suppressed.Contains(userId))
            {
                continue;
            }

            var preference = preferences.GetValueOrDefault(userId);
            var channels = preference?.Channels ?? NotificationDefaults.ChannelsFor(category);
            var isEnabled = preference?.IsEnabled ?? true;

            if (!isEnabled && !mandatory)
            {
                continue;
            }

            var notification = build(userId);

            if (mandatory || channels.HasFlag(NotificationChannel.InApp))
            {
                pending.Add(notification);
            }

            if (mandatory || (isEnabled && channels.HasFlag(NotificationChannel.Email)))
            {
                emailRecipients.Add(notification);
            }
        }

        if (pending.Count > 0)
        {
            await context.Notifications.AddRangeAsync(pending, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            // One grouped count for the whole batch rather than one per recipient.
            var recipientIds = pending.Select(entry => entry.UserId).ToList();

            var unreadCounts = await context.Notifications
                .AsNoTracking()
                .Where(entry => recipientIds.Contains(entry.UserId) && !entry.IsRead)
                .GroupBy(entry => entry.UserId)
                .Select(group => new { UserId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(entry => entry.UserId, entry => entry.Count, cancellationToken);

            foreach (var notification in pending)
            {
                await PublishLiveAsync(
                    notification,
                    unreadCounts.GetValueOrDefault(notification.UserId),
                    cancellationToken);
            }
        }

        foreach (var notification in emailRecipients)
        {
            await SendEmailAsync(notification, values, cancellationToken);
        }

        return pending.Count;
    }

    /// <summary>
    /// The single write path: preference check, deduplication, persistence, then live push
    /// and email. Returns false when the notification was deliberately suppressed.
    /// </summary>
    private async Task<bool> DeliverAsync(
        Notification notification,
        IReadOnlyDictionary<string, string>? values,
        CancellationToken cancellationToken)
    {
        var mandatory = notification.Priority == NotificationPriority.Critical;

        var preference = await context.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entry => entry.UserId == notification.UserId && entry.Category == notification.Category,
                cancellationToken);

        // No row means "use the category default", which turns email on for the events
        // that cost something to miss and leaves it off for the chatty ones.
        var channels = preference?.Channels ?? NotificationDefaults.ChannelsFor(notification.Category);
        var isEnabled = preference?.IsEnabled ?? true;

        if (!isEnabled && !mandatory)
        {
            return false;
        }

        // A critical notification is always shown in-app, whatever the user switched off.
        var writeInApp = mandatory || channels.HasFlag(NotificationChannel.InApp);

        if (writeInApp)
        {
            if (!string.IsNullOrWhiteSpace(notification.DeduplicationKey)
                && await context.Notifications.AnyAsync(
                    existing => existing.UserId == notification.UserId
                        && existing.DeduplicationKey == notification.DeduplicationKey,
                    cancellationToken))
            {
                return false;
            }

            await context.Notifications.AddAsync(notification, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            await PublishLiveAsync(notification, unreadCount: null, cancellationToken);
        }

        // A critical notification is emailed whatever the preference says; everything else
        // honours the channel switch.
        if (mandatory || (channels.HasFlag(NotificationChannel.Email) && isEnabled))
        {
            await SendEmailAsync(notification, values, cancellationToken);
        }

        // WhatsApp/SMS is delivered by the same preference switch once a provider is
        // configured; the channel flag is already persisted and honoured here.
        return writeInApp;
    }

    /// <summary>
    /// Pushes to connected clients. The row is already committed, so a transport failure is
    /// logged and swallowed rather than failing the business operation that raised it.
    /// </summary>
    private async Task PublishLiveAsync(
        Notification notification,
        int? unreadCount,
        CancellationToken cancellationToken)
    {
        try
        {
            await realtimeNotifier.PublishAsync(
                new RealtimeNotification
                {
                    Id = notification.Id,
                    UserId = notification.UserId,
                    TenantId = notification.TenantId,
                    Title = notification.Title,
                    Message = notification.Message,
                    TitleEn = notification.TitleEn,
                    MessageEn = notification.MessageEn,
                    Type = notification.Type,
                    Category = notification.Category,
                    Priority = notification.Priority,
                    Event = notification.Event,
                    ActionUrl = notification.ActionUrl,
                    ReferenceId = notification.ReferenceId,
                    RelatedEntityType = notification.RelatedEntityType,
                    CreatedAt = notification.CreatedAt
                },
                cancellationToken);

            var unread = unreadCount ?? await context.Notifications.CountAsync(
                entry => entry.UserId == notification.UserId && !entry.IsRead,
                cancellationToken);

            await realtimeNotifier.PublishUnreadCountAsync(notification.UserId, unread, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Live delivery of notification {NotificationId} failed; the stored notification is unaffected.",
                notification.Id);
        }
    }

    /// <summary>
    /// Hands the notification to the email channel.
    ///
    /// An event that has a template in the email catalog is sent as a proper branded,
    /// localised message; one that does not is simply not emailed, because a bell caption
    /// wrapped in a paragraph tag is not an email anyone wants to receive. Delivery failures
    /// are swallowed by the transactional service, so the bell never depends on SMTP.
    /// </summary>
    private async Task SendEmailAsync(
        Notification notification,
        IReadOnlyDictionary<string, string>? values,
        CancellationToken cancellationToken)
    {
        if (notification.Event is null || !EmailCatalog.TryGet(notification.Event, out _))
        {
            return;
        }

        await transactionalEmail.SendToUserAsync(
            notification.UserId,
            new TransactionalEmailRequest
            {
                Event = notification.Event,
                TenantId = notification.TenantId,
                RelatedEntityId = notification.ReferenceId,
                Values = values,
                // Scoped so the email and the in-app row dedupe independently: suppressing
                // one must never silently suppress the other.
                DeduplicationKey = notification.DeduplicationKey is null
                    ? null
                    : "notification:" + notification.DeduplicationKey
            },
            cancellationToken);
    }

    /// <summary>Maps the legacy notification type onto a preference category.</summary>
    private static NotificationCategory MapCategory(NotificationType type) => type switch
    {
        NotificationType.Enrollment => NotificationCategory.Enrollment,
        NotificationType.Review => NotificationCategory.Grade,
        NotificationType.Inquiry => NotificationCategory.Message,
        NotificationType.Schedule => NotificationCategory.Schedule,
        NotificationType.Verification => NotificationCategory.System,
        NotificationType.Discount => NotificationCategory.Payment,
        NotificationType.Course => NotificationCategory.Course,
        NotificationType.Exam => NotificationCategory.Exam,
        NotificationType.Homework => NotificationCategory.Homework,
        NotificationType.Attendance => NotificationCategory.Attendance,
        NotificationType.Payment => NotificationCategory.Payment,
        NotificationType.Message => NotificationCategory.Message,
        NotificationType.Announcement => NotificationCategory.Announcement,
        NotificationType.Security => NotificationCategory.Security,
        NotificationType.Certificate => NotificationCategory.Certificate,
        NotificationType.Subscription => NotificationCategory.Subscription,
        _ => NotificationCategory.System
    };
}
