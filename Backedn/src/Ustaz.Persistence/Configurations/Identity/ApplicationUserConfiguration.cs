using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Identity;

namespace Ustaz.Persistence.Configurations.Identity;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.DisplayName)
            .HasMaxLength(DomainRules.NameMaxLength)
            .IsRequired();

        builder.Property(user => user.ProfileImageUrl)
            .HasMaxLength(DomainRules.UrlMaxLength);

        builder.Property(user => user.ProfileImagePublicId)
            .HasMaxLength(300);

        builder.Property(user => user.PreferredLanguage)
            .HasMaxLength(10)
            .IsRequired()
            .HasDefaultValue("ar-EG");

        builder.Property(user => user.City)
            .HasMaxLength(100);

        builder.Ignore(user => user.FullName);

        builder.HasIndex(user => user.Email)
            .HasDatabaseName("IX_Users_Email");

        builder.HasIndex(user => user.IsActive)
            .HasDatabaseName("IX_Users_IsActive");

        builder.HasOne(user => user.TeacherProfile)
            .WithOne(profile => profile.User)
            .HasForeignKey<Domain.Entities.TeacherProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(user => user.StudentProfile)
            .WithOne(profile => profile.User)
            .HasForeignKey<Domain.Entities.StudentProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.RefreshTokens)
            .WithOne(token => token.User)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.Notifications)
            .WithOne(notification => notification.User)
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
