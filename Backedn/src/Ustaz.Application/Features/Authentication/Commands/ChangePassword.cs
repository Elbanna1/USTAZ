using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Authentication.Commands;

/// <summary>
/// Changes the signed-in user's password. Unlike the reset flow this proves possession of the
/// current password rather than of the mailbox, so it needs no token.
/// </summary>
public sealed record ChangePasswordCommand : IRequest<Unit>
{
    public required string CurrentPassword { get; init; }

    public required string NewPassword { get; init; }

    public required string ConfirmPassword { get; init; }
}

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword)
            .NotEmpty()
            .WithMessage("Your current password is required.");

        RuleFor(command => command.NewPassword).StrongPassword();

        RuleFor(command => command.ConfirmPassword)
            .Equal(command => command.NewPassword)
            .WithMessage("The confirmation password does not match the new password.");

        RuleFor(command => command.NewPassword)
            .NotEqual(command => command.CurrentPassword)
            .WithMessage("The new password must differ from the current one.");
    }
}

public sealed class ChangePasswordCommandHandler(
    IApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications) : IRequestHandler<ChangePasswordCommand, Unit>
{
    public async Task<Unit> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User", userId);

        if (!user.IsActive)
        {
            throw new ForbiddenAccessException("This account is deactivated.");
        }

        var result = await userManager.ChangePasswordAsync(
            user, command.CurrentPassword, command.NewPassword);

        if (!result.Succeeded)
        {
            // Identity reports a wrong current password through the same channel as a weak new
            // one; both are user-correctable input, so both surface as validation failures.
            throw new Common.Exceptions.ValidationException(
                nameof(command.CurrentPassword),
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        // Changing a password invalidates every outstanding session, exactly as a reset does.
        var utcNow = dateTimeProvider.UtcNow;

        var activeTokens = await context.RefreshTokens
            .Where(refreshToken => refreshToken.UserId == user.Id && refreshToken.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in activeTokens)
        {
            refreshToken.Revoke(utcNow, "Password was changed.");
        }

        user.UpdatedAt = utcNow;

        await context.SaveChangesAsync(cancellationToken);

        await notifications.SendAsync(
            user.Id,
            new NotificationRequest { Event = NotificationEvents.PasswordChanged, ReferenceId = user.Id },
            cancellationToken);

        return Unit.Value;
    }
}
