using System.Linq.Expressions;
using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Domain.Entities;

namespace Ustaz.Application.Features.Groups.Queries;

internal static partial class GroupProjectionsExtra
{
    /// <summary>
    /// Full detail projection without materials: material visibility depends on the caller,
    /// so it is resolved separately once the principal is known.
    /// </summary>
    public static Expression<Func<Group, GroupDetailsDto>> ToDetails =>
        group => new GroupDetailsDto
        {
            Id = group.Id,
            Name = group.Name,
            Slug = group.Slug,
            Description = group.Description,
            Type = group.Type,
            TeachingMode = group.TeachingMode,
            Location = group.Location,
            Pricing = new PricingDto
            {
                Price = group.MonthlyPrice,
                OriginalPrice = group.OriginalPrice,
                DiscountPercentage = group.DiscountPercentage,
                HasDiscount = group.DiscountPercentage > 0
                    || (group.OriginalPrice != null && group.OriginalPrice > group.MonthlyPrice),
                IsFree = group.MonthlyPrice <= 0m
            },
            Capacity = group.Capacity,
            EnrolledStudentsCount = group.EnrolledStudentsCount,
            AvailableSeats = group.Capacity - group.EnrolledStudentsCount < 0
                ? 0
                : group.Capacity - group.EnrolledStudentsCount,
            IsFull = group.EnrolledStudentsCount >= group.Capacity,
            StartDate = group.StartDate,
            EndDate = group.EndDate,
            IsPublished = group.IsPublished,
            IsActive = group.IsActive,
            Rating = group.Rating,
            ReviewCount = group.ReviewCount,
            WhatsAppLink = group.WhatsAppLink,
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt,
            Teacher = new TeacherSummaryDto
            {
                Id = group.Teacher.Id,
                UserId = group.Teacher.UserId,
                DisplayName = group.Teacher.User.DisplayName,
                Headline = group.Teacher.Headline,
                ProfileImageUrl = group.Teacher.ProfileImageUrl ?? group.Teacher.User.ProfileImageUrl,
                City = group.Teacher.City,
                Rating = group.Teacher.Rating,
                ReviewCount = group.Teacher.ReviewCount,
                IsVerified = group.Teacher.IsVerified
            },
            Subject = new SubjectDto
            {
                Id = group.Subject.Id,
                NameAr = group.Subject.NameAr,
                NameEn = group.Subject.NameEn,
                Slug = group.Subject.Slug,
                Description = group.Subject.Description,
                Icon = group.Subject.Icon,
                ColorHex = group.Subject.ColorHex,
                IsActive = group.Subject.IsActive
            },
            Grade = new GradeDto
            {
                Id = group.Grade.Id,
                NameAr = group.Grade.NameAr,
                NameEn = group.Grade.NameEn,
                Slug = group.Grade.Slug,
                Stage = group.Grade.Stage,
                SortOrder = group.Grade.SortOrder,
                IsActive = group.Grade.IsActive
            },
            Schedules = group.Schedules
                .OrderBy(schedule => schedule.DayOfWeek)
                .ThenBy(schedule => schedule.StartTime)
                .Select(schedule => new GroupScheduleDto
                {
                    Id = schedule.Id,
                    GroupId = schedule.GroupId,
                    DayOfWeek = schedule.DayOfWeek,
                    StartTime = schedule.StartTime,
                    EndTime = schedule.EndTime,
                    Location = schedule.Location,
                    MeetingUrl = schedule.MeetingUrl,
                    IsActive = schedule.IsActive
                })
                .ToList(),
            Curriculum = group.Curriculum
                .OrderBy(item => item.SortOrder)
                .Select(item => new GroupCurriculumDto
                {
                    Id = item.Id,
                    GroupId = item.GroupId,
                    Title = item.Title,
                    Description = item.Description,
                    SortOrder = item.SortOrder
                })
                .ToList()
        };
}
