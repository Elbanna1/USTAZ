using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Teachers;

public sealed class EducationalGradeConfiguration : BaseEntityConfiguration<EducationalGrade>
{
    protected override void ConfigureEntity(EntityTypeBuilder<EducationalGrade> builder)
    {
        builder.ToTable("EducationalGrades");

        builder.Property(grade => grade.NameAr).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(grade => grade.NameEn).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(grade => grade.Slug).HasMaxLength(DomainRules.SlugMaxLength).IsRequired();
        builder.Property(grade => grade.Stage).HasConversion<int>();

        builder.HasIndex(grade => grade.Slug)
            .IsUnique()
            .HasDatabaseName("IX_EducationalGrades_Slug");

        builder.HasIndex(grade => new { grade.IsActive, grade.SortOrder })
            .HasDatabaseName("IX_EducationalGrades_IsActive_SortOrder");
    }
}
