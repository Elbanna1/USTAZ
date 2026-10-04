using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;

namespace Ustaz.Persistence.Configurations.Education;

public sealed class ExamConfiguration : BaseEntityConfiguration<Exam>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("Exams");

        builder.Property(exam => exam.Title).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(exam => exam.Description).HasMaxLength(DomainRules.DescriptionMaxLength);
        builder.Property(exam => exam.Type).HasConversion<int>();
        builder.Property(exam => exam.MaximumScore).HasPrecision(8, 2);
        builder.Property(exam => exam.PassingScore).HasPrecision(8, 2);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Exams_MaximumScore", "[MaximumScore] > 0");
            table.HasCheckConstraint("CK_Exams_PassingScore", "[PassingScore] >= 0 AND [PassingScore] <= [MaximumScore]");
            table.HasCheckConstraint("CK_Exams_Duration", "[DurationInMinutes] >= 0");
        });

        builder.HasIndex(exam => new { exam.TenantId, exam.ExamDate })
            .HasDatabaseName("IX_Exams_TenantId_ExamDate");

        builder.HasIndex(exam => new { exam.TenantId, exam.IsPublished })
            .HasDatabaseName("IX_Exams_TenantId_IsPublished");

        builder.HasIndex(exam => exam.GroupId).HasDatabaseName("IX_Exams_GroupId");
        builder.HasIndex(exam => exam.TeacherId).HasDatabaseName("IX_Exams_TeacherId");
        builder.HasIndex(exam => exam.SubjectId).HasDatabaseName("IX_Exams_SubjectId");

        builder.HasOne(exam => exam.Tenant)
            .WithMany()
            .HasForeignKey(exam => exam.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(exam => exam.Teacher)
            .WithMany()
            .HasForeignKey(exam => exam.TeacherId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(exam => exam.Group)
            .WithMany()
            .HasForeignKey(exam => exam.GroupId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(exam => exam.Subject)
            .WithMany()
            .HasForeignKey(exam => exam.SubjectId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(exam => exam.Grade)
            .WithMany()
            .HasForeignKey(exam => exam.GradeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(exam => exam.Results)
            .WithOne(result => result.Exam)
            .HasForeignKey(result => result.ExamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ExamResultConfiguration : BaseEntityConfiguration<ExamResult>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ExamResult> builder)
    {
        builder.ToTable("ExamResults");

        builder.Property(result => result.Score).HasPrecision(8, 2);
        builder.Property(result => result.MaximumScore).HasPrecision(8, 2);
        builder.Property(result => result.Percentage).HasPrecision(5, 2);
        builder.Property(result => result.TeacherComment).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ExamResults_Score", "[Score] >= 0");
            table.HasCheckConstraint("CK_ExamResults_Percentage", "[Percentage] >= 0 AND [Percentage] <= 100");
        });

        // One result per student per exam.
        builder.HasIndex(result => new { result.ExamId, result.StudentId })
            .IsUnique()
            .HasDatabaseName("UX_ExamResults_ExamId_StudentId");

        builder.HasIndex(result => new { result.TenantId, result.StudentId })
            .HasDatabaseName("IX_ExamResults_TenantId_StudentId");

        // Workspace analytics reads results over a rolling window, not per student.
        builder.HasIndex(result => new { result.TenantId, result.RecordedAt })
            .HasDatabaseName("IX_ExamResults_TenantId_RecordedAt");

        builder.HasIndex(result => result.StudentId).HasDatabaseName("IX_ExamResults_StudentId");

        builder.HasOne(result => result.Student)
            .WithMany()
            .HasForeignKey(result => result.StudentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
