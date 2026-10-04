using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Courses;

public sealed class CourseLessonConfiguration : BaseEntityConfiguration<CourseLesson>
{
    protected override void ConfigureEntity(EntityTypeBuilder<CourseLesson> builder)
    {
        builder.ToTable("CourseLessons");

        builder.Property(lesson => lesson.Title).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(lesson => lesson.Description).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(lesson => lesson.VideoUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(lesson => lesson.VideoPublicId).HasMaxLength(300);

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_CourseLessons_Duration", "[DurationInMinutes] >= 0"));

        builder.HasIndex(lesson => new { lesson.SectionId, lesson.SortOrder })
            .HasDatabaseName("IX_CourseLessons_SectionId_SortOrder");

        builder.HasIndex(lesson => lesson.IsPreview).HasDatabaseName("IX_CourseLessons_IsPreview");
    }
}
