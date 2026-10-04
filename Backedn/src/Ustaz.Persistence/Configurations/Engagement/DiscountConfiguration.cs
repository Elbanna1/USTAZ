using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Engagement;

public sealed class DiscountConfiguration : BaseEntityConfiguration<Discount>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Discount> builder)
    {
        builder.ToTable("Discounts");

        builder.Property(discount => discount.Code).HasMaxLength(50).IsRequired();
        builder.Property(discount => discount.Description).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(discount => discount.Type).HasConversion<int>();

        builder.Property(discount => discount.Value).HasPrecision(18, 2);
        builder.Property(discount => discount.MinimumAmount).HasPrecision(18, 2);
        builder.Property(discount => discount.MaximumDiscount).HasPrecision(18, 2);

        builder.Ignore(discount => discount.HasReachedUsageLimit);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Discounts_Value", "[Value] > 0");
            table.HasCheckConstraint("CK_Discounts_UsedCount", "[UsedCount] >= 0");
        });

        builder.HasIndex(discount => discount.Code).HasDatabaseName("IX_Discounts_Code");

        builder.HasIndex(discount => new { discount.TenantId, discount.IsActive })
            .HasDatabaseName("IX_Discounts_TenantId_IsActive");

        // Codes are scoped to a teacher, so two teachers may both use "WELCOME10".
        builder.HasIndex(discount => new { discount.TeacherId, discount.Code })
            .IsUnique()
            .HasDatabaseName("UX_Discounts_TeacherId_Code");

        builder.HasIndex(discount => new { discount.IsActive, discount.StartDate, discount.EndDate })
            .HasDatabaseName("IX_Discounts_Active_Window");

        builder.HasOne(discount => discount.Teacher)
            .WithMany(teacher => teacher.Discounts)
            .HasForeignKey(discount => discount.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
