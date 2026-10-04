using System.Linq.Expressions;
using Ustaz.Application.Features.Courses.Dtos;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Domain.Entities;

namespace Ustaz.Application.Features.Courses.Queries;

internal static class CourseProjections
{
    public static Expression<Func<Course, CourseListItemDto>> ToListItem =>
        course => new CourseListItemDto
        {
            Id = course.Id,
            Title = course.Title,
            Slug = course.Slug,
            Description = course.Description,
            ThumbnailUrl = course.ThumbnailUrl,
            Pricing = new PricingDto
            {
                Price = course.Price,
                OriginalPrice = course.OriginalPrice,
                DiscountPercentage = course.DiscountPercentage,
                HasDiscount = course.DiscountPercentage > 0
                    || (course.OriginalPrice != null && course.OriginalPrice > course.Price),
                IsFree = course.Price <= 0m
            },
            DurationInMinutes = course.DurationInMinutes,
            LessonsCount = course.LessonsCount,
            Rating = course.Rating,
            ReviewCount = course.ReviewCount,
            StudentCount = course.StudentCount,
            IsPublished = course.IsPublished,
            IsFeatured = course.IsFeatured,
            CreatedAt = course.CreatedAt,
            Teacher = new TeacherSummaryDto
            {
                Id = course.Teacher.Id,
                UserId = course.Teacher.UserId,
                DisplayName = course.Teacher.User.DisplayName,
                Headline = course.Teacher.Headline,
                ProfileImageUrl = course.Teacher.ProfileImageUrl ?? course.Teacher.User.ProfileImageUrl,
                City = course.Teacher.City,
                Rating = course.Teacher.Rating,
                ReviewCount = course.Teacher.ReviewCount,
                IsVerified = course.Teacher.IsVerified
            },
            Subject = new SubjectDto
            {
                Id = course.Subject.Id,
                NameAr = course.Subject.NameAr,
                NameEn = course.Subject.NameEn,
                Slug = course.Subject.Slug,
                Icon = course.Subject.Icon,
                ColorHex = course.Subject.ColorHex,
                IsActive = course.Subject.IsActive
            },
            Grade = new GradeDto
            {
                Id = course.Grade.Id,
                NameAr = course.Grade.NameAr,
                NameEn = course.Grade.NameEn,
                Slug = course.Grade.Slug,
                Stage = course.Grade.Stage,
                SortOrder = course.Grade.SortOrder,
                IsActive = course.Grade.IsActive
            }
        };

    public static Expression<Func<Course, CourseDetailsDto>> ToDetailsWithoutContent =>
        course => new CourseDetailsDto
        {
            Id = course.Id,
            Title = course.Title,
            Slug = course.Slug,
            Description = course.Description,
            ThumbnailUrl = course.ThumbnailUrl,
            Pricing = new PricingDto
            {
                Price = course.Price,
                OriginalPrice = course.OriginalPrice,
                DiscountPercentage = course.DiscountPercentage,
                HasDiscount = course.DiscountPercentage > 0
                    || (course.OriginalPrice != null && course.OriginalPrice > course.Price),
                IsFree = course.Price <= 0m
            },
            DurationInMinutes = course.DurationInMinutes,
            LessonsCount = course.LessonsCount,
            Rating = course.Rating,
            ReviewCount = course.ReviewCount,
            StudentCount = course.StudentCount,
            IsPublished = course.IsPublished,
            IsFeatured = course.IsFeatured,
            CreatedAt = course.CreatedAt,
            UpdatedAt = course.UpdatedAt,
            Teacher = new TeacherSummaryDto
            {
                Id = course.Teacher.Id,
                UserId = course.Teacher.UserId,
                DisplayName = course.Teacher.User.DisplayName,
                Headline = course.Teacher.Headline,
                ProfileImageUrl = course.Teacher.ProfileImageUrl ?? course.Teacher.User.ProfileImageUrl,
                City = course.Teacher.City,
                Rating = course.Teacher.Rating,
                ReviewCount = course.Teacher.ReviewCount,
                IsVerified = course.Teacher.IsVerified
            },
            Subject = new SubjectDto
            {
                Id = course.Subject.Id,
                NameAr = course.Subject.NameAr,
                NameEn = course.Subject.NameEn,
                Slug = course.Subject.Slug,
                Description = course.Subject.Description,
                Icon = course.Subject.Icon,
                ColorHex = course.Subject.ColorHex,
                IsActive = course.Subject.IsActive
            },
            Grade = new GradeDto
            {
                Id = course.Grade.Id,
                NameAr = course.Grade.NameAr,
                NameEn = course.Grade.NameEn,
                Slug = course.Grade.Slug,
                Stage = course.Grade.Stage,
                SortOrder = course.Grade.SortOrder,
                IsActive = course.Grade.IsActive
            }
        };
}
