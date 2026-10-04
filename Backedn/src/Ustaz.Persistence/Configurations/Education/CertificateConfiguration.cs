using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;

namespace Ustaz.Persistence.Configurations.Education;

public sealed class CertificateConfiguration : BaseEntityConfiguration<Certificate>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Certificate> builder)
    {
        builder.ToTable("Certificates");

        builder.Property(certificate => certificate.CertificateNumber).HasMaxLength(40).IsRequired();
        builder.Property(certificate => certificate.VerificationCode).HasMaxLength(64).IsRequired();
        builder.Property(certificate => certificate.Status).HasConversion<int>();
        builder.Property(certificate => certificate.StudentName).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(certificate => certificate.CourseTitle).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(certificate => certificate.TeacherName).HasMaxLength(DomainRules.NameMaxLength);
        builder.Property(certificate => certificate.WorkspaceName).HasMaxLength(DomainRules.NameMaxLength);
        builder.Property(certificate => certificate.RevocationReason).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.Ignore(certificate => certificate.IsValid);

        builder.HasIndex(certificate => certificate.CertificateNumber)
            .IsUnique()
            .HasDatabaseName("UX_Certificates_Number");

        // Verification looks the row up by this alone, so it has to be unique and indexed.
        builder.HasIndex(certificate => certificate.VerificationCode)
            .IsUnique()
            .HasDatabaseName("UX_Certificates_VerificationCode");

        // One certificate per completed enrollment: issuing is therefore idempotent.
        builder.HasIndex(certificate => certificate.CourseEnrollmentId)
            .IsUnique()
            .HasDatabaseName("UX_Certificates_CourseEnrollmentId");

        builder.HasIndex(certificate => new { certificate.TenantId, certificate.IssuedAt })
            .HasDatabaseName("IX_Certificates_TenantId_IssuedAt");

        builder.HasIndex(certificate => certificate.StudentId)
            .HasDatabaseName("IX_Certificates_StudentId");

        builder.HasOne(certificate => certificate.Student)
            .WithMany()
            .HasForeignKey(certificate => certificate.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(certificate => certificate.Course)
            .WithMany()
            .HasForeignKey(certificate => certificate.CourseId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
