using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Teachers;

public sealed class TeacherSocialLinkConfiguration : BaseEntityConfiguration<TeacherSocialLink>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TeacherSocialLink> builder)
    {
        builder.ToTable("TeacherSocialLinks");

        builder.Property(link => link.Url).HasMaxLength(DomainRules.UrlMaxLength).IsRequired();
        builder.Property(link => link.Platform).HasConversion<int>();

        builder.HasIndex(link => new { link.TeacherId, link.Platform })
            .IsUnique()
            .HasDatabaseName("UX_TeacherSocialLinks_TeacherId_Platform");
    }
}
