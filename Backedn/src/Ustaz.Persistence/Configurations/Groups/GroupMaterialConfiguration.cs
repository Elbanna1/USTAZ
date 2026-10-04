using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Groups;

public sealed class GroupMaterialConfiguration : BaseEntityConfiguration<GroupMaterial>
{
    protected override void ConfigureEntity(EntityTypeBuilder<GroupMaterial> builder)
    {
        builder.ToTable("GroupMaterials");

        builder.Property(material => material.Title).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(material => material.Description).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(material => material.FileUrl).HasMaxLength(DomainRules.UrlMaxLength).IsRequired();
        builder.Property(material => material.FilePublicId).HasMaxLength(300);
        builder.Property(material => material.FileType).HasConversion<int>();

        builder.HasIndex(material => new { material.GroupId, material.SortOrder })
            .HasDatabaseName("IX_GroupMaterials_GroupId_SortOrder");
    }
}
