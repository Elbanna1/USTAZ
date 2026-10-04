using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Engagement;

public sealed class ReviewConfiguration : BaseEntityConfiguration<Review>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");

        builder.Property(review => review.Comment).HasMaxLength(DomainRules.DescriptionMaxLength);
        builder.Property(review => review.ModerationNote).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.Ignore(review => review.TargetType);

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_Reviews_Rating", "[Rating] >= 1 AND [Rating] <= 5"));

        builder.HasIndex(review => review.TeacherId).HasDatabaseName("IX_Reviews_TeacherId");
        builder.HasIndex(review => review.GroupId).HasDatabaseName("IX_Reviews_GroupId");
        builder.HasIndex(review => review.CourseId).HasDatabaseName("IX_Reviews_CourseId");
        builder.HasIndex(review => new { review.TeacherId, review.IsApproved })
            .HasDatabaseName("IX_Reviews_TeacherId_IsApproved");

        // One review per student per teacher when the review is not scoped to a group/course.
        builder.HasIndex(review => new { review.StudentId, review.TeacherId })
            .IsUnique()
            .HasFilter("[GroupId] IS NULL AND [CourseId] IS NULL")
            .HasDatabaseName("UX_Reviews_Student_Teacher");

        builder.HasIndex(review => new { review.StudentId, review.GroupId })
            .IsUnique()
            .HasFilter("[GroupId] IS NOT NULL")
            .HasDatabaseName("UX_Reviews_Student_Group");

        builder.HasIndex(review => new { review.StudentId, review.CourseId })
            .IsUnique()
            .HasFilter("[CourseId] IS NOT NULL")
            .HasDatabaseName("UX_Reviews_Student_Course");

        builder.HasOne(review => review.Student)
            .WithMany(student => student.Reviews)
            .HasForeignKey(review => review.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(review => review.Teacher)
            .WithMany(teacher => teacher.Reviews)
            .HasForeignKey(review => review.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(review => review.Group)
            .WithMany(group => group.Reviews)
            .HasForeignKey(review => review.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(review => review.Course)
            .WithMany(course => course.Reviews)
            .HasForeignKey(review => review.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
