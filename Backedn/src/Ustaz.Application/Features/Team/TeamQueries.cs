using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Team.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Team;

internal static class TeamMapper
{
    public static TenantMemberDto ToDto(TenantMember member) => new()
    {
        Id = member.Id,
        TenantId = member.TenantId,
        UserId = member.UserId,
        DisplayName = member.User?.DisplayName ?? string.Empty,
        Email = member.User?.Email ?? string.Empty,
        ProfileImageUrl = member.User?.ProfileImageUrl,
        PhoneNumber = member.User?.PhoneNumber,
        Role = member.Role,
        Status = member.Status,
        Title = member.Title,
        Permissions = [.. member.EffectivePermissions().OrderBy(permission => permission, StringComparer.Ordinal)],
        ExplicitPermissions = [.. member.Permissions
            .Where(permission => permission.IsGranted)
            .Select(permission => permission.Permission)
            .OrderBy(permission => permission, StringComparer.Ordinal)],
        InvitedAt = member.InvitedAt,
        JoinedAt = member.JoinedAt,
        CreatedAt = member.CreatedAt
    };
}

/// <summary>Members of the calling workspace.</summary>
public sealed record GetTeamMembersQuery : PagedRequest, IRequest<PaginatedList<TenantMemberDto>>
{
    public TenantMemberRole? Role { get; init; }

    public TenantMemberStatus? Status { get; init; }

    /// <summary>Restricts the list to assistants, which powers the assistants screen.</summary>
    public bool AssistantsOnly { get; init; }
}

public sealed class GetTeamMembersQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetTeamMembersQuery, PaginatedList<TenantMemberDto>>
{
    public async Task<PaginatedList<TenantMemberDto>> Handle(
        GetTeamMembersQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequireAccessAsync(cancellationToken: cancellationToken);
        var search = query.Search?.Trim();

        var members = context.TenantMembers
            .AsNoTracking()
            .Include(member => member.User)
            .Include(member => member.Permissions)
            .Where(member => member.TenantId == access.TenantId)
            .WhereIf(query.AssistantsOnly, member => member.Role == TenantMemberRole.Assistant)
            .WhereIf(query.Role.HasValue, member => member.Role == query.Role!.Value)
            .WhereIf(query.Status.HasValue, member => member.Status == query.Status!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(search), member =>
                member.User.DisplayName.Contains(search!)
                || (member.User.Email != null && member.User.Email.Contains(search!)))
            .OrderBy(member => member.Role)
            .ThenBy(member => member.CreatedAt);

        var page = await members.ToPaginatedListAsync(query, cancellationToken);

        return page.Map(TeamMapper.ToDto);
    }
}

public sealed record GetTeamMemberQuery(Guid MemberId) : IRequest<TenantMemberDto>;

public sealed class GetTeamMemberQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetTeamMemberQuery, TenantMemberDto>
{
    public async Task<TenantMemberDto> Handle(GetTeamMemberQuery query, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequireAccessAsync(cancellationToken: cancellationToken);

        var member = await context.TenantMembers
            .AsNoTracking()
            .Include(entity => entity.User)
            .Include(entity => entity.Permissions)
            .FirstOrDefaultAsync(entity => entity.Id == query.MemberId, cancellationToken)
            ?? throw new NotFoundException("TenantMember", query.MemberId);

        // Reading a member of another workspace is a 404, not a 403.
        tenantAuthorization.EnsureSameTenant(access, member.TenantId);

        return TeamMapper.ToDto(member);
    }
}

/// <summary>The permission catalogue, grouped for the editor UI.</summary>
public sealed record GetPermissionCatalogQuery : IRequest<PermissionCatalogDto>;

public sealed class GetPermissionCatalogQueryHandler : IRequestHandler<GetPermissionCatalogQuery, PermissionCatalogDto>
{
    public Task<PermissionCatalogDto> Handle(GetPermissionCatalogQuery query, CancellationToken cancellationToken)
    {
        // Owner-only capabilities are deliberately excluded: they can never be delegated.
        var groups = TenantPermissions.All
            .Except(TenantPermissions.OwnerOnly)
            .GroupBy(permission => permission.Split('.')[0])
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new PermissionGroupDto
            {
                Name = group.Key,
                Permissions = [.. group.OrderBy(permission => permission, StringComparer.Ordinal)]
            })
            .ToList();

        return Task.FromResult(new PermissionCatalogDto { Groups = groups });
    }
}
