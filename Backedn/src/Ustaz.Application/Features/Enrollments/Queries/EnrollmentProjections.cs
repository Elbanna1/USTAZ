using System.Linq.Expressions;
using Ustaz.Application.Features.Enrollments.Dtos;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Domain.Entities;

namespace Ustaz.Application.Features.Enrollments.Queries;

internal static class EnrollmentProjections
{
    public static Expression<Func<GroupEnrollment, GroupEnrollmentDto>> ToGroupDto =>
        enrollment => new GroupEnrollmentDto
        {
            Id = enrollment.Id,
            StudentId = enrollment.StudentId,
            StudentName = enrollment.Student.User.DisplayName,
            StudentPhoneNumber = enrollment.Student.PhoneNumber,
            GroupId = enrollment.GroupId,
            GroupName = enrollment.Group.Name,
            GroupSlug = enrollment.Group.Slug,
            Status = enrollment.Status,
            EnrolledAt = enrollment.EnrolledAt,
            CancelledAt = enrollment.CancelledAt,
            CompletedAt = enrollment.CompletedAt,
            PricePaid = enrollment.PricePaid,
            DiscountAmount = enrollment.DiscountAmount,
            FinalPrice = enrollment.FinalPrice,
            DiscountCode = enrollment.DiscountCode,
            WhatsAppLink = enrollment.Group.WhatsAppLink,
            Teacher = new TeacherSummaryDto
            {
                Id = enrollment.Group.Teacher.Id,
                UserId = enrollment.Group.Teacher.UserId,
                DisplayName = enrollment.Group.Teacher.User.DisplayName,
                Headline = enrollment.Group.Teacher.Headline,
                ProfileImageUrl = enrollment.Group.Teacher.ProfileImageUrl ?? enrollment.Group.Teacher.User.ProfileImageUrl,
                City = enrollment.Group.Teacher.City,
                Rating = enrollment.Group.Teacher.Rating,
                ReviewCount = enrollment.Group.Teacher.ReviewCount,
                IsVerified = enrollment.Group.Teacher.IsVerified
            },
            Subject = new SubjectDto
            {
                Id = enrollment.Group.Subject.Id,
                NameAr = enrollment.Group.Subject.NameAr,
                NameEn = enrollment.Group.Subject.NameEn,
                Slug = enrollment.Group.Subject.Slug,
                Icon = enrollment.Group.Subject.Icon,
                ColorHex = enrollment.Group.Subject.ColorHex,
                IsActive = enrollment.Group.Subject.IsActive
            },
            Grade = new GradeDto
            {
                Id = enrollment.Group.Grade.Id,
                NameAr = enrollment.Group.Grade.NameAr,
                NameEn = enrollment.Group.Grade.NameEn,
                Slug = enrollment.Group.Grade.Slug,
                Stage = enrollment.Group.Grade.Stage,
                SortOrder = enrollment.Group.Grade.SortOrder,
                IsActive = enrollment.Group.Grade.IsActive
            },
            Schedule = enrollment.Group.Schedules
                .Where(schedule => schedule.IsActive)
                .OrderBy(schedule => schedule.DayOfWeek)
                .ThenBy(schedule => schedule.StartTime)
                .Select(schedule => new ScheduleSlotDto
                {
                    Id = schedule.Id,
                    GroupId = schedule.GroupId,
                    GroupName = enrollment.Group.Name,
                    GroupSlug = enrollment.Group.Slug,
                    TeacherId = enrollment.Group.TeacherId,
                    TeacherName = enrollment.Group.Teacher.User.DisplayName,
                    SubjectId = enrollment.Group.SubjectId,
                    SubjectNameAr = enrollment.Group.Subject.NameAr,
                    SubjectNameEn = enrollment.Group.Subject.NameEn,
                    DayOfWeek = schedule.DayOfWeek,
                    StartTime = schedule.StartTime,
                    EndTime = schedule.EndTime,
                    Location = schedule.Location,
                    MeetingUrl = schedule.MeetingUrl,
                    IsActive = schedule.IsActive
                })
                .ToList()
        };

    public static Expression<Func<CourseEnrollment, CourseEnrollmentDto>> ToCourseDto =>
        enrollment => new CourseEnrollmentDto
        {
            Id = enrollment.Id,
            StudentId = enrollment.StudentId,
            StudentName = enrollment.Student.User.DisplayName,
            CourseId = enrollment.CourseId,
            CourseTitle = enrollment.Course.Title,
            CourseSlug = enrollment.Course.Slug,
            CourseThumbnailUrl = enrollment.Course.ThumbnailUrl,
            Status = enrollment.Status,
            EnrolledAt = enrollment.EnrolledAt,
            CancelledAt = enrollment.CancelledAt,
            CompletedAt = enrollment.CompletedAt,
            LastAccessedAt = enrollment.LastAccessedAt,
            PricePaid = enrollment.PricePaid,
            DiscountAmount = enrollment.DiscountAmount,
            FinalPrice = enrollment.FinalPrice,
            DiscountCode = enrollment.DiscountCode,
            ProgressPercentage = enrollment.ProgressPercentage,
            LessonsCount = enrollment.Course.LessonsCount,
            DurationInMinutes = enrollment.Course.DurationInMinutes,
            Teacher = new TeacherSummaryDto
            {
                Id = enrollment.Course.Teacher.Id,
                UserId = enrollment.Course.Teacher.UserId,
                DisplayName = enrollment.Course.Teacher.User.DisplayName,
                Headline = enrollment.Course.Teacher.Headline,
                ProfileImageUrl = enrollment.Course.Teacher.ProfileImageUrl ?? enrollment.Course.Teacher.User.ProfileImageUrl,
                City = enrollment.Course.Teacher.City,
                Rating = enrollment.Course.Teacher.Rating,
                ReviewCount = enrollment.Course.Teacher.ReviewCount,
                IsVerified = enrollment.Course.Teacher.IsVerified
            },
            Subject = new SubjectDto
            {
                Id = enrollment.Course.Subject.Id,
                NameAr = enrollment.Course.Subject.NameAr,
                NameEn = enrollment.Course.Subject.NameEn,
                Slug = enrollment.Course.Subject.Slug,
                Icon = enrollment.Course.Subject.Icon,
                ColorHex = enrollment.Course.Subject.ColorHex,
                IsActive = enrollment.Course.Subject.IsActive
            },
            Grade = new GradeDto
            {
                Id = enrollment.Course.Grade.Id,
                NameAr = enrollment.Course.Grade.NameAr,
                NameEn = enrollment.Course.Grade.NameEn,
                Slug = enrollment.Course.Grade.Slug,
                Stage = enrollment.Course.Grade.Stage,
                SortOrder = enrollment.Course.Grade.SortOrder,
                IsActive = enrollment.Course.Grade.IsActive
            }
        };
}
