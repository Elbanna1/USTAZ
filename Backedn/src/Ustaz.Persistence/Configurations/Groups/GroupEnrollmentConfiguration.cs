using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Groups;

public sealed class GroupEnrollmentConfiguration : BaseEntityConfiguration<GroupEnrollment>
{
    protected override void ConfigureEntity(EntityTypeBuilder<GroupEnrollment> builder)
    {
        builder.ToTable("GroupEnrollments");

        builder.Property(enrollment => enrollment.Status).HasConversion<int>();
        builder.Property(enrollment => enrollment.PricePaid).HasPrecision(18, 2);
        builder.Property(enrollment => enrollment.DiscountAmount).HasPrecision(18, 2);
        builder.Property(enrollment => enrollment.FinalPrice).HasPrecision(18, 2);
        builder.Property(enrollment => enrollment.DiscountCode).HasMaxLength(50);
        builder.Property(enrollment => enrollment.CancellationReason).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.Ignore(enrollment => enrollment.IsOccupyingSeat);

        builder.HasIndex(enrollment => new { enrollment.StudentId, enrollment.GroupId, enrollment.Status })
            .HasDatabaseName("IX_GroupEnrollments_Student_Group_Status");

        builder.HasIndex(enrollment => enrollment.GroupId).HasDatabaseName("IX_GroupEnrollments_GroupId");
        builder.HasIndex(enrollment => enrollment.EnrolledAt).HasDatabaseName("IX_GroupEnrollments_EnrolledAt");

        // A student can hold at most one non-cancelled enrollment per group.
        builder.HasIndex(enrollment => new { enrollment.StudentId, enrollment.GroupId })
            .IsUnique()
            .HasFilter("[Status] <> 3")
            .HasDatabaseName("UX_GroupEnrollments_Student_Group_Active");

        builder.HasOne(enrollment => enrollment.Student)
            .WithMany(student => student.GroupEnrollments)
            .HasForeignKey(enrollment => enrollment.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(enrollment => enrollment.Group)
            .WithMany(group => group.Enrollments)
            .HasForeignKey(enrollment => enrollment.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // A redeemed discount is never hard deleted, so NoAction is safe and avoids
        // a second cascade path from the teacher.
        builder.HasOne(enrollment => enrollment.Discount)
            .WithMany(discount => discount.GroupEnrollments)
            .HasForeignKey(enrollment => enrollment.DiscountId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
