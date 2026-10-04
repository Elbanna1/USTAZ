using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Courses;

public sealed class CourseEnrollmentConfiguration : BaseEntityConfiguration<CourseEnrollment>
{
    protected override void ConfigureEntity(EntityTypeBuilder<CourseEnrollment> builder)
    {
        builder.ToTable("CourseEnrollments");

        builder.Property(enrollment => enrollment.Status).HasConversion<int>();
        builder.Property(enrollment => enrollment.PricePaid).HasPrecision(18, 2);
        builder.Property(enrollment => enrollment.DiscountAmount).HasPrecision(18, 2);
        builder.Property(enrollment => enrollment.FinalPrice).HasPrecision(18, 2);
        builder.Property(enrollment => enrollment.DiscountCode).HasMaxLength(50);
        builder.Property(enrollment => enrollment.CancellationReason).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.Ignore(enrollment => enrollment.IsActive);

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_CourseEnrollments_Progress", "[ProgressPercentage] >= 0 AND [ProgressPercentage] <= 100"));

        builder.HasIndex(enrollment => enrollment.CourseId).HasDatabaseName("IX_CourseEnrollments_CourseId");
        builder.HasIndex(enrollment => enrollment.StudentId).HasDatabaseName("IX_CourseEnrollments_StudentId");

        builder.HasIndex(enrollment => new { enrollment.StudentId, enrollment.CourseId })
            .IsUnique()
            .HasFilter("[Status] <> 3")
            .HasDatabaseName("UX_CourseEnrollments_Student_Course_Active");

        builder.HasOne(enrollment => enrollment.Student)
            .WithMany(student => student.CourseEnrollments)
            .HasForeignKey(enrollment => enrollment.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(enrollment => enrollment.Course)
            .WithMany(course => course.Enrollments)
            .HasForeignKey(enrollment => enrollment.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // A redeemed discount is never hard deleted, so NoAction is safe and avoids
        // a second cascade path from the teacher.
        builder.HasOne(enrollment => enrollment.Discount)
            .WithMany(discount => discount.CourseEnrollments)
            .HasForeignKey(enrollment => enrollment.DiscountId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
