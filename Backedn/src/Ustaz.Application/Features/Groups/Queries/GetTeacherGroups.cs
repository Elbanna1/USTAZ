using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Groups.Queries;

/// <summary>The calling teacher's own groups, including drafts and archived ones.</summary>
public sealed record GetTeacherGroupsQuery : PagedRequest, IRequest<PaginatedList<GroupListItemDto>>
{
    public bool? IsPublished { get; init; }

    public bool? IsActive { get; init; }

    public Guid? SubjectId { get; init; }

    public Guid? GradeId { get; init; }
}

public sealed class GetTeacherGroupsQueryValidator : AbstractValidator<GetTeacherGroupsQuery>
{
    public GetTeacherGroupsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, DomainRules.MaxPageSize);
        RuleFor(query => query.Search).MaximumLength(200);
    }
}

public sealed class GetTeacherGroupsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetTeacherGroupsQuery, PaginatedList<GroupListItemDto>>
{
    public Task<PaginatedList<GroupListItemDto>> Handle(
        GetTeacherGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();
        var search = query.Search?.Trim();

        var groups = context.Groups
            .AsNoTracking()
            .Where(group => group.TeacherId == teacherId)
            .WhereIf(query.IsPublished.HasValue, group => group.IsPublished == query.IsPublished!.Value)
            .WhereIf(query.IsActive.HasValue, group => group.IsActive == query.IsActive!.Value)
            .WhereIf(query.SubjectId.HasValue, group => group.SubjectId == query.SubjectId!.Value)
            .WhereIf(query.GradeId.HasValue, group => group.GradeId == query.GradeId!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(search), group => group.Name.Contains(search!));

        groups = query.Sort?.ToLowerInvariant() switch
        {
            "name" => groups.OrderBy(group => group.Name),
            "students" => groups.OrderByDescending(group => group.EnrolledStudentsCount),
            "price_asc" => groups.OrderBy(group => group.MonthlyPrice),
            "price_desc" => groups.OrderByDescending(group => group.MonthlyPrice),
            "oldest" => groups.OrderBy(group => group.CreatedAt),
            _ => groups.OrderByDescending(group => group.CreatedAt)
        };

        return groups
            .Select(GroupProjections.ToListItem)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>Public listing of the groups a given teacher currently offers.</summary>
public sealed record GetGroupsByTeacherQuery : PagedRequest, IRequest<PaginatedList<GroupListItemDto>>
{
    public Guid TeacherId { get; init; }
}

public sealed class GetGroupsByTeacherQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetGroupsByTeacherQuery, PaginatedList<GroupListItemDto>>
{
    public Task<PaginatedList<GroupListItemDto>> Handle(
        GetGroupsByTeacherQuery query,
        CancellationToken cancellationToken) =>
        context.Groups
            .AsNoTracking()
            .Where(group => group.TeacherId == query.TeacherId && group.IsPublished && group.IsActive)
            .OrderByDescending(group => group.Type == GroupType.Regular)
            .ThenByDescending(group => group.CreatedAt)
            .Select(GroupProjections.ToListItem)
            .ToPaginatedListAsync(query, cancellationToken);
}
