using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Enrollments.Dtos;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Enrollments.Queries;

public sealed record GetCourseEnrollmentByIdQuery(Guid EnrollmentId) : IRequest<CourseEnrollmentDto>;

public sealed class GetCourseEnrollmentByIdQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetCourseEnrollmentByIdQuery, CourseEnrollmentDto>
{
    public async Task<CourseEnrollmentDto> Handle(
        GetCourseEnrollmentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var owners = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Id == query.EnrollmentId)
            .Select(enrollment => new { enrollment.StudentId, enrollment.Course.TeacherId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("CourseEnrollment", query.EnrollmentId);

        var isStudentOwner = currentUser.StudentId is not null && currentUser.StudentId == owners.StudentId;
        var isTeacherOwner = currentUser.TeacherId is not null && currentUser.TeacherId == owners.TeacherId;

        if (!isStudentOwner && !isTeacherOwner && !currentUser.IsAdmin)
        {
            throw new ForbiddenAccessException("You can only view enrollments that belong to you.");
        }

        return await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Id == query.EnrollmentId)
            .Select(EnrollmentProjections.ToCourseDto)
            .FirstAsync(cancellationToken);
    }
}

public sealed record GetStudentCourseEnrollmentsQuery : PagedRequest, IRequest<PaginatedList<CourseEnrollmentDto>>
{
    public EnrollmentStatus? Status { get; init; }
}

public sealed class GetStudentCourseEnrollmentsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<GetStudentCourseEnrollmentsQuery, PaginatedList<CourseEnrollmentDto>>
{
    public Task<PaginatedList<CourseEnrollmentDto>> Handle(
        GetStudentCourseEnrollmentsQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        return context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId)
            .WhereIf(query.Status.HasValue, enrollment => enrollment.Status == query.Status!.Value)
            .OrderByDescending(enrollment => enrollment.EnrolledAt)
            .Select(EnrollmentProjections.ToCourseDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

public sealed record GetTeacherCourseEnrollmentsQuery : PagedRequest, IRequest<PaginatedList<CourseEnrollmentDto>>
{
    public Guid? CourseId { get; init; }

    public EnrollmentStatus? Status { get; init; }
}

public sealed class GetTeacherCourseEnrollmentsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<GetTeacherCourseEnrollmentsQuery, PaginatedList<CourseEnrollmentDto>>
{
    public Task<PaginatedList<CourseEnrollmentDto>> Handle(
        GetTeacherCourseEnrollmentsQuery query,
        CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();
        var search = query.Search?.Trim();

        return context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Course.TeacherId == teacherId)
            .WhereIf(query.CourseId.HasValue, enrollment => enrollment.CourseId == query.CourseId!.Value)
            .WhereIf(query.Status.HasValue, enrollment => enrollment.Status == query.Status!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(search), enrollment =>
                enrollment.Student.User.DisplayName.Contains(search!)
                || enrollment.Course.Title.Contains(search!))
            .OrderByDescending(enrollment => enrollment.EnrolledAt)
            .Select(EnrollmentProjections.ToCourseDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}
