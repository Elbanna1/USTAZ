using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;

namespace Ustaz.Persistence.Configurations.Education;

public sealed class ExamQuestionConfiguration : BaseEntityConfiguration<ExamQuestion>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ExamQuestion> builder)
    {
        builder.ToTable("ExamQuestions");

        builder.Property(question => question.Text)
            .HasMaxLength(DomainRules.DescriptionMaxLength)
            .IsRequired();

        builder.Property(question => question.Type).HasConversion<int>();
        builder.Property(question => question.Points).HasPrecision(8, 2);
        builder.Property(question => question.CorrectAnswer).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(question => question.Explanation).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.Ignore(question => question.IsAutoGradable);

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_ExamQuestions_Points", "[Points] > 0"));

        builder.HasIndex(question => new { question.ExamId, question.SortOrder })
            .HasDatabaseName("IX_ExamQuestions_ExamId_SortOrder");

        builder.HasIndex(question => question.TenantId).HasDatabaseName("IX_ExamQuestions_TenantId");

        builder.HasOne(question => question.Exam)
            .WithMany(exam => exam.Questions)
            .HasForeignKey(question => question.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(question => question.Options)
            .WithOne(option => option.Question)
            .HasForeignKey(option => option.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ExamQuestionOptionConfiguration : BaseEntityConfiguration<ExamQuestionOption>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ExamQuestionOption> builder)
    {
        builder.ToTable("ExamQuestionOptions");

        builder.Property(option => option.Text)
            .HasMaxLength(DomainRules.ShortTextMaxLength)
            .IsRequired();

        builder.HasIndex(option => new { option.QuestionId, option.SortOrder })
            .HasDatabaseName("IX_ExamQuestionOptions_QuestionId_SortOrder");
    }
}
