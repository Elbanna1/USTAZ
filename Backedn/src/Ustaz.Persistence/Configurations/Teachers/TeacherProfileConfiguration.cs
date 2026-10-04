using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Teachers;

public sealed class TeacherProfileConfiguration : BaseEntityConfiguration<TeacherProfile>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TeacherProfile> builder)
    {
        builder.ToTable("TeacherProfiles");

        builder.Property(teacher => teacher.Bio)
            .HasMaxLength(DomainRules.DescriptionMaxLength);

        builder.Property(teacher => teacher.Headline)
            .HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.Property(teacher => teacher.CoverImageUrl)
            .HasMaxLength(DomainRules.UrlMaxLength);

        builder.Property(teacher => teacher.ProfileImageUrl)
            .HasMaxLength(DomainRules.UrlMaxLength);

        builder.Property(teacher => teacher.CoverImagePublicId).HasMaxLength(300);
        builder.Property(teacher => teacher.ProfileImagePublicId).HasMaxLength(300);
        builder.Property(teacher => teacher.City).HasMaxLength(100);
        builder.Property(teacher => teacher.WhatsAppNumber).HasMaxLength(DomainRules.PhoneMaxLength);
        builder.Property(teacher => teacher.VerificationNotes).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.Property(teacher => teacher.Rating)
            .HasPrecision(3, 2);

        builder.Property(teacher => teacher.VerificationStatus)
            .HasConversion<int>();

        builder.Property(teacher => teacher.PreferredTeachingMode)
            .HasConversion<int>();

        // Derived state, computed from mapped columns — never persisted.
        builder.Ignore(teacher => teacher.CanPublish);
        builder.Ignore(teacher => teacher.IsReadyForVerification);
        builder.Ignore(teacher => teacher.HasVerificationInReview);
        builder.Ignore(teacher => teacher.CanSubmitForVerification);

        builder.HasIndex(teacher => teacher.UserId)
            .IsUnique()
            .HasDatabaseName("IX_TeacherProfiles_UserId");

        // Each teacher owns exactly one workspace.
        builder.HasIndex(teacher => teacher.TenantId)
            .IsUnique()
            .HasDatabaseName("UX_TeacherProfiles_TenantId");

        builder.HasOne(teacher => teacher.Tenant)
            .WithMany()
            .HasForeignKey(teacher => teacher.TenantId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(teacher => teacher.IsFeatured)
            .HasDatabaseName("IX_TeacherProfiles_IsFeatured");

        builder.HasIndex(teacher => teacher.IsVerified)
            .HasDatabaseName("IX_TeacherProfiles_IsVerified");

        builder.HasIndex(teacher => teacher.Rating)
            .HasDatabaseName("IX_TeacherProfiles_Rating");

        // Covers the default public listing: verified teachers ordered by rating.
        builder.HasIndex(teacher => new { teacher.IsVerified, teacher.IsFeatured, teacher.Rating })
            .HasDatabaseName("IX_TeacherProfiles_Verified_Featured_Rating");

        builder.HasIndex(teacher => teacher.City)
            .HasDatabaseName("IX_TeacherProfiles_City");

        builder.HasMany(teacher => teacher.Achievements)
            .WithOne(achievement => achievement.Teacher)
            .HasForeignKey(achievement => achievement.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(teacher => teacher.SocialLinks)
            .WithOne(link => link.Teacher)
            .HasForeignKey(link => link.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
