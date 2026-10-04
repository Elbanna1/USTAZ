using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Emails;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Team.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Team;

/// <summary>
/// Rules shared by every team mutation. These are the guard rails that stop an assistant
/// escalating their own access or taking over the workspace.
/// </summary>
internal static class TeamRules
{
    /// <summary>
    /// A member may never act on their own row: no self-promotion, no editing your own
    /// permissions, no reactivating yourself after being deactivated.
    /// </summary>
    public static void EnsureNotSelf(TenantAccess access, TenantMember member, string action)
    {
        if (member.UserId == access.UserId && !access.IsPlatformAdmin)
        {
            throw new ForbiddenAccessException("You cannot " + action + " your own workspace membership.");
        }
    }

    /// <summary>Ownership is structural: it is never granted or transferred through team endpoints.</summary>
    public static void EnsureRoleIsAssignable(TenantMemberRole role)
    {
        if (role == TenantMemberRole.Owner)
        {
            throw new BusinessRuleException(
                "OWNER_ROLE_NOT_ASSIGNABLE",
                "Ownership cannot be granted here. Use the dedicated ownership transfer flow.");
        }
    }

    /// <summary>
    /// Validates a requested permission set: every entry must be a known permission, and
    /// owner-only capabilities can never be delegated.
    /// </summary>
    public static IReadOnlyList<string> SanitisePermissions(IEnumerable<string>? permissions)
    {
        if (permissions is null)
        {
            return [];
        }

        var requested = permissions
            .Select(permission => permission.Trim())
            .Where(permission => permission.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var unknown = requested.FirstOrDefault(permission => !TenantPermissions.IsKnown(permission));
        if (unknown is not null)
        {
            throw new BusinessRuleException("UNKNOWN_PERMISSION", "'" + unknown + "' is not a valid permission.");
        }

        var forbidden = requested.FirstOrDefault(TenantPermissions.OwnerOnly.Contains);
        if (forbidden is not null)
        {
            throw new BusinessRuleException(
                "PERMISSION_NOT_DELEGATABLE",
                "'" + forbidden + "' is reserved for the workspace owner and cannot be granted.");
        }

        return requested;
    }

    public static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}

// ---------------------------------------------------------------------------
// Invite
// ---------------------------------------------------------------------------
public sealed record InviteTeamMemberCommand : IRequest<TenantInvitationDto>
{
    public required string Email { get; init; }

    public TenantMemberRole Role { get; init; } = TenantMemberRole.Assistant;

    public string? Title { get; init; }

    public IReadOnlyList<string> Permissions { get; init; } = [];
}

public sealed class InviteTeamMemberCommandValidator : AbstractValidator<InviteTeamMemberCommand>
{
    public InviteTeamMemberCommandValidator()
    {
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Role).IsInEnum();
        RuleFor(command => command.Title).MaximumLength(150);

        RuleFor(command => command.Role)
            .NotEqual(TenantMemberRole.Owner)
            .WithMessage("Ownership cannot be granted through an invitation.");
    }
}

public sealed class InviteTeamMemberCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    ITransactionalEmailService emails,
    IAuditLogger auditLogger) : IRequestHandler<InviteTeamMemberCommand, TenantInvitationDto>
{
    private const int InvitationValidDays = 14;

    public async Task<TenantInvitationDto> Handle(
        InviteTeamMemberCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.TeamManage, cancellationToken: cancellationToken);

        TeamRules.EnsureRoleIsAssignable(command.Role);
        var permissions = TeamRules.SanitisePermissions(command.Permissions);

        // Both quotas apply: a seat is a seat, and assistants have their own cap.
        await planLimits.EnsureQuotaAsync(access.TenantId, PlanQuota.TeamMembers, 1, cancellationToken);

        if (command.Role == TenantMemberRole.Assistant)
        {
            await planLimits.EnsureQuotaAsync(access.TenantId, PlanQuota.Assistants, 1, cancellationToken);
        }

        var email = command.Email.Trim().ToLowerInvariant();
        var utcNow = dateTimeProvider.UtcNow;

        var alreadyMember = await context.TenantMembers
            .AnyAsync(
                member => member.TenantId == access.TenantId
                    && member.User.Email == email
                    && member.Status != TenantMemberStatus.Removed,
                cancellationToken);

        if (alreadyMember)
        {
            throw new ConflictException("This person is already part of your workspace.");
        }

        var pending = await context.TenantInvitations
            .AnyAsync(
                invitation => invitation.TenantId == access.TenantId
                    && invitation.Email == email
                    && invitation.AcceptedAt == null
                    && invitation.DeclinedAt == null
                    && invitation.RevokedAt == null
                    && invitation.ExpiresAt > utcNow,
                cancellationToken);

        if (pending)
        {
            throw new ConflictException("An invitation is already pending for this email address.");
        }

        var token = TeamRules.NewToken();

        var entity = new TenantInvitation
        {
            Id = Guid.NewGuid(),
            TenantId = access.TenantId,
            Email = email,
            Role = command.Role,
            Title = command.Title?.Trim(),
            TokenHash = TeamRules.HashToken(token),
            PermissionsCsv = permissions.Count == 0 ? null : string.Join(',', permissions),
            ExpiresAt = utcNow.AddDays(InvitationValidDays),
            InvitedByUserId = access.UserId,
            CreatedAt = utcNow
        };

        await context.TenantInvitations.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var tenantName = await context.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == access.TenantId)
            .Select(tenant => tenant.Name)
            .FirstAsync(cancellationToken);

        await emails.SendToAddressAsync(
            email,
            displayName: null,
            new TransactionalEmailRequest
            {
                Event = EmailEvents.TeamMemberInvited,
                TenantId = access.TenantId,
                RelatedEntityId = entity.Id,
                Values = new Dictionary<string, string>
                {
                    ["userName"] = email,
                    ["workspace"] = tenantName,
                    ["role"] = command.Role.ToString()
                }
            },
            cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.MemberInvited,
            nameof(TenantInvitation),
            entity.Id,
            newValues: new { entity.Email, entity.Role, Permissions = permissions },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return new TenantInvitationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TenantName = tenantName,
            Email = entity.Email,
            Role = entity.Role,
            Title = entity.Title,
            Permissions = permissions,
            ExpiresAt = entity.ExpiresAt,
            CreatedAt = entity.CreatedAt,
            Token = token
        };
    }
}

// ---------------------------------------------------------------------------
// Accept / decline
// ---------------------------------------------------------------------------
public sealed record AcceptInvitationCommand(string Token) : IRequest<TenantMemberDto>;

public sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator() =>
        RuleFor(command => command.Token).NotEmpty().WithMessage("An invitation token is required.");
}

public sealed class AcceptInvitationCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger) : IRequestHandler<AcceptInvitationCommand, TenantMemberDto>
{
    public async Task<TenantMemberDto> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var utcNow = dateTimeProvider.UtcNow;
        var tokenHash = TeamRules.HashToken(command.Token);

        var invitation = await context.TenantInvitations
            .FirstOrDefaultAsync(entity => entity.TokenHash == tokenHash, cancellationToken)
            ?? throw new NotFoundException("This invitation link is not valid.");

        invitation.EnsureRedeemable(utcNow);

        // The invitation is bound to an email address, so it cannot be redeemed by
        // whoever happens to hold the token.
        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException(
                "This invitation was sent to a different email address. Sign in with that account to accept it.");
        }

        var existing = await context.TenantMembers
            .Include(member => member.Permissions)
            .FirstOrDefaultAsync(
                member => member.TenantId == invitation.TenantId && member.UserId == userId,
                cancellationToken);

        var member = existing ?? new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = invitation.TenantId,
            UserId = userId,
            CreatedAt = utcNow
        };

        // An existing owner is never demoted by accepting an invitation.
        if (!member.IsOwner)
        {
            member.Role = invitation.Role;
            member.Title = invitation.Title;
        }

        member.InvitedByUserId = invitation.InvitedByUserId;
        member.InvitedAt = invitation.CreatedAt;
        member.Accept(utcNow);

        if (existing is null)
        {
            await context.TenantMembers.AddAsync(member, cancellationToken);
        }
        else
        {
            context.TenantMemberPermissions.RemoveRange(existing.Permissions);
        }

        foreach (var permission in invitation.Permissions())
        {
            if (!TenantPermissions.IsKnown(permission) || TenantPermissions.OwnerOnly.Contains(permission))
            {
                continue;
            }

            member.Permissions.Add(new TenantMemberPermission
            {
                Id = Guid.NewGuid(),
                TenantMemberId = member.Id,
                Permission = permission,
                IsGranted = true,
                GrantedByUserId = invitation.InvitedByUserId,
                CreatedAt = utcNow
            });
        }

        invitation.AcceptedAt = utcNow;
        invitation.UpdatedAt = utcNow;

        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.MemberStatusChanged,
            nameof(TenantMember),
            member.Id,
            newValues: new { Accepted = true, member.Role },
            tenantId: invitation.TenantId,
            cancellationToken: cancellationToken);

        var saved = await context.TenantMembers
            .AsNoTracking()
            .Include(entity => entity.User)
            .Include(entity => entity.Permissions)
            .FirstAsync(entity => entity.Id == member.Id, cancellationToken);

        return TeamMapper.ToDto(saved);
    }
}

public sealed record DeclineInvitationCommand(string Token) : IRequest<Unit>;

public sealed class DeclineInvitationCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<DeclineInvitationCommand, Unit>
{
    public async Task<Unit> Handle(DeclineInvitationCommand command, CancellationToken cancellationToken)
    {
        var tokenHash = TeamRules.HashToken(command.Token);

        var invitation = await context.TenantInvitations
            .FirstOrDefaultAsync(entity => entity.TokenHash == tokenHash, cancellationToken)
            ?? throw new NotFoundException("This invitation link is not valid.");

        invitation.EnsureRedeemable(dateTimeProvider.UtcNow);

        invitation.DeclinedAt = dateTimeProvider.UtcNow;
        invitation.UpdatedAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
