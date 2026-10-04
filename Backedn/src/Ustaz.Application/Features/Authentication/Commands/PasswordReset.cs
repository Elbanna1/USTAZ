using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ustaz.Application.Common.Emails;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Authentication.Commands;

public sealed record ForgotPasswordCommand : IRequest<Unit>
{
    public required string Email { get; init; }
}

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() => RuleFor(command => command.Email).ValidEmail();
}

public sealed class ForgotPasswordCommandHandler(
    UserManager<ApplicationUser> userManager,
    ITransactionalEmailService emails,
    IConfiguration configuration) : IRequestHandler<ForgotPasswordCommand, Unit>
{
    public async Task<Unit> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        // Always succeed so the endpoint cannot reveal which emails are registered.
        if (user is null || !user.IsActive)
        {
            return Unit.Value;
        }

        var rawToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = VerifyEmailCommandHandler.EncodeToken(rawToken);

        var clientBaseUrl = configuration["Email:ClientBaseUrl"] ?? "http://localhost:5173";
        var link = clientBaseUrl.TrimEnd('/') + "/reset-password?email=" + Uri.EscapeDataString(email) + "&token=" + encoded;

        // Sent to the address rather than "to the user": the endpoint must behave
        // identically whether or not the account exists, and the link is one-time.
        await emails.SendToAddressAsync(
            email,
            user.DisplayName,
            new TransactionalEmailRequest
            {
                Event = EmailEvents.PasswordResetRequested,
                Language = TransactionalEmailLanguage.For(user.PreferredLanguage),
                Values = new Dictionary<string, string>
                {
                    ["userName"] = user.DisplayName,
                    ["resetUrl"] = link
                }
            },
            cancellationToken);

        return Unit.Value;
    }
}

public sealed record ResetPasswordCommand : IRequest<Unit>
{
    public required string Email { get; init; }

    public required string Token { get; init; }

    public required string NewPassword { get; init; }

    public required string ConfirmPassword { get; init; }
}

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Token).NotEmpty().WithMessage("A reset token is required.");
        RuleFor(command => command.NewPassword).StrongPassword();

        RuleFor(command => command.ConfirmPassword)
            .Equal(command => command.NewPassword)
            .WithMessage("The confirmation password does not match the new password.");
    }
}

public sealed class ResetPasswordCommandHandler(
    IApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications) : IRequestHandler<ResetPasswordCommand, Unit>
{
    public async Task<Unit> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(command.Email.Trim().ToLowerInvariant())
            ?? throw new BusinessRuleException(
                "PASSWORD_RESET_FAILED",
                "The reset link is invalid or has expired. Please request a new one.");

        var token = VerifyEmailCommandHandler.DecodeToken(command.Token);
        var result = await userManager.ResetPasswordAsync(user, token, command.NewPassword);

        if (!result.Succeeded)
        {
            throw new Common.Exceptions.ValidationException(
                nameof(command.NewPassword),
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        // Changing a password invalidates every outstanding session.
        var utcNow = dateTimeProvider.UtcNow;
        var activeTokens = await context.RefreshTokens
            .Where(refreshToken => refreshToken.UserId == user.Id && refreshToken.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in activeTokens)
        {
            refreshToken.Revoke(utcNow, "Password was reset.");
        }

        await context.SaveChangesAsync(cancellationToken);

        await notifications.SendAsync(
            user.Id,
            new NotificationRequest { Event = NotificationEvents.PasswordReset, ReferenceId = user.Id },
            cancellationToken);

        return Unit.Value;
    }
}
