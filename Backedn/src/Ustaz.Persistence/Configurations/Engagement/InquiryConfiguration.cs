using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Engagement;

public sealed class InquiryConfiguration : BaseEntityConfiguration<Inquiry>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Inquiry> builder)
    {
        builder.ToTable("Inquiries");

        builder.Property(inquiry => inquiry.Name).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(inquiry => inquiry.PhoneNumber).HasMaxLength(DomainRules.PhoneMaxLength).IsRequired();
        builder.Property(inquiry => inquiry.Email).HasMaxLength(256);
        builder.Property(inquiry => inquiry.Message).HasMaxLength(DomainRules.DescriptionMaxLength).IsRequired();
        builder.Property(inquiry => inquiry.TeacherNote).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(inquiry => inquiry.Status).HasConversion<int>();

        builder.HasIndex(inquiry => new { inquiry.TeacherId, inquiry.Status })
            .HasDatabaseName("IX_Inquiries_TeacherId_Status");

        builder.HasIndex(inquiry => inquiry.StudentId).HasDatabaseName("IX_Inquiries_StudentId");

        builder.HasIndex(inquiry => new { inquiry.TenantId, inquiry.Status })
            .HasDatabaseName("IX_Inquiries_TenantId_Status");
        builder.HasIndex(inquiry => inquiry.CreatedAt).HasDatabaseName("IX_Inquiries_CreatedAt");

        builder.HasOne(inquiry => inquiry.Teacher)
            .WithMany(teacher => teacher.Inquiries)
            .HasForeignKey(inquiry => inquiry.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        // Only the teacher relationship cascades; the others are NoAction so SQL Server does
        // not see multiple cascade paths. The delete handlers detach inquiries explicitly.
        builder.HasOne(inquiry => inquiry.Student)
            .WithMany(student => student.Inquiries)
            .HasForeignKey(inquiry => inquiry.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(inquiry => inquiry.Group)
            .WithMany()
            .HasForeignKey(inquiry => inquiry.GroupId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(inquiry => inquiry.Course)
            .WithMany()
            .HasForeignKey(inquiry => inquiry.CourseId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
