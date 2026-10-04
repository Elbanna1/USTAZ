using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Common.Security;

/// <inheritdoc />
public sealed class TenantAuthorizationService(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    ITenantContext tenantContext) : ITenantAuthorizationService
{
    public async Task<TenantAccess> RequireAccessAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var access = await TryGetAccessAsync(tenantId, cancellationToken);

        if (access is null)
        {
            throw new ForbiddenAccessException("You do not have access to this workspace.");
        }

        return access;
    }

    public async Task<TenantAccess> RequirePermissionAsync(
        string permission,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(tenantId, cancellationToken);

        if (!access.Has(permission))
        {
            throw new ForbiddenAccessException(
                "You do not have the '" + permission + "' permission in this workspace.");
        }

        return access;
    }

    public async Task<TenantAccess> RequireOwnerAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(tenantId, cancellationToken);

        if (!access.IsOwner && !access.IsPlatformAdmin)
        {
            throw new ForbiddenAccessException("Only the workspace owner can perform this action.");
        }

        return access;
    }

    public async Task<TenantAccess?> TryGetAccessAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        if (userId is null)
        {
            return null;
        }

        var effectiveTenantId = tenantId ?? tenantContext.TenantId;
        if (effectiveTenantId is null)
        {
            return null;
        }

        // A platform administrator may inspect any workspace, but the workspace must exist.
        if (currentUser.IsAdmin)
        {
            var exists = await context.Tenants
                .AsNoTracking()
                .AnyAsync(tenant => tenant.Id == effectiveTenantId.Value, cancellationToken);

            if (!exists)
            {
                throw new NotFoundException("Tenant", effectiveTenantId.Value);
            }

            return new TenantAccess
            {
                TenantId = effectiveTenantId.Value,
                UserId = userId.Value,
                Role = TenantMemberRole.Admin,
                Permissions = TenantPermissions.All,
                IsPlatformAdmin = true
            };
        }

        // Everything below is resolved from the database, never from the token.
        var membership = await context.TenantMembers
            .AsNoTracking()
            .Include(member => member.Permissions)
            .FirstOrDefaultAsync(
                member => member.TenantId == effectiveTenantId.Value && member.UserId == userId.Value,
                cancellationToken);

        if (membership is null || membership.Status != TenantMemberStatus.Active)
        {
            return null;
        }

        var tenantStatus = await context.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == effectiveTenantId.Value)
            .Select(tenant => (TenantStatus?)tenant.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantStatus is null)
        {
            throw new NotFoundException("Tenant", effectiveTenantId.Value);
        }

        if (tenantStatus == TenantStatus.Suspended)
        {
            throw new ForbiddenAccessException("This workspace is suspended. Please contact USTAZ support.");
        }

        return new TenantAccess
        {
            TenantId = effectiveTenantId.Value,
            UserId = userId.Value,
            Role = membership.Role,
            Permissions = membership.EffectivePermissions()
        };
    }

    public void EnsureSameTenant(TenantAccess access, Guid resourceTenantId)
    {
        if (access.TenantId == resourceTenantId)
        {
            return;
        }

        // A platform admin is allowed to reach across workspaces; anyone else is not, and the
        // response is a 404 so the existence of the other tenant's resource is not confirmed.
        if (access.IsPlatformAdmin)
        {
            return;
        }

        throw new NotFoundException("The requested resource does not exist in this workspace.");
    }

    public Task<TenantMember?> FindMembershipAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        context.TenantMembers
            .Include(member => member.Permissions)
            .FirstOrDefaultAsync(
                member => member.TenantId == tenantId && member.UserId == userId,
                cancellationToken);
}
