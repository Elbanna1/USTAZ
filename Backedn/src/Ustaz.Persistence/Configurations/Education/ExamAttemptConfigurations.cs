using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;

namespace Ustaz.Persistence.Configurations.Education;

public sealed class ExamAttemptConfiguration : BaseEntityConfiguration<ExamAttempt>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ExamAttempt> builder)
    {
        builder.ToTable("ExamAttempts");

        builder.Property(attempt => attempt.Status).HasConversion<int>();
        builder.Property(attempt => attempt.Score).HasPrecision(8, 2);
        builder.Property(attempt => attempt.MaximumScore).HasPrecision(8, 2);
        builder.Property(attempt => attempt.Percentage).HasPrecision(5, 2);
        builder.Property(attempt => attempt.TeacherComment).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.Ignore(attempt => attempt.IsOpen);

        builder.Property(attempt => attempt.RowVersion).IsRowVersion();

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_ExamAttempts_AttemptNumber", "[AttemptNumber] > 0"));

        // One row per sitting: the number is what makes a retake distinguishable, and the
        // unique index is what stops a double-click from opening two parallel attempts.
        builder.HasIndex(attempt => new { attempt.ExamId, attempt.StudentId, attempt.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("UX_ExamAttempts_Exam_Student_Attempt");

        builder.HasIndex(attempt => new { attempt.TenantId, attempt.Status })
            .HasDatabaseName("IX_ExamAttempts_TenantId_Status");

        builder.HasIndex(attempt => attempt.StudentId).HasDatabaseName("IX_ExamAttempts_StudentId");

        builder.HasOne(attempt => attempt.Exam)
            .WithMany(exam => exam.Attempts)
            .HasForeignKey(attempt => attempt.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(attempt => attempt.Student)
            .WithMany()
            .HasForeignKey(attempt => attempt.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(attempt => attempt.Answers)
            .WithOne(answer => answer.Attempt)
            .HasForeignKey(answer => answer.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ExamAttemptAnswerConfiguration : BaseEntityConfiguration<ExamAttemptAnswer>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ExamAttemptAnswer> builder)
    {
        builder.ToTable("ExamAttemptAnswers");

        builder.Property(answer => answer.TextAnswer).HasMaxLength(DomainRules.DescriptionMaxLength);
        builder.Property(answer => answer.TeacherFeedback).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(answer => answer.AwardedPoints).HasPrecision(8, 2);
        builder.Property(answer => answer.MaximumPoints).HasPrecision(8, 2);

        // One answer per question per attempt.
        builder.HasIndex(answer => new { answer.AttemptId, answer.QuestionId })
            .IsUnique()
            .HasDatabaseName("UX_ExamAttemptAnswers_Attempt_Question");

        builder.HasOne(answer => answer.Question)
            .WithMany()
            .HasForeignKey(answer => answer.QuestionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
