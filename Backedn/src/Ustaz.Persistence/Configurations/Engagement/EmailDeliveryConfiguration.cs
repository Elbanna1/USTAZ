using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Engagement;

public sealed class EmailDeliveryConfiguration : BaseEntityConfiguration<EmailDelivery>
{
    protected override void ConfigureEntity(EntityTypeBuilder<EmailDelivery> builder)
    {
        builder.ToTable("EmailDeliveries");

        builder.Property(delivery => delivery.Event).HasMaxLength(80).IsRequired();
        builder.Property(delivery => delivery.Recipient).HasMaxLength(256).IsRequired();
        builder.Property(delivery => delivery.Subject).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(delivery => delivery.Language).HasMaxLength(10).IsRequired();
        builder.Property(delivery => delivery.Status).HasConversion<int>();
        builder.Property(delivery => delivery.ErrorCategory).HasMaxLength(80);
        builder.Property(delivery => delivery.ErrorMessage).HasMaxLength(400);
        builder.Property(delivery => delivery.CorrelationId).HasMaxLength(80);
        builder.Property(delivery => delivery.DeduplicationKey).HasMaxLength(200);
        builder.Property(delivery => delivery.RelatedEntityType).HasMaxLength(80);
        builder.Property(delivery => delivery.PayloadJson).HasMaxLength(4000);

        builder.Ignore(delivery => delivery.IsRetryable);

        // Deduplication is enforced by the database: two concurrent sends of the same
        // reminder lose the second insert rather than producing a second email.
        builder.HasIndex(delivery => new { delivery.Recipient, delivery.DeduplicationKey })
            .IsUnique()
            .HasFilter("[DeduplicationKey] IS NOT NULL")
            .HasDatabaseName("UX_EmailDeliveries_Recipient_DeduplicationKey");

        // The retry pass reads exactly this shape.
        builder.HasIndex(delivery => new { delivery.Status, delivery.CreatedAt })
            .HasDatabaseName("IX_EmailDeliveries_Status_CreatedAt");

        builder.HasIndex(delivery => new { delivery.UserId, delivery.CreatedAt })
            .HasDatabaseName("IX_EmailDeliveries_UserId_CreatedAt");

        builder.HasIndex(delivery => delivery.Event).HasDatabaseName("IX_EmailDeliveries_Event");
    }
}
