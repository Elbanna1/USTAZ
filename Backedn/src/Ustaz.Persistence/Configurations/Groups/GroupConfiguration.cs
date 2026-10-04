using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Groups;

public sealed class GroupConfiguration : BaseEntityConfiguration<Group>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("Groups");

        builder.Property(group => group.Name).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(group => group.Slug).HasMaxLength(DomainRules.SlugMaxLength).IsRequired();
        builder.Property(group => group.Description).HasMaxLength(DomainRules.DescriptionMaxLength);
        builder.Property(group => group.Location).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(group => group.WhatsAppLink).HasMaxLength(DomainRules.UrlMaxLength);

        builder.Property(group => group.Type).HasConversion<int>();
        builder.Property(group => group.TeachingMode).HasConversion<int>();

        builder.Property(group => group.MonthlyPrice).HasPrecision(18, 2);
        builder.Property(group => group.OriginalPrice).HasPrecision(18, 2);
        builder.Property(group => group.Rating).HasPrecision(3, 2);

        builder.Ignore(group => group.AvailableSeats);
        builder.Ignore(group => group.IsFull);
        builder.Ignore(group => group.HasDiscount);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Groups_Capacity", "[Capacity] > 0");
            table.HasCheckConstraint("CK_Groups_MonthlyPrice", "[MonthlyPrice] >= 0");
            table.HasCheckConstraint("CK_Groups_DiscountPercentage", "[DiscountPercentage] >= 0 AND [DiscountPercentage] <= 100");
            table.HasCheckConstraint("CK_Groups_EnrolledStudentsCount", "[EnrolledStudentsCount] >= 0");
        });

        builder.HasIndex(group => group.TeacherId).HasDatabaseName("IX_Groups_TeacherId");

        // Tenant-scoped workspace listings hit this index.
        builder.HasIndex(group => new { group.TenantId, group.IsActive })
            .HasDatabaseName("IX_Groups_TenantId_IsActive");
        builder.HasIndex(group => group.SubjectId).HasDatabaseName("IX_Groups_SubjectId");
        builder.HasIndex(group => group.GradeId).HasDatabaseName("IX_Groups_GradeId");
        builder.HasIndex(group => group.IsPublished).HasDatabaseName("IX_Groups_IsPublished");

        builder.HasIndex(group => group.Slug)
            .IsUnique()
            .HasDatabaseName("UX_Groups_Slug");

        // Covers the public discovery query: published + active filtered by subject and grade.
        builder.HasIndex(group => new { group.IsPublished, group.IsActive, group.SubjectId, group.GradeId })
            .HasDatabaseName("IX_Groups_Published_Active_Subject_Grade");

        builder.HasOne(group => group.Teacher)
            .WithMany(teacher => teacher.Groups)
            .HasForeignKey(group => group.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(group => group.Subject)
            .WithMany(subject => subject.Groups)
            .HasForeignKey(group => group.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(group => group.Grade)
            .WithMany(grade => grade.Groups)
            .HasForeignKey(group => group.GradeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(group => group.Schedules)
            .WithOne(schedule => schedule.Group)
            .HasForeignKey(schedule => schedule.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(group => group.Curriculum)
            .WithOne(item => item.Group)
            .HasForeignKey(item => item.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(group => group.Materials)
            .WithOne(material => material.Group)
            .HasForeignKey(material => material.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
