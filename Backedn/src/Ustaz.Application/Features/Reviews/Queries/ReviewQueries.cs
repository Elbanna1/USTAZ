using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Reviews.Dtos;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Reviews.Commands;

/// <summary>Shared projection and single-review loader used by the review handlers.</summary>
internal static class ReviewQueryHelper
{
    public static Expression<Func<Review, ReviewDto>> Projection(Guid? callerStudentId) =>
        review => new ReviewDto
        {
            Id = review.Id,
            StudentId = review.StudentId,
            StudentName = review.Student.User.DisplayName,
            StudentImageUrl = review.Student.User.ProfileImageUrl,
            TeacherId = review.TeacherId,
            TeacherName = review.Teacher.User.DisplayName,
            GroupId = review.GroupId,
            GroupName = review.Group != null ? review.Group.Name : null,
            CourseId = review.CourseId,
            CourseTitle = review.Course != null ? review.Course.Title : null,
            TargetType = review.CourseId != null
                ? ReviewTargetType.Course
                : review.GroupId != null
                    ? ReviewTargetType.Group
                    : ReviewTargetType.Teacher,
            Rating = review.Rating,
            Comment = review.Comment,
            IsApproved = review.IsApproved,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt,
            IsOwn = callerStudentId != null && review.StudentId == callerStudentId
        };

    public static Task<ReviewDto> LoadAsync(
        IApplicationDbContext context,
        Guid reviewId,
        Guid? callerStudentId,
        CancellationToken cancellationToken) =>
        context.Reviews
            .AsNoTracking()
            .Where(review => review.Id == reviewId)
            .Select(Projection(callerStudentId))
            .FirstAsync(cancellationToken);
}
