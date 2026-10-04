using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Courses;

public sealed class CourseSectionConfiguration : BaseEntityConfiguration<CourseSection>
{
    protected override void ConfigureEntity(EntityTypeBuilder<CourseSection> builder)
    {
        builder.ToTable("CourseSections");

        builder.Property(section => section.Title).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(section => section.Description).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.HasIndex(section => new { section.CourseId, section.SortOrder })
            .HasDatabaseName("IX_CourseSections_CourseId_SortOrder");

        builder.HasMany(section => section.Lessons)
            .WithOne(lesson => lesson.Section)
            .HasForeignKey(lesson => lesson.SectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
