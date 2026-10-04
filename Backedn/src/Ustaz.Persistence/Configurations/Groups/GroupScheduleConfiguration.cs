using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Groups;

public sealed class GroupScheduleConfiguration : BaseEntityConfiguration<GroupSchedule>
{
    protected override void ConfigureEntity(EntityTypeBuilder<GroupSchedule> builder)
    {
        builder.ToTable("GroupSchedules");

        builder.Property(schedule => schedule.DayOfWeek).HasConversion<int>();
        builder.Property(schedule => schedule.StartTime).HasColumnType("time");
        builder.Property(schedule => schedule.EndTime).HasColumnType("time");
        builder.Property(schedule => schedule.Location).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(schedule => schedule.MeetingUrl).HasMaxLength(DomainRules.UrlMaxLength);

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_GroupSchedules_TimeRange", "[EndTime] > [StartTime]"));

        builder.HasIndex(schedule => new { schedule.GroupId, schedule.DayOfWeek, schedule.StartTime })
            .HasDatabaseName("IX_GroupSchedules_GroupId_Day_Start");

        builder.HasIndex(schedule => new { schedule.IsActive, schedule.DayOfWeek })
            .HasDatabaseName("IX_GroupSchedules_IsActive_Day");
    }
}
