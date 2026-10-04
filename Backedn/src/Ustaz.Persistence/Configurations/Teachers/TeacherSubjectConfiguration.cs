using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Teachers;

public sealed class TeacherSubjectConfiguration : BaseEntityConfiguration<TeacherSubject>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TeacherSubject> builder)
    {
        builder.ToTable("TeacherSubjects");

        builder.HasIndex(link => new { link.TeacherId, link.SubjectId })
            .IsUnique()
            .HasDatabaseName("UX_TeacherSubjects_TeacherId_SubjectId");

        builder.HasOne(link => link.Teacher)
            .WithMany(teacher => teacher.TeacherSubjects)
            .HasForeignKey(link => link.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.Subject)
            .WithMany(subject => subject.TeacherSubjects)
            .HasForeignKey(link => link.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
