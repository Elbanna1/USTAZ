using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;

namespace Ustaz.Persistence.Configurations.Education;

public sealed class AttendanceSessionConfiguration : BaseEntityConfiguration<AttendanceSession>
{
    protected override void ConfigureEntity(EntityTypeBuilder<AttendanceSession> builder)
    {
        builder.ToTable("AttendanceSessions");

        builder.Property(session => session.Title).HasMaxLength(DomainRules.NameMaxLength);
        builder.Property(session => session.Notes).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(session => session.Status).HasConversion<int>();
        builder.Property(session => session.StartTime).HasColumnType("time");
        builder.Property(session => session.EndTime).HasColumnType("time");

        // One register per group per calendar day.
        builder.HasIndex(session => new { session.GroupId, session.SessionDate })
            .IsUnique()
            .HasDatabaseName("UX_AttendanceSessions_GroupId_SessionDate");

        builder.HasIndex(session => new { session.TenantId, session.SessionDate })
            .HasDatabaseName("IX_AttendanceSessions_TenantId_SessionDate");

        builder.HasIndex(session => new { session.TenantId, session.Status })
            .HasDatabaseName("IX_AttendanceSessions_TenantId_Status");

        builder.HasOne(session => session.Tenant)
            .WithMany()
            .HasForeignKey(session => session.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction: the cascade already reaches here through the tenant.
        builder.HasOne(session => session.Group)
            .WithMany()
            .HasForeignKey(session => session.GroupId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(session => session.GroupSchedule)
            .WithMany()
            .HasForeignKey(session => session.GroupScheduleId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(session => session.Records)
            .WithOne(record => record.Session)
            .HasForeignKey(record => record.AttendanceSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AttendanceRecordConfiguration : BaseEntityConfiguration<AttendanceRecord>
{
    protected override void ConfigureEntity(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords");

        builder.Property(record => record.Status).HasConversion<int>();
        builder.Property(record => record.Note).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.Ignore(record => record.CountsAsMissed);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_AttendanceRecords_MinutesLate", "[MinutesLate] IS NULL OR [MinutesLate] >= 0"));

        // A student appears at most once per session.
        builder.HasIndex(record => new { record.AttendanceSessionId, record.StudentId })
            .IsUnique()
            .HasDatabaseName("UX_AttendanceRecords_SessionId_StudentId");

        // Drives the per-student history and statistics queries.
        builder.HasIndex(record => new { record.TenantId, record.StudentId, record.Status })
            .HasDatabaseName("IX_AttendanceRecords_TenantId_StudentId_Status");

        builder.HasIndex(record => record.StudentId)
            .HasDatabaseName("IX_AttendanceRecords_StudentId");

        builder.HasOne(record => record.Student)
            .WithMany()
            .HasForeignKey(record => record.StudentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
