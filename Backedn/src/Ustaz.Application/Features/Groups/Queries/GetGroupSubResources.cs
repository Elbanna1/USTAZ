using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Groups.Queries;

/// <summary>Shared existence/visibility check for the group sub-resource endpoints.</summary>
internal static class GroupVisibility
{
    public static async Task<Guid> EnsureVisibleAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .AsNoTracking()
            .Where(entity => entity.Id == groupId)
            .Select(entity => new { entity.TeacherId, entity.IsPublished })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Group", groupId);

        var isOwner = currentUser.TeacherId is not null && currentUser.TeacherId == group.TeacherId;

        if (!group.IsPublished && !isOwner && !currentUser.IsAdmin)
        {
            throw new NotFoundException("Group", groupId);
        }

        return group.TeacherId;
    }
}

public sealed record GetGroupScheduleQuery(Guid GroupId) : IRequest<IReadOnlyList<GroupScheduleDto>>;

public sealed class GetGroupScheduleQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetGroupScheduleQuery, IReadOnlyList<GroupScheduleDto>>
{
    public async Task<IReadOnlyList<GroupScheduleDto>> Handle(
        GetGroupScheduleQuery query,
        CancellationToken cancellationToken)
    {
        await GroupVisibility.EnsureVisibleAsync(context, currentUser, query.GroupId, cancellationToken);

        return await context.GroupSchedules
            .AsNoTracking()
            .Where(schedule => schedule.GroupId == query.GroupId)
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
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetGroupCurriculumQuery(Guid GroupId) : IRequest<IReadOnlyList<GroupCurriculumDto>>;

public sealed class GetGroupCurriculumQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetGroupCurriculumQuery, IReadOnlyList<GroupCurriculumDto>>
{
    public async Task<IReadOnlyList<GroupCurriculumDto>> Handle(
        GetGroupCurriculumQuery query,
        CancellationToken cancellationToken)
    {
        await GroupVisibility.EnsureVisibleAsync(context, currentUser, query.GroupId, cancellationToken);

        return await context.GroupCurriculums
            .AsNoTracking()
            .Where(item => item.GroupId == query.GroupId)
            .OrderBy(item => item.SortOrder)
            .Select(item => new GroupCurriculumDto
            {
                Id = item.Id,
                GroupId = item.GroupId,
                Title = item.Title,
                Description = item.Description,
                SortOrder = item.SortOrder
            })
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetGroupMaterialsQuery(Guid GroupId) : IRequest<IReadOnlyList<GroupMaterialDto>>;

public sealed class GetGroupMaterialsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetGroupMaterialsQuery, IReadOnlyList<GroupMaterialDto>>
{
    public async Task<IReadOnlyList<GroupMaterialDto>> Handle(
        GetGroupMaterialsQuery query,
        CancellationToken cancellationToken)
    {
        var teacherId = await GroupVisibility.EnsureVisibleAsync(context, currentUser, query.GroupId, cancellationToken);

        var studentId = currentUser.StudentId;
        var isEnrolled = studentId is not null && await context.GroupEnrollments
            .AsNoTracking()
            .AnyAsync(
                enrollment => enrollment.GroupId == query.GroupId
                    && enrollment.StudentId == studentId.Value
                    && (enrollment.Status == EnrollmentStatus.Active || enrollment.Status == EnrollmentStatus.Pending),
                cancellationToken);

        var canSeeRestricted = isEnrolled || currentUser.IsAdmin || currentUser.TeacherId == teacherId;

        return await context.GroupMaterials
            .AsNoTracking()
            .Where(material => material.GroupId == query.GroupId)
            .OrderBy(material => material.SortOrder)
            .Select(material => new GroupMaterialDto
            {
                Id = material.Id,
                GroupId = material.GroupId,
                Title = material.Title,
                Description = material.Description,
                FileUrl = !material.IsEnrolledOnly || canSeeRestricted ? material.FileUrl : null,
                FileType = material.FileType,
                FileSizeInBytes = material.FileSizeInBytes,
                IsEnrolledOnly = material.IsEnrolledOnly,
                IsLocked = material.IsEnrolledOnly && !canSeeRestricted,
                SortOrder = material.SortOrder
            })
            .ToListAsync(cancellationToken);
    }
}
