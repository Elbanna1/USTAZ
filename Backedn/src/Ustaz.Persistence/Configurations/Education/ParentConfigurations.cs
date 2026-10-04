using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;

namespace Ustaz.Persistence.Configurations.Education;

public sealed class ParentProfileConfiguration : BaseEntityConfiguration<ParentProfile>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ParentProfile> builder)
    {
        builder.ToTable("ParentProfiles");

        builder.Property(parent => parent.PhoneNumber).HasMaxLength(DomainRules.PhoneMaxLength);
        builder.Property(parent => parent.AlternatePhoneNumber).HasMaxLength(DomainRules.PhoneMaxLength);
        builder.Property(parent => parent.City).HasMaxLength(100);
        builder.Property(parent => parent.Occupation).HasMaxLength(150);

        builder.HasIndex(parent => parent.UserId)
            .IsUnique()
            .HasDatabaseName("UX_ParentProfiles_UserId");

        builder.HasIndex(parent => parent.PhoneNumber)
            .HasDatabaseName("IX_ParentProfiles_PhoneNumber");

        builder.HasOne(parent => parent.User)
            .WithOne(user => user.ParentProfile)
            .HasForeignKey<ParentProfile>(parent => parent.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ParentStudentLinkConfiguration : BaseEntityConfiguration<ParentStudentLink>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ParentStudentLink> builder)
    {
        builder.ToTable("ParentStudentLinks");

        builder.Property(link => link.Relationship).HasMaxLength(50);

        // A guardian can only be linked to a given student once.
        builder.HasIndex(link => new { link.ParentId, link.StudentId })
            .IsUnique()
            .HasDatabaseName("UX_ParentStudentLinks_ParentId_StudentId");

        builder.HasIndex(link => link.StudentId)
            .HasDatabaseName("IX_ParentStudentLinks_StudentId");

        // Every parent-facing authorization check filters on these three columns.
        builder.HasIndex(link => new { link.ParentId, link.IsConfirmed })
            .HasDatabaseName("IX_ParentStudentLinks_ParentId_IsConfirmed");

        builder.HasOne(link => link.Parent)
            .WithMany(parent => parent.Children)
            .HasForeignKey(link => link.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.Student)
            .WithMany(student => student.Guardians)
            .HasForeignKey(link => link.StudentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class NotificationPreferenceConfiguration : BaseEntityConfiguration<NotificationPreference>
{
    protected override void ConfigureEntity(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");

        builder.Property(preference => preference.Category).HasConversion<int>();
        builder.Property(preference => preference.Channels).HasConversion<int>();

        builder.HasIndex(preference => new { preference.UserId, preference.Category })
            .IsUnique()
            .HasDatabaseName("UX_NotificationPreferences_UserId_Category");

        builder.HasOne(preference => preference.User)
            .WithMany(user => user.NotificationPreferences)
            .HasForeignKey(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AuditLogConfiguration : BaseEntityConfiguration<AuditLog>
{
    protected override void ConfigureEntity(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.Property(log => log.Action).HasMaxLength(100).IsRequired();
        builder.Property(log => log.EntityName).HasMaxLength(100);
        builder.Property(log => log.EntityId).HasMaxLength(100);
        builder.Property(log => log.UserEmail).HasMaxLength(256);
        builder.Property(log => log.OldValues).HasMaxLength(4000);
        builder.Property(log => log.NewValues).HasMaxLength(4000);
        builder.Property(log => log.IpAddress).HasMaxLength(64);
        builder.Property(log => log.UserAgent).HasMaxLength(512);
        builder.Property(log => log.FailureReason).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.HasIndex(log => new { log.TenantId, log.CreatedAt })
            .HasDatabaseName("IX_AuditLogs_TenantId_CreatedAt");

        builder.HasIndex(log => new { log.UserId, log.CreatedAt })
            .HasDatabaseName("IX_AuditLogs_UserId_CreatedAt");

        builder.HasIndex(log => new { log.Action, log.CreatedAt })
            .HasDatabaseName("IX_AuditLogs_Action_CreatedAt");

        builder.HasIndex(log => new { log.EntityName, log.EntityId })
            .HasDatabaseName("IX_AuditLogs_Entity");
    }
}
