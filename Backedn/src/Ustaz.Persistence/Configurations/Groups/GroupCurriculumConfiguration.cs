using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Groups;

public sealed class GroupCurriculumConfiguration : BaseEntityConfiguration<GroupCurriculum>
{
    protected override void ConfigureEntity(EntityTypeBuilder<GroupCurriculum> builder)
    {
        builder.ToTable("GroupCurriculums");

        builder.Property(item => item.Title).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.HasIndex(item => new { item.GroupId, item.SortOrder })
            .HasDatabaseName("IX_GroupCurriculums_GroupId_SortOrder");
    }
}
