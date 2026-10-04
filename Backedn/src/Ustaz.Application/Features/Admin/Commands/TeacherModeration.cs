using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Events;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Admin.Commands;

/// <summary>Admin action: approve or reject a teacher's verification request.</summary>
public sealed record VerifyTeacherCommand : IRequest<Unit>
{
    public Guid TeacherId { get; init; }

    public bool Approve { get; init; } = true;

    public string? Notes { get; init; }
}

public sealed class VerifyTeacherCommandValidator : AbstractValidator<VerifyTeacherCommand>
{
    public VerifyTeacherCommandValidator()
    {
        RuleFor(command => command.TeacherId).NotEmpty();
        RuleFor(command => command.Notes).MaximumLength(DomainRules.ShortTextMaxLength);

        RuleFor(command => command.Notes)
            .NotEmpty()
            .When(command => !command.Approve)
            .WithMessage("Provide a reason when rejecting a verification request.");
    }
}

public sealed class VerifyTeacherCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    ICacheService cache) : IRequestHandler<VerifyTeacherCommand, Unit>
{
    public async Task<Unit> Handle(VerifyTeacherCommand command, CancellationToken cancellationToken)
    {
        var teacher = await context.TeacherProfiles
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.Id == command.TeacherId, cancellationToken)
            ?? throw new NotFoundException("Teacher", command.TeacherId);

        var utcNow = dateTimeProvider.UtcNow;

        if (command.Approve)
        {
            teacher.MarkVerified(utcNow, command.Notes?.Trim());
            teacher.RaiseDomainEvent(new TeacherVerifiedEvent(teacher.Id, teacher.UserId, teacher.User.DisplayName));
        }
        else
        {
            teacher.RejectVerification(utcNow, command.Notes!.Trim());
        }

        teacher.User.IsVerified = teacher.IsVerified;
        teacher.User.UpdatedAt = utcNow;

        await context.SaveChangesAsync(cancellationToken);

        if (command.Approve)
        {
            await notifications.SendAsync(
                teacher.UserId,
                new NotificationRequest
                {
                    Event = NotificationEvents.TeacherVerified,
                    TenantId = teacher.TenantId,
                    ReferenceId = teacher.Id
                },
                cancellationToken);
        }
        else
        {
            // Routed through the catalog like every other notification, so the teacher gets
            // it in their own language instead of a hardcoded Arabic string.
            await notifications.SendAsync(
                teacher.UserId,
                new NotificationRequest
                {
                    Event = NotificationEvents.TeacherVerificationRejected,
                    TenantId = teacher.TenantId,
                    ReferenceId = teacher.Id,
                    Values = new Dictionary<string, string>
                    {
                        ["reason"] = command.Notes!.Trim()
                    }
                },
                cancellationToken);
        }

        await cache.RemoveAsync(CacheKeys.TeacherProfile(teacher.Id), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Admin action: move a pending request into review. This is what tells a waiting teacher
/// that a person is actually looking at their profile.
/// </summary>
public sealed record BeginTeacherReviewCommand(Guid TeacherId) : IRequest<Unit>;

public sealed class BeginTeacherReviewCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    ICacheService cache) : IRequestHandler<BeginTeacherReviewCommand, Unit>
{
    public async Task<Unit> Handle(BeginTeacherReviewCommand command, CancellationToken cancellationToken)
    {
        var teacher = await context.TeacherProfiles
            .FirstOrDefaultAsync(profile => profile.Id == command.TeacherId, cancellationToken)
            ?? throw new NotFoundException("Teacher", command.TeacherId);

        teacher.BeginVerificationReview(dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        await notifications.SendAsync(
            teacher.UserId,
            new NotificationRequest
            {
                Event = NotificationEvents.TeacherVerificationUnderReview,
                TenantId = teacher.TenantId,
                ReferenceId = teacher.Id
            },
            cancellationToken);

        await cache.RemoveAsync(CacheKeys.TeacherProfile(teacher.Id), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);

        return Unit.Value;
    }
}

/// <summary>Admin action: suspend or reactivate any account.</summary>
public sealed record SetUserActiveStateCommand : IRequest<Unit>
{
    public Guid UserId { get; init; }

    public bool IsActive { get; init; }

    public string? Reason { get; init; }
}

public sealed class SetUserActiveStateCommandValidator : AbstractValidator<SetUserActiveStateCommand>
{
    public SetUserActiveStateCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(DomainRules.ShortTextMaxLength);

        RuleFor(command => command.Reason)
            .NotEmpty()
            .When(command => !command.IsActive)
            .WithMessage("Provide a reason when suspending an account.");
    }
}

public sealed class SetUserActiveStateCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    UserManager<ApplicationUser> users,
    ICurrentUserService currentUser,
    ICacheService cache) : IRequestHandler<SetUserActiveStateCommand, Unit>
{
    public async Task<Unit> Handle(SetUserActiveStateCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(entity => entity.Id == command.UserId, cancellationToken)
            ?? throw new NotFoundException("User", command.UserId);

        // Suspension is as total as revoking the role: it closes every session and blocks
        // sign-in. So the two guards that protect platform administration from being lost
        // belong here too, not only on RevokePlatformAdminCommand.
        if (!command.IsActive)
        {
            if (currentUser.UserId == user.Id)
            {
                throw new BusinessRuleException(
                    "PLATFORM_ADMIN_SELF_DEMOTION",
                    "You cannot suspend your own account.");
            }

            if (await PlatformAdministration.IsLastActiveAdministratorAsync(users, user.Id, cancellationToken))
            {
                throw new BusinessRuleException(
                    "LAST_PLATFORM_ADMIN",
                    "This is the last active platform administrator. Grant the role to someone else first.");
            }
        }

        var utcNow = dateTimeProvider.UtcNow;

        user.IsActive = command.IsActive;
        user.UpdatedAt = utcNow;

        // Suspension immediately invalidates every outstanding session.
        if (!command.IsActive)
        {
            var activeTokens = await context.RefreshTokens
                .Where(token => token.UserId == user.Id && token.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.Revoke(utcNow, "Account suspended by an administrator.");
            }

            var teacher = await context.TeacherProfiles
                .FirstOrDefaultAsync(profile => profile.UserId == user.Id, cancellationToken);

            if (teacher is not null)
            {
                var groups = await context.Groups
                    .Where(group => group.TeacherId == teacher.Id && group.IsPublished)
                    .ToListAsync(cancellationToken);

                foreach (var group in groups)
                {
                    group.Unpublish(utcNow);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // Account state is a security event: Critical priority, so it is delivered even to a
        // user who has switched every other notification off.
        await notifications.SendAsync(
            user.Id,
            new NotificationRequest
            {
                Event = command.IsActive
                    ? NotificationEvents.AccountActivated
                    : NotificationEvents.AccountSuspended,
                ReferenceId = user.Id,
                Values = new Dictionary<string, string>
                {
                    ["reason"] = command.Reason ?? "غير محدد"
                }
            },
            cancellationToken);

        await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.GroupsTag, cancellationToken);

        return Unit.Value;
    }
}

/// <summary>Admin action: feature or unfeature a teacher on the home page.</summary>
public sealed record SetTeacherFeaturedCommand(Guid TeacherId, bool IsFeatured) : IRequest<Unit>;

public sealed class SetTeacherFeaturedCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache) : IRequestHandler<SetTeacherFeaturedCommand, Unit>
{
    public async Task<Unit> Handle(SetTeacherFeaturedCommand command, CancellationToken cancellationToken)
    {
        var teacher = await context.TeacherProfiles
            .FirstOrDefaultAsync(profile => profile.Id == command.TeacherId, cancellationToken)
            ?? throw new NotFoundException("Teacher", command.TeacherId);

        if (command.IsFeatured && !teacher.IsVerified)
        {
            throw new BusinessRuleException(
                "FEATURE_REQUIRES_VERIFICATION",
                "Only verified teachers can be featured.");
        }

        teacher.IsFeatured = command.IsFeatured;
        teacher.UpdatedAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);

        return Unit.Value;
    }
}
