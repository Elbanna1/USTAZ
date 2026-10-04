using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Courses;

public sealed class CourseLessonProgressConfiguration : BaseEntityConfiguration<CourseLessonProgress>
{
    protected override void ConfigureEntity(EntityTypeBuilder<CourseLessonProgress> builder)
    {
        builder.ToTable("CourseLessonProgress");

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_CourseLessonProgress_WatchedSeconds", "[WatchedSeconds] >= 0"));

        // One row per lesson per enrollment: this is what makes completion idempotent.
        builder.HasIndex(progress => new { progress.CourseEnrollmentId, progress.LessonId })
            .IsUnique()
            .HasDatabaseName("UX_CourseLessonProgress_Enrollment_Lesson");

        builder.HasIndex(progress => new { progress.StudentId, progress.CourseId })
            .HasDatabaseName("IX_CourseLessonProgress_StudentId_CourseId");

        builder.HasIndex(progress => progress.LessonId)
            .HasDatabaseName("IX_CourseLessonProgress_LessonId");

        // The enrollment owns the row logically, but deleting a lesson is the operation that
        // actually happens, so the cascade hangs off the lesson and the enrollment link is
        // NoAction. Enrollments are cancelled, never hard deleted.
        builder.HasOne(progress => progress.CourseEnrollment)
            .WithMany(enrollment => enrollment.LessonProgress)
            .HasForeignKey(progress => progress.CourseEnrollmentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(progress => progress.Lesson)
            .WithMany()
            .HasForeignKey(progress => progress.LessonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
