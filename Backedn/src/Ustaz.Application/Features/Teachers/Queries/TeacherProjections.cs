using System.Linq.Expressions;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Domain.Entities;

namespace Ustaz.Application.Features.Teachers.Queries;

/// <summary>
/// Server side projections. Every teacher query selects straight into a DTO so EF Core
/// never materialises entities and no N+1 query is possible.
/// </summary>
internal static class TeacherProjections
{
    public static Expression<Func<TeacherProfile, TeacherListItemDto>> ToListItem =>
        teacher => new TeacherListItemDto
        {
            Id = teacher.Id,
            UserId = teacher.UserId,
            DisplayName = teacher.User.DisplayName,
            Headline = teacher.Headline,
            ProfileImageUrl = teacher.ProfileImageUrl ?? teacher.User.ProfileImageUrl,
            CoverImageUrl = teacher.CoverImageUrl,
            City = teacher.City,
            YearsOfExperience = teacher.YearsOfExperience,
            StudentsCount = teacher.StudentsCount,
            Rating = teacher.Rating,
            ReviewCount = teacher.ReviewCount,
            IsFeatured = teacher.IsFeatured,
            IsVerified = teacher.IsVerified,
            PreferredTeachingMode = teacher.PreferredTeachingMode,
            GroupsCount = teacher.Groups.Count(group => group.IsPublished && group.IsActive),
            CoursesCount = teacher.Courses.Count(course => course.IsPublished),
            Subjects = teacher.TeacherSubjects
                .OrderBy(link => link.Subject.SortOrder)
                .Select(link => new SubjectDto
                {
                    Id = link.Subject.Id,
                    NameAr = link.Subject.NameAr,
                    NameEn = link.Subject.NameEn,
                    Slug = link.Subject.Slug,
                    Icon = link.Subject.Icon,
                    ColorHex = link.Subject.ColorHex,
                    IsActive = link.Subject.IsActive
                })
                .ToList(),
            Grades = teacher.TeacherGrades
                .OrderBy(link => link.Grade.SortOrder)
                .Select(link => new GradeDto
                {
                    Id = link.Grade.Id,
                    NameAr = link.Grade.NameAr,
                    NameEn = link.Grade.NameEn,
                    Slug = link.Grade.Slug,
                    Stage = link.Grade.Stage,
                    SortOrder = link.Grade.SortOrder,
                    IsActive = link.Grade.IsActive
                })
                .ToList()
        };

    public static Expression<Func<TeacherProfile, TeacherDetailsDto>> ToDetails =>
        teacher => new TeacherDetailsDto
        {
            Id = teacher.Id,
            UserId = teacher.UserId,
            DisplayName = teacher.User.DisplayName,
            FirstName = teacher.User.FirstName,
            LastName = teacher.User.LastName,
            Headline = teacher.Headline,
            Bio = teacher.Bio,
            ProfileImageUrl = teacher.ProfileImageUrl ?? teacher.User.ProfileImageUrl,
            CoverImageUrl = teacher.CoverImageUrl,
            City = teacher.City,
            YearsOfExperience = teacher.YearsOfExperience,
            StudentsCount = teacher.StudentsCount,
            Rating = teacher.Rating,
            ReviewCount = teacher.ReviewCount,
            IsFeatured = teacher.IsFeatured,
            IsVerified = teacher.IsVerified,
            VerificationStatus = teacher.VerificationStatus,
            PreferredTeachingMode = teacher.PreferredTeachingMode,
            GroupsCount = teacher.Groups.Count(group => group.IsPublished && group.IsActive),
            CoursesCount = teacher.Courses.Count(course => course.IsPublished),
            CreatedAt = teacher.CreatedAt,
            Subjects = teacher.TeacherSubjects
                .OrderBy(link => link.Subject.SortOrder)
                .Select(link => new SubjectDto
                {
                    Id = link.Subject.Id,
                    NameAr = link.Subject.NameAr,
                    NameEn = link.Subject.NameEn,
                    Slug = link.Subject.Slug,
                    Icon = link.Subject.Icon,
                    ColorHex = link.Subject.ColorHex,
                    Description = link.Subject.Description,
                    IsActive = link.Subject.IsActive
                })
                .ToList(),
            Grades = teacher.TeacherGrades
                .OrderBy(link => link.Grade.SortOrder)
                .Select(link => new GradeDto
                {
                    Id = link.Grade.Id,
                    NameAr = link.Grade.NameAr,
                    NameEn = link.Grade.NameEn,
                    Slug = link.Grade.Slug,
                    Stage = link.Grade.Stage,
                    SortOrder = link.Grade.SortOrder,
                    IsActive = link.Grade.IsActive
                })
                .ToList(),
            Achievements = teacher.Achievements
                .OrderBy(achievement => achievement.SortOrder)
                .Select(achievement => new TeacherAchievementDto
                {
                    Id = achievement.Id,
                    Title = achievement.Title,
                    Description = achievement.Description,
                    Year = achievement.Year,
                    ImageUrl = achievement.ImageUrl,
                    SortOrder = achievement.SortOrder
                })
                .ToList(),
            SocialLinks = teacher.SocialLinks
                .OrderBy(link => link.SortOrder)
                .Select(link => new TeacherSocialLinkDto
                {
                    Id = link.Id,
                    Platform = link.Platform,
                    Url = link.Url,
                    SortOrder = link.SortOrder
                })
                .ToList()
        };

    public static Expression<Func<TeacherProfile, TeacherSummaryDto>> ToSummary =>
        teacher => new TeacherSummaryDto
        {
            Id = teacher.Id,
            UserId = teacher.UserId,
            DisplayName = teacher.User.DisplayName,
            Headline = teacher.Headline,
            ProfileImageUrl = teacher.ProfileImageUrl ?? teacher.User.ProfileImageUrl,
            City = teacher.City,
            Rating = teacher.Rating,
            ReviewCount = teacher.ReviewCount,
            IsVerified = teacher.IsVerified
        };
}
