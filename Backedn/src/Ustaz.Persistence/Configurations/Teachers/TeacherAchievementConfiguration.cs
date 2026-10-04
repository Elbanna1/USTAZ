using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Teachers;

public sealed class TeacherAchievementConfiguration : BaseEntityConfiguration<TeacherAchievement>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TeacherAchievement> builder)
    {
        builder.ToTable("TeacherAchievements");

        builder.Property(achievement => achievement.Title).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(achievement => achievement.Description).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(achievement => achievement.ImageUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(achievement => achievement.ImagePublicId).HasMaxLength(300);

        builder.HasIndex(achievement => new { achievement.TeacherId, achievement.SortOrder })
            .HasDatabaseName("IX_TeacherAchievements_TeacherId_SortOrder");
    }
}
