using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Entities;

namespace Ustaz.Persistence.Configurations.Engagement;

public sealed class TeacherFavoriteConfiguration : BaseEntityConfiguration<TeacherFavorite>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TeacherFavorite> builder)
    {
        builder.ToTable("TeacherFavorites");

        builder.HasIndex(favorite => new { favorite.StudentId, favorite.TeacherId })
            .IsUnique()
            .HasDatabaseName("UX_TeacherFavorites_Student_Teacher");

        // NoAction on the student side keeps SQL Server to a single cascade path
        // (User -> TeacherProfile -> TeacherFavorites).
        builder.HasOne(favorite => favorite.Student)
            .WithMany(student => student.FavoriteTeachers)
            .HasForeignKey(favorite => favorite.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(favorite => favorite.Teacher)
            .WithMany(teacher => teacher.FavouritedBy)
            .HasForeignKey(favorite => favorite.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CourseFavoriteConfiguration : BaseEntityConfiguration<CourseFavorite>
{
    protected override void ConfigureEntity(EntityTypeBuilder<CourseFavorite> builder)
    {
        builder.ToTable("CourseFavorites");

        builder.HasIndex(favorite => new { favorite.StudentId, favorite.CourseId })
            .IsUnique()
            .HasDatabaseName("UX_CourseFavorites_Student_Course");

        // NoAction on the student side keeps SQL Server to a single cascade path
        // (User -> TeacherProfile -> Course -> CourseFavorites).
        builder.HasOne(favorite => favorite.Student)
            .WithMany(student => student.FavoriteCourses)
            .HasForeignKey(favorite => favorite.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(favorite => favorite.Course)
            .WithMany(course => course.FavouritedBy)
            .HasForeignKey(favorite => favorite.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
