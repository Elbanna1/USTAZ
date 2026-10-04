using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Teachers;

public sealed class SubjectConfiguration : BaseEntityConfiguration<Subject>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("Subjects");

        builder.Property(subject => subject.NameAr).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(subject => subject.NameEn).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(subject => subject.Slug).HasMaxLength(DomainRules.SlugMaxLength).IsRequired();
        builder.Property(subject => subject.Description).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(subject => subject.Icon).HasMaxLength(100);
        builder.Property(subject => subject.ColorHex).HasMaxLength(9);

        builder.HasIndex(subject => subject.Slug)
            .IsUnique()
            .HasDatabaseName("IX_Subjects_Slug");

        builder.HasIndex(subject => subject.IsActive)
            .HasDatabaseName("IX_Subjects_IsActive");
    }
}
