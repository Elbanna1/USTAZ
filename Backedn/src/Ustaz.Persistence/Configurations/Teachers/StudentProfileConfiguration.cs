using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Teachers;

public sealed class StudentProfileConfiguration : BaseEntityConfiguration<StudentProfile>
{
    protected override void ConfigureEntity(EntityTypeBuilder<StudentProfile> builder)
    {
        builder.ToTable("StudentProfiles");

        builder.Property(student => student.SchoolName).HasMaxLength(DomainRules.NameMaxLength);
        builder.Property(student => student.City).HasMaxLength(100);
        builder.Property(student => student.PhoneNumber).HasMaxLength(DomainRules.PhoneMaxLength);
        builder.Property(student => student.ParentPhoneNumber).HasMaxLength(DomainRules.PhoneMaxLength);

        builder.HasIndex(student => student.UserId)
            .IsUnique()
            .HasDatabaseName("IX_StudentProfiles_UserId");

        builder.HasIndex(student => student.GradeId)
            .HasDatabaseName("IX_StudentProfiles_GradeId");

        builder.HasOne(student => student.Grade)
            .WithMany(grade => grade.Students)
            .HasForeignKey(student => student.GradeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
