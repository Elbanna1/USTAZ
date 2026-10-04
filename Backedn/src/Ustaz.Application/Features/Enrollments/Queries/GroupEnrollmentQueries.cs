using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Enrollments.Dtos;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Enrollments.Queries;

/// <summary>A single group enrollment, readable by its student, the owning teacher and admins.</summary>
public sealed record GetGroupEnrollmentByIdQuery(Guid EnrollmentId) : IRequest<GroupEnrollmentDto>;

public sealed class GetGroupEnrollmentByIdQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetGroupEnrollmentByIdQuery, GroupEnrollmentDto>
{
    public async Task<GroupEnrollmentDto> Handle(
        GetGroupEnrollmentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var owners = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Id == query.EnrollmentId)
            .Select(enrollment => new { enrollment.StudentId, enrollment.Group.TeacherId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("GroupEnrollment", query.EnrollmentId);

        var isStudentOwner = currentUser.StudentId is not null && currentUser.StudentId == owners.StudentId;
        var isTeacherOwner = currentUser.TeacherId is not null && currentUser.TeacherId == owners.TeacherId;

        if (!isStudentOwner && !isTeacherOwner && !currentUser.IsAdmin)
        {
            throw new ForbiddenAccessException("You can only view enrollments that belong to you.");
        }

        return await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Id == query.EnrollmentId)
            .Select(EnrollmentProjections.ToGroupDto)
            .FirstAsync(cancellationToken);
    }
}

/// <summary>The calling student's group subscriptions.</summary>
public sealed record GetStudentGroupEnrollmentsQuery : PagedRequest, IRequest<PaginatedList<GroupEnrollmentDto>>
{
    public EnrollmentStatus? Status { get; init; }
}

public sealed class GetStudentGroupEnrollmentsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetStudentGroupEnrollmentsQuery, PaginatedList<GroupEnrollmentDto>>
{
    public Task<PaginatedList<GroupEnrollmentDto>> Handle(
        GetStudentGroupEnrollmentsQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        return context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId)
            .WhereIf(query.Status.HasValue, enrollment => enrollment.Status == query.Status!.Value)
            .OrderByDescending(enrollment => enrollment.EnrolledAt)
            .Select(EnrollmentProjections.ToGroupDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>Every student enrolled in the calling teacher's groups.</summary>
/// <summary>
/// Group enrollments for one teacher, or for the whole platform when an administrator asks.
/// </summary>
public sealed record GetTeacherGroupEnrollmentsQuery : PagedRequest, IRequest<PaginatedList<GroupEnrollmentDto>>
{
    public Guid? GroupId { get; init; }

    public EnrollmentStatus? Status { get; init; }

    /// <summary>Narrows a platform-wide listing to one teacher. Ignored for a teacher caller.</summary>
    public Guid? TeacherId { get; init; }
}

public sealed class GetTeacherGroupEnrollmentsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetTeacherGroupEnrollmentsQuery, PaginatedList<GroupEnrollmentDto>>
{
    public Task<PaginatedList<GroupEnrollmentDto>> Handle(
        GetTeacherGroupEnrollmentsQuery query,
        CancellationToken cancellationToken)
    {
        var scopedTeacherId = currentUser.IsAdmin && currentUser.TeacherId is null
            ? query.TeacherId
            : currentUser.RequireTeacherId();

        var search = query.Search?.Trim();

        return context.GroupEnrollments
            .AsNoTracking()
            .WhereIf(scopedTeacherId.HasValue, enrollment => enrollment.Group.TeacherId == scopedTeacherId!.Value)
            .WhereIf(query.GroupId.HasValue, enrollment => enrollment.GroupId == query.GroupId!.Value)
            .WhereIf(query.Status.HasValue, enrollment => enrollment.Status == query.Status!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(search), enrollment =>
                enrollment.Student.User.DisplayName.Contains(search!)
                || enrollment.Group.Name.Contains(search!))
            .OrderByDescending(enrollment => enrollment.EnrolledAt)
            .Select(EnrollmentProjections.ToGroupDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}
