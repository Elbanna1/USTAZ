using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Localization;
using Ustaz.Application.Common.Models;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Admin.Commands;

/// <summary>
/// Who runs USTAZ itself.
///
/// The platform <c>Admin</c> role is the highest authority in the product and, until now, it
/// could only be created by the seeder reading <c>Seed__AdminEmail</c> — there was no way to
/// add a second administrator, and no way to remove one, without a database edit.
///
/// Three rules make granting it safe, and all three are enforced here rather than in a
/// controller:
///
/// 1. **The last administrator cannot be removed.** Revoking the only remaining platform admin
///    would leave the platform with nobody able to grant the role back, recoverable only by
///    editing the database directly. The same guard covers suspending their account, which
///    locks them out just as completely.
/// 2. **An administrator cannot demote or suspend themselves.** Losing your own access is
///    almost never the intent, and it is the easiest way to arrive at rule 1 by accident.
/// 3. **Every grant and revoke is audited**, with the actor, the subject and the outcome.
///
/// Note what is deliberately absent: there is no "promote by email address" that would create
/// an account. The subject must already exist, so granting platform authority is always a
/// decision about a known person rather than an invitation into the role.
/// </summary>
public static class PlatformAdministration
{
    /// <summary>
    /// True when <paramref name="userId"/> is the only platform administrator whose account is
    /// still active. Shared by the grant/revoke commands and by account suspension, because
    /// both can leave the platform unadministered.
    /// </summary>
    public static async Task<bool> IsLastActiveAdministratorAsync(
        UserManager<ApplicationUser> users,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var administrators = await users.GetUsersInRoleAsync(RoleNames.Admin);

        var activeIds = administrators
            .Where(administrator => administrator.IsActive)
            .Select(administrator => administrator.Id)
            .ToHashSet();

        return activeIds.Count <= 1 && activeIds.Contains(userId);
    }
}

// ---------------------------------------------------------------------------
// Read
// ---------------------------------------------------------------------------

/// <summary>
/// One platform administrator. Carries only what an operator needs to recognise the person and
/// judge whether the account should still hold the role — never a credential, a token, or any
/// personal detail the role does not require.
/// </summary>
public sealed record PlatformAdministratorDto
{
    public Guid UserId { get; init; }

    public required string DisplayName { get; init; }

    public required string Email { get; init; }

    public bool IsActive { get; init; }

    public bool EmailConfirmed { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? LastLoginAt { get; init; }

    /// <summary>
    /// True when removing this administrator's authority would leave the platform with none.
    /// Surfaced so a console can disable the control rather than let the request fail.
    /// </summary>
    public bool IsLastAdministrator { get; init; }

    /// <summary>True when this row is the caller, who may not revoke their own access.</summary>
    public bool IsCurrentUser { get; init; }
}

public sealed record GetPlatformAdministratorsQuery : IRequest<IReadOnlyList<PlatformAdministratorDto>>;

public sealed class GetPlatformAdministratorsQueryHandler(
    UserManager<ApplicationUser> users,
    ICurrentUserService currentUser)
    : IRequestHandler<GetPlatformAdministratorsQuery, IReadOnlyList<PlatformAdministratorDto>>
{
    public async Task<IReadOnlyList<PlatformAdministratorDto>> Handle(
        GetPlatformAdministratorsQuery query,
        CancellationToken cancellationToken)
    {
        var administrators = await users.GetUsersInRoleAsync(RoleNames.Admin);
        var callerId = currentUser.UserId;

        var activeCount = administrators.Count(administrator => administrator.IsActive);

        return administrators
            .OrderByDescending(administrator => administrator.IsActive)
            .ThenBy(administrator => administrator.DisplayName)
            .Select(administrator => new PlatformAdministratorDto
            {
                UserId = administrator.Id,
                DisplayName = administrator.DisplayName,
                Email = administrator.Email ?? string.Empty,
                IsActive = administrator.IsActive,
                EmailConfirmed = administrator.EmailConfirmed,
                CreatedAt = administrator.CreatedAt,
                LastLoginAt = administrator.LastLoginAt,
                IsLastAdministrator = administrator.IsActive && activeCount <= 1,
                IsCurrentUser = callerId is not null && administrator.Id == callerId.Value
            })
            .ToList();
    }
}

// ---------------------------------------------------------------------------
// Grant
// ---------------------------------------------------------------------------

/// <summary>Gives an existing account platform-administrator authority.</summary>
public sealed record GrantPlatformAdminCommand : IRequest<Unit>
{
    public Guid UserId { get; init; }

    /// <summary>Why, for the audit trail. Required: a grant of this size needs a reason.</summary>
    public string? Reason { get; init; }
}

public sealed class GrantPlatformAdminCommandValidator : AbstractValidator<GrantPlatformAdminCommand>
{
    public GrantPlatformAdminCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(DomainRules.ShortTextMaxLength);
    }
}

public sealed class GrantPlatformAdminCommandHandler(
    IApplicationDbContext context,
    UserManager<ApplicationUser> users,
    IAuditLogger audit) : IRequestHandler<GrantPlatformAdminCommand, Unit>
{
    public async Task<Unit> Handle(GrantPlatformAdminCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(entity => entity.Id == command.UserId, cancellationToken)
            ?? throw new NotFoundException("User", command.UserId);

        if (!user.IsActive)
        {
            throw new BusinessRuleException(
                ErrorCodes.AccountInactive,
                "A suspended account cannot be granted platform administration. Reactivate it first.");
        }

        if (await users.IsInRoleAsync(user, RoleNames.Admin))
        {
            // Already there: nothing to do, and reporting a conflict would make a retried
            // request look like a failure when the end state is exactly what was asked for.
            return Unit.Value;
        }

        var result = await users.AddToRoleAsync(user, RoleNames.Admin);

        if (!result.Succeeded)
        {
            await audit.LogAsync(
                AuditActions.PlatformAdminGranted,
                entityName: nameof(ApplicationUser),
                entityId: user.Id,
                succeeded: false,
                failureReason: string.Join("; ", result.Errors.Select(error => error.Description)),
                cancellationToken: cancellationToken);

            throw new BusinessRuleException(
                "PLATFORM_ADMIN_GRANT_FAILED",
                "The platform administrator role could not be granted.");
        }

        await audit.LogAsync(
            AuditActions.PlatformAdminGranted,
            entityName: nameof(ApplicationUser),
            entityId: user.Id,
            newValues: new { Role = RoleNames.Admin, command.Reason },
            cancellationToken: cancellationToken);

        return Unit.Value;
    }
}

// ---------------------------------------------------------------------------
// Revoke
// ---------------------------------------------------------------------------

/// <summary>Takes platform-administrator authority away from an account.</summary>
public sealed record RevokePlatformAdminCommand : IRequest<Unit>
{
    public Guid UserId { get; init; }

    public string? Reason { get; init; }
}

public sealed class RevokePlatformAdminCommandValidator : AbstractValidator<RevokePlatformAdminCommand>
{
    public RevokePlatformAdminCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(DomainRules.ShortTextMaxLength);
    }
}

public sealed class RevokePlatformAdminCommandHandler(
    IApplicationDbContext context,
    UserManager<ApplicationUser> users,
    ICurrentUserService currentUser,
    IAuditLogger audit) : IRequestHandler<RevokePlatformAdminCommand, Unit>
{
    public async Task<Unit> Handle(RevokePlatformAdminCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(entity => entity.Id == command.UserId, cancellationToken)
            ?? throw new NotFoundException("User", command.UserId);

        if (!await users.IsInRoleAsync(user, RoleNames.Admin))
        {
            // Not an administrator: the requested end state already holds.
            return Unit.Value;
        }

        if (currentUser.UserId == user.Id)
        {
            throw new BusinessRuleException(
                "PLATFORM_ADMIN_SELF_DEMOTION",
                "You cannot remove your own platform administrator access. Ask another administrator.");
        }

        if (await PlatformAdministration.IsLastActiveAdministratorAsync(users, user.Id, cancellationToken))
        {
            throw new BusinessRuleException(
                "LAST_PLATFORM_ADMIN",
                "This is the last active platform administrator. Grant the role to someone else first.");
        }

        var result = await users.RemoveFromRoleAsync(user, RoleNames.Admin);

        if (!result.Succeeded)
        {
            await audit.LogAsync(
                AuditActions.PlatformAdminRevoked,
                entityName: nameof(ApplicationUser),
                entityId: user.Id,
                succeeded: false,
                failureReason: string.Join("; ", result.Errors.Select(error => error.Description)),
                cancellationToken: cancellationToken);

            throw new BusinessRuleException(
                "PLATFORM_ADMIN_REVOKE_FAILED",
                "The platform administrator role could not be removed.");
        }

        // Their existing access tokens still carry the Admin role claim until they expire, so
        // the outstanding sessions are closed rather than left to age out.
        var activeTokens = await context.RefreshTokens
            .Where(token => token.UserId == user.Id && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(DateTime.UtcNow, "Platform administrator access was revoked.");
        }

        await context.SaveChangesAsync(cancellationToken);

        await audit.LogAsync(
            AuditActions.PlatformAdminRevoked,
            entityName: nameof(ApplicationUser),
            entityId: user.Id,
            oldValues: new { Role = RoleNames.Admin },
            newValues: new { command.Reason },
            cancellationToken: cancellationToken);

        return Unit.Value;
    }
}
