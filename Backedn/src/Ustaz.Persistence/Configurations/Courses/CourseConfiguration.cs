using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Courses;

public sealed class CourseConfiguration : BaseEntityConfiguration<Course>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses");

        builder.Property(course => course.Title).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(course => course.Slug).HasMaxLength(DomainRules.SlugMaxLength).IsRequired();
        builder.Property(course => course.Description).HasMaxLength(DomainRules.DescriptionMaxLength);
        builder.Property(course => course.ThumbnailUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(course => course.ThumbnailPublicId).HasMaxLength(300);

        builder.Property(course => course.Price).HasPrecision(18, 2);
        builder.Property(course => course.OriginalPrice).HasPrecision(18, 2);
        builder.Property(course => course.Rating).HasPrecision(3, 2);

        builder.Ignore(course => course.HasDiscount);
        builder.Ignore(course => course.IsFree);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Courses_Price", "[Price] >= 0");
            table.HasCheckConstraint("CK_Courses_DiscountPercentage", "[DiscountPercentage] >= 0 AND [DiscountPercentage] <= 100");
        });

        builder.HasIndex(course => course.TeacherId).HasDatabaseName("IX_Courses_TeacherId");

        builder.HasIndex(course => new { course.TenantId, course.IsPublished })
            .HasDatabaseName("IX_Courses_TenantId_IsPublished");
        builder.HasIndex(course => course.SubjectId).HasDatabaseName("IX_Courses_SubjectId");
        builder.HasIndex(course => course.GradeId).HasDatabaseName("IX_Courses_GradeId");

        builder.HasIndex(course => course.Slug)
            .IsUnique()
            .HasDatabaseName("UX_Courses_Slug");

        builder.HasIndex(course => new { course.IsPublished, course.IsFeatured, course.Rating })
            .HasDatabaseName("IX_Courses_Published_Featured_Rating");

        builder.HasOne(course => course.Teacher)
            .WithMany(teacher => teacher.Courses)
            .HasForeignKey(course => course.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(course => course.Subject)
            .WithMany(subject => subject.Courses)
            .HasForeignKey(course => course.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(course => course.Grade)
            .WithMany(grade => grade.Courses)
            .HasForeignKey(course => course.GradeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(course => course.Sections)
            .WithOne(section => section.Course)
            .HasForeignKey(section => section.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
