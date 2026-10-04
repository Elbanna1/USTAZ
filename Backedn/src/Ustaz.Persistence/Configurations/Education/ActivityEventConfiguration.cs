using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;

namespace Ustaz.Persistence.Configurations.Education;

public sealed class ActivityEventConfiguration : BaseEntityConfiguration<ActivityEvent>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ActivityEvent> builder)
    {
        builder.ToTable("ActivityEvents");

        builder.Property(entry => entry.Type).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.ActorName).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(entry => entry.RelatedEntityType).HasMaxLength(80);
        builder.Property(entry => entry.Metadata).HasMaxLength(1000);
        builder.Property(entry => entry.Visibility).HasConversion<int>();
        builder.Property(entry => entry.DeduplicationKey).HasMaxLength(200);

        // The workspace feed: newest first within one tenant. Descending on the sort key so the
        // index is scanned in the order the feed is read.
        builder.HasIndex(entry => new { entry.TenantId, entry.OccurredAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ActivityEvents_TenantId_OccurredAt");

        // A student's own feed. Filtered, because most rows are workspace-level and carry no
        // student at all.
        builder.HasIndex(entry => new { entry.StudentId, entry.OccurredAt })
            .IsDescending(false, true)
            .HasFilter("[StudentId] IS NOT NULL")
            .HasDatabaseName("IX_ActivityEvents_StudentId_OccurredAt");

        // Type filters are applied inside a workspace, so the tenant leads the index.
        builder.HasIndex(entry => new { entry.TenantId, entry.Type, entry.OccurredAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_ActivityEvents_TenantId_Type_OccurredAt");

        // Idempotency enforced by the database, not only by the recorder's pre-check: two
        // requests racing on the same key lose the second insert instead of double-posting.
        // Filtered, since the great majority of rows carry no key.
        builder.HasIndex(entry => new { entry.TenantId, entry.DeduplicationKey })
            .IsUnique()
            .HasFilter("[DeduplicationKey] IS NOT NULL")
            .HasDatabaseName("UX_ActivityEvents_TenantId_DeduplicationKey");

        builder.HasOne(entry => entry.Tenant)
            .WithMany()
            .HasForeignKey(entry => entry.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // The timeline outlives a student profile being removed, so the link is severed rather
        // than cascading the history away.
        builder.HasOne(entry => entry.Student)
            .WithMany()
            .HasForeignKey(entry => entry.StudentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
