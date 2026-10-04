using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Engagement;

public sealed class NotificationConfiguration : BaseEntityConfiguration<Notification>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.Property(notification => notification.Title).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(notification => notification.Message).HasMaxLength(DomainRules.ShortTextMaxLength).IsRequired();
        builder.Property(notification => notification.ActionUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(notification => notification.Type).HasConversion<int>();
        builder.Property(notification => notification.Category).HasConversion<int>();
        builder.Property(notification => notification.Priority).HasConversion<int>();
        builder.Property(notification => notification.TitleEn).HasMaxLength(DomainRules.NameMaxLength);
        builder.Property(notification => notification.MessageEn).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(notification => notification.Event).HasMaxLength(80);
        builder.Property(notification => notification.RelatedEntityType).HasMaxLength(80);
        builder.Property(notification => notification.DeduplicationKey).HasMaxLength(200);
        builder.Property(notification => notification.Metadata).HasMaxLength(2000);

        builder.HasIndex(notification => notification.UserId).HasDatabaseName("IX_Notifications_UserId");

        builder.HasIndex(notification => new { notification.UserId, notification.Category })
            .HasDatabaseName("IX_Notifications_UserId_Category");

        builder.HasIndex(notification => notification.TenantId)
            .HasDatabaseName("IX_Notifications_TenantId");

        // Drives both the bell badge (unread count) and the paged inbox.
        builder.HasIndex(notification => new { notification.UserId, notification.IsRead, notification.CreatedAt })
            .HasDatabaseName("IX_Notifications_UserId_IsRead_CreatedAt");

        // Deduplication is enforced by the database, not only by the dispatcher's check:
        // two concurrent sweeps racing on the same key lose the second insert rather than
        // producing a duplicate. Filtered, so the vast majority of rows carry no key.
        builder.HasIndex(notification => new { notification.UserId, notification.DeduplicationKey })
            .IsUnique()
            .HasFilter("[DeduplicationKey] IS NOT NULL")
            .HasDatabaseName("UX_Notifications_UserId_DeduplicationKey");

        // The inbox is read newest-first, so the sort key is indexed in that direction.
        builder.HasIndex(notification => new { notification.UserId, notification.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Notifications_UserId_CreatedAt");
    }
}
