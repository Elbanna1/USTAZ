using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Team.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Team;

// ---------------------------------------------------------------------------
// Change a member's role
// ---------------------------------------------------------------------------
public sealed record ChangeMemberRoleCommand : IRequest<TenantMemberDto>
{
    public Guid MemberId { get; init; }

    public TenantMemberRole Role { get; init; }

    public string? Title { get; init; }
}

public sealed class ChangeMemberRoleCommandValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    public ChangeMemberRoleCommandValidator()
    {
        RuleFor(command => command.MemberId).NotEmpty();
        RuleFor(command => command.Role).IsInEnum();
        RuleFor(command => command.Title).MaximumLength(150);

        RuleFor(command => command.Role)
            .NotEqual(TenantMemberRole.Owner)
            .WithMessage("Ownership cannot be granted by changing a role.");
    }
}

public sealed class ChangeMemberRoleCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger) : IRequestHandler<ChangeMemberRoleCommand, TenantMemberDto>
{
    public async Task<TenantMemberDto> Handle(ChangeMemberRoleCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.TeamManage, cancellationToken: cancellationToken);

        TeamRules.EnsureRoleIsAssignable(command.Role);

        var member = await LoadMemberAsync(context, tenantAuthorization, access, command.MemberId, cancellationToken);

        // No self-promotion, and the owner row is immutable.
        TeamRules.EnsureNotSelf(access, member, "change the role of");
        member.EnsureNotOwner("re-roled");

        var previousRole = member.Role;

        // Moving *into* the assistant role consumes an assistant seat.
        if (command.Role == TenantMemberRole.Assistant && previousRole != TenantMemberRole.Assistant)
        {
            await planLimits.EnsureQuotaAsync(access.TenantId, PlanQuota.Assistants, 1, cancellationToken);
        }

        member.Role = command.Role;

        if (command.Title is not null)
        {
            member.Title = command.Title.Trim();
        }

        member.UpdatedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.MemberRoleChanged,
            nameof(TenantMember),
            member.Id,
            new { Role = previousRole },
            new { member.Role },
            access.TenantId,
            cancellationToken: cancellationToken);

        return await ReloadAsync(context, member.Id, cancellationToken);
    }

    internal static async Task<TenantMember> LoadMemberAsync(
        IApplicationDbContext context,
        ITenantAuthorizationService tenantAuthorization,
        TenantAccess access,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var member = await context.TenantMembers
            .Include(entity => entity.Permissions)
            .FirstOrDefaultAsync(entity => entity.Id == memberId, cancellationToken)
            ?? throw new NotFoundException("TenantMember", memberId);

        tenantAuthorization.EnsureSameTenant(access, member.TenantId);

        return member;
    }

    internal static async Task<TenantMemberDto> ReloadAsync(
        IApplicationDbContext context,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var saved = await context.TenantMembers
            .AsNoTracking()
            .Include(entity => entity.User)
            .Include(entity => entity.Permissions)
            .FirstAsync(entity => entity.Id == memberId, cancellationToken);

        return TeamMapper.ToDto(saved);
    }
}

// ---------------------------------------------------------------------------
// Replace a member's permissions
// ---------------------------------------------------------------------------
public sealed record UpdateMemberPermissionsCommand : IRequest<TenantMemberDto>
{
    public Guid MemberId { get; init; }

    public IReadOnlyList<string> Permissions { get; init; } = [];
}

public sealed class UpdateMemberPermissionsCommandValidator : AbstractValidator<UpdateMemberPermissionsCommand>
{
    public UpdateMemberPermissionsCommandValidator()
    {
        RuleFor(command => command.MemberId).NotEmpty();

        RuleFor(command => command.Permissions)
            .Must(permissions => permissions.Count <= TenantPermissions.All.Count)
            .WithMessage("Too many permissions were supplied.");
    }
}

public sealed class UpdateMemberPermissionsCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger) : IRequestHandler<UpdateMemberPermissionsCommand, TenantMemberDto>
{
    public async Task<TenantMemberDto> Handle(
        UpdateMemberPermissionsCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.TeamManage, cancellationToken: cancellationToken);

        // Unknown and owner-only permissions are rejected before anything is written.
        var permissions = TeamRules.SanitisePermissions(command.Permissions);

        var member = await ChangeMemberRoleCommandHandler.LoadMemberAsync(
            context, tenantAuthorization, access, command.MemberId, cancellationToken);

        // The critical rule: nobody edits their own permissions, not even a workspace admin.
        TeamRules.EnsureNotSelf(access, member, "change the permissions of");
        member.EnsureNotOwner("granted explicit permissions");

        var before = member.Permissions
            .Where(permission => permission.IsGranted)
            .Select(permission => permission.Permission)
            .ToList();

        context.TenantMemberPermissions.RemoveRange(member.Permissions);
        member.Permissions.Clear();

        var utcNow = dateTimeProvider.UtcNow;

        foreach (var permission in permissions)
        {
            member.Permissions.Add(new TenantMemberPermission
            {
                Id = Guid.NewGuid(),
                TenantMemberId = member.Id,
                Permission = permission,
                IsGranted = true,
                GrantedByUserId = access.UserId,
                CreatedAt = utcNow
            });
        }

        member.UpdatedAt = utcNow;
        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.PermissionsChanged,
            nameof(TenantMember),
            member.Id,
            new { Permissions = before },
            new { Permissions = permissions },
            access.TenantId,
            cancellationToken: cancellationToken);

        return await ChangeMemberRoleCommandHandler.ReloadAsync(context, member.Id, cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Activate / deactivate
// ---------------------------------------------------------------------------
public sealed record ChangeMemberStatusCommand : IRequest<TenantMemberDto>
{
    public Guid MemberId { get; init; }

    public bool IsActive { get; init; }
}

public sealed class ChangeMemberStatusCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger) : IRequestHandler<ChangeMemberStatusCommand, TenantMemberDto>
{
    public async Task<TenantMemberDto> Handle(ChangeMemberStatusCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.TeamManage, cancellationToken: cancellationToken);

        var member = await ChangeMemberRoleCommandHandler.LoadMemberAsync(
            context, tenantAuthorization, access, command.MemberId, cancellationToken);

        TeamRules.EnsureNotSelf(access, member, "change the status of");

        var utcNow = dateTimeProvider.UtcNow;
        var previous = member.Status;

        if (command.IsActive)
        {
            member.Status = TenantMemberStatus.Active;
            member.JoinedAt ??= utcNow;
            member.DeactivatedAt = null;
            member.UpdatedAt = utcNow;
        }
        else
        {
            member.Deactivate(utcNow);
        }

        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.MemberStatusChanged,
            nameof(TenantMember),
            member.Id,
            new { Status = previous },
            new { member.Status },
            access.TenantId,
            cancellationToken: cancellationToken);

        return await ChangeMemberRoleCommandHandler.ReloadAsync(context, member.Id, cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Remove
// ---------------------------------------------------------------------------
public sealed record RemoveTeamMemberCommand(Guid MemberId) : IRequest<Unit>;

public sealed class RemoveTeamMemberCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger) : IRequestHandler<RemoveTeamMemberCommand, Unit>
{
    public async Task<Unit> Handle(RemoveTeamMemberCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.TeamManage, cancellationToken: cancellationToken);

        var member = await ChangeMemberRoleCommandHandler.LoadMemberAsync(
            context, tenantAuthorization, access, command.MemberId, cancellationToken);

        TeamRules.EnsureNotSelf(access, member, "remove");
        member.EnsureNotOwner("removed");

        // Soft removal keeps the audit trail intact while revoking access immediately.
        member.Status = TenantMemberStatus.Removed;
        member.DeactivatedAt = dateTimeProvider.UtcNow;
        member.UpdatedAt = dateTimeProvider.UtcNow;

        context.TenantMemberPermissions.RemoveRange(member.Permissions);

        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.MemberRemoved,
            nameof(TenantMember),
            member.Id,
            oldValues: new { member.UserId, member.Role },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return Unit.Value;
    }
}
