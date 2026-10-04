using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;

namespace Ustaz.Persistence.Configurations.Education;

/// <summary>
/// Tuition paid by a family to a tenant. Intentionally a different table, sequence and
/// provider-id namespace from <c>BillingTransactions</c>, which records the tenant paying USTAZ.
/// </summary>
public sealed class EducationalPaymentConfiguration : BaseEntityConfiguration<EducationalPayment>
{
    protected override void ConfigureEntity(EntityTypeBuilder<EducationalPayment> builder)
    {
        builder.ToTable("EducationalPayments");

        builder.Property(payment => payment.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(payment => payment.Type).HasConversion<int>();
        builder.Property(payment => payment.Status).HasConversion<int>();
        builder.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();
        builder.Property(payment => payment.Provider).HasMaxLength(50);
        builder.Property(payment => payment.ProviderTransactionId).HasMaxLength(200);
        builder.Property(payment => payment.DiscountCode).HasMaxLength(50);
        builder.Property(payment => payment.Notes).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.DiscountAmount).HasPrecision(18, 2);
        builder.Property(payment => payment.NetAmount).HasPrecision(18, 2);

        builder.Ignore(payment => payment.IsSettled);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_EducationalPayments_Amount", "[Amount] >= 0");
            table.HasCheckConstraint("CK_EducationalPayments_Net", "[NetAmount] >= 0");
        });

        // Tuition invoice numbers are unique within a workspace.
        builder.HasIndex(payment => new { payment.TenantId, payment.InvoiceNumber })
            .IsUnique()
            .HasDatabaseName("UX_EducationalPayments_TenantId_InvoiceNumber");

        builder.HasIndex(payment => new { payment.TenantId, payment.Status, payment.CreatedAt })
            .HasDatabaseName("IX_EducationalPayments_TenantId_Status_CreatedAt");

        builder.HasIndex(payment => new { payment.TenantId, payment.StudentId })
            .HasDatabaseName("IX_EducationalPayments_TenantId_StudentId");

        builder.HasIndex(payment => payment.ParentId).HasDatabaseName("IX_EducationalPayments_ParentId");
        builder.HasIndex(payment => payment.GroupId).HasDatabaseName("IX_EducationalPayments_GroupId");
        builder.HasIndex(payment => payment.CourseId).HasDatabaseName("IX_EducationalPayments_CourseId");
        builder.HasIndex(payment => payment.PaidAt).HasDatabaseName("IX_EducationalPayments_PaidAt");

        // Idempotency for online tuition providers, scoped to this system only.
        builder.HasIndex(payment => new { payment.Provider, payment.ProviderTransactionId })
            .IsUnique()
            .HasFilter("[ProviderTransactionId] IS NOT NULL")
            .HasDatabaseName("UX_EducationalPayments_Provider_TransactionId");

        builder.HasOne(payment => payment.Tenant)
            .WithMany()
            .HasForeignKey(payment => payment.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(payment => payment.Student)
            .WithMany()
            .HasForeignKey(payment => payment.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(payment => payment.Parent)
            .WithMany()
            .HasForeignKey(payment => payment.ParentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(payment => payment.Group)
            .WithMany()
            .HasForeignKey(payment => payment.GroupId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(payment => payment.Course)
            .WithMany()
            .HasForeignKey(payment => payment.CourseId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(payment => payment.GroupEnrollment)
            .WithMany()
            .HasForeignKey(payment => payment.GroupEnrollmentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(payment => payment.CourseEnrollment)
            .WithMany()
            .HasForeignKey(payment => payment.CourseEnrollmentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(payment => payment.Discount)
            .WithMany()
            .HasForeignKey(payment => payment.DiscountId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
