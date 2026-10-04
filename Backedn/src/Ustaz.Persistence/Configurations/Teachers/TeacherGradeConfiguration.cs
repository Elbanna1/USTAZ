using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Teachers;

public sealed class TeacherGradeConfiguration : BaseEntityConfiguration<TeacherGrade>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TeacherGrade> builder)
    {
        builder.ToTable("TeacherGrades");

        builder.HasIndex(link => new { link.TeacherId, link.GradeId })
            .IsUnique()
            .HasDatabaseName("UX_TeacherGrades_TeacherId_GradeId");

        builder.HasOne(link => link.Teacher)
            .WithMany(teacher => teacher.TeacherGrades)
            .HasForeignKey(link => link.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.Grade)
            .WithMany(grade => grade.TeacherGrades)
            .HasForeignKey(link => link.GradeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
