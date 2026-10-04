using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;

namespace Ustaz.Persistence.Configurations.Education;

public sealed class HomeworkConfiguration : BaseEntityConfiguration<Homework>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Homework> builder)
    {
        builder.ToTable("Homeworks");

        builder.Property(homework => homework.Title).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(homework => homework.Description).HasMaxLength(DomainRules.DescriptionMaxLength);
        builder.Property(homework => homework.Instructions).HasMaxLength(DomainRules.DescriptionMaxLength);
        builder.Property(homework => homework.MaximumScore).HasPrecision(8, 2);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Homeworks_MaximumScore", "[MaximumScore] > 0"));

        builder.HasIndex(homework => new { homework.TenantId, homework.DueAt })
            .HasDatabaseName("IX_Homeworks_TenantId_DueAt");

        builder.HasIndex(homework => new { homework.GroupId, homework.IsPublished })
            .HasDatabaseName("IX_Homeworks_GroupId_IsPublished");

        builder.HasIndex(homework => homework.TeacherId).HasDatabaseName("IX_Homeworks_TeacherId");

        // The assignment list and the reminder sweep both ask for open work in a workspace, so
        // the closed marker joins the covering index rather than filtering after the read.
        builder.HasIndex(homework => new { homework.TenantId, homework.IsPublished, homework.ClosedAt })
            .HasDatabaseName("IX_Homeworks_TenantId_IsPublished_ClosedAt");

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Homeworks_MaxSubmissions", "[MaxSubmissions] IS NULL OR [MaxSubmissions] > 0"));

        builder.HasOne(homework => homework.Tenant)
            .WithMany()
            .HasForeignKey(homework => homework.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(homework => homework.Teacher)
            .WithMany()
            .HasForeignKey(homework => homework.TeacherId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(homework => homework.Group)
            .WithMany()
            .HasForeignKey(homework => homework.GroupId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(homework => homework.Subject)
            .WithMany()
            .HasForeignKey(homework => homework.SubjectId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(homework => homework.Attachments)
            .WithOne(attachment => attachment.Homework)
            .HasForeignKey(attachment => attachment.HomeworkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(homework => homework.Submissions)
            .WithOne(submission => submission.Homework)
            .HasForeignKey(submission => submission.HomeworkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class HomeworkAttachmentConfiguration : BaseEntityConfiguration<HomeworkAttachment>
{
    protected override void ConfigureEntity(EntityTypeBuilder<HomeworkAttachment> builder)
    {
        builder.ToTable("HomeworkAttachments");

        builder.Property(attachment => attachment.FileName).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(attachment => attachment.FileUrl).HasMaxLength(DomainRules.UrlMaxLength).IsRequired();
        builder.Property(attachment => attachment.FilePublicId).HasMaxLength(300);
        builder.Property(attachment => attachment.FileType).HasConversion<int>();

        builder.HasIndex(attachment => attachment.HomeworkId)
            .HasDatabaseName("IX_HomeworkAttachments_HomeworkId");
    }
}

public sealed class HomeworkSubmissionConfiguration : BaseEntityConfiguration<HomeworkSubmission>
{
    protected override void ConfigureEntity(EntityTypeBuilder<HomeworkSubmission> builder)
    {
        builder.ToTable("HomeworkSubmissions");

        builder.Property(submission => submission.Status).HasConversion<int>();
        builder.Property(submission => submission.Content).HasMaxLength(DomainRules.DescriptionMaxLength);
        builder.Property(submission => submission.FileUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(submission => submission.FilePublicId).HasMaxLength(300);
        builder.Property(submission => submission.TeacherFeedback).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(submission => submission.ReturnReason).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(submission => submission.Score).HasPrecision(8, 2);
        builder.Property(submission => submission.MaximumScore).HasPrecision(8, 2);
        builder.Property(submission => submission.Percentage).HasPrecision(5, 2);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_HomeworkSubmissions_Score", "[Score] IS NULL OR [Score] >= 0"));

        // One submission row per student per assignment.
        builder.HasIndex(submission => new { submission.HomeworkId, submission.StudentId })
            .IsUnique()
            .HasDatabaseName("UX_HomeworkSubmissions_HomeworkId_StudentId");

        builder.HasIndex(submission => new { submission.TenantId, submission.StudentId, submission.Status })
            .HasDatabaseName("IX_HomeworkSubmissions_TenantId_StudentId_Status");

        builder.HasIndex(submission => submission.StudentId)
            .HasDatabaseName("IX_HomeworkSubmissions_StudentId");

        // Drives the marking queue: everything in this workspace still waiting for a teacher.
        builder.HasIndex(submission => new { submission.TenantId, submission.Status, submission.SubmittedAt })
            .HasDatabaseName("IX_HomeworkSubmissions_TenantId_Status_SubmittedAt");

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_HomeworkSubmissions_SubmissionCount", "[SubmissionCount] >= 0"));

        builder.HasOne(submission => submission.Student)
            .WithMany()
            .HasForeignKey(submission => submission.StudentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class AnnouncementConfiguration : BaseEntityConfiguration<Announcement>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("Announcements");

        builder.Property(announcement => announcement.Title).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(announcement => announcement.Body).HasMaxLength(DomainRules.DescriptionMaxLength).IsRequired();
        builder.Property(announcement => announcement.Audience).HasConversion<int>();
        builder.Property(announcement => announcement.ImageUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(announcement => announcement.AttachmentUrl).HasMaxLength(DomainRules.UrlMaxLength);

        builder.HasIndex(announcement => new { announcement.TenantId, announcement.IsPublished, announcement.PublishedAt })
            .HasDatabaseName("IX_Announcements_TenantId_Published");

        builder.HasIndex(announcement => new { announcement.TenantId, announcement.Audience })
            .HasDatabaseName("IX_Announcements_TenantId_Audience");

        builder.HasIndex(announcement => announcement.GroupId)
            .HasDatabaseName("IX_Announcements_GroupId");

        builder.HasOne(announcement => announcement.Tenant)
            .WithMany()
            .HasForeignKey(announcement => announcement.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(announcement => announcement.Group)
            .WithMany()
            .HasForeignKey(announcement => announcement.GroupId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
