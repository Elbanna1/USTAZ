using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Exceptions;
using Ustaz.Application.Common.Emails;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;

namespace Ustaz.Application.Features.Authentication.Commands;

public sealed record VerifyEmailCommand : IRequest<Unit>
{
    public required string Email { get; init; }

    /// <summary>Base64-url encoded token produced by ASP.NET Core Identity.</summary>
    public required string Token { get; init; }
}

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Token).NotEmpty().WithMessage("A verification token is required.");
    }
}

public sealed class VerifyEmailCommandHandler(
    UserManager<ApplicationUser> userManager,
    INotificationDispatcher notifications)
    : IRequestHandler<VerifyEmailCommand, Unit>
{
    public async Task<Unit> Handle(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(command.Email.Trim().ToLowerInvariant())
            ?? throw new NotFoundException("The verification link is invalid or has expired.");

        if (user.EmailConfirmed)
        {
            return Unit.Value;
        }

        var token = DecodeToken(command.Token);
        var result = await userManager.ConfirmEmailAsync(user, token);

        if (!result.Succeeded)
        {
            throw new BusinessRuleException(
                "EMAIL_VERIFICATION_FAILED",
                "The verification link is invalid or has expired. Please request a new one.");
        }

        await notifications.SendAsync(
            user.Id,
            new NotificationRequest
            {
                Event = NotificationEvents.EmailVerified,
                ReferenceId = user.Id,
                DeduplicationKey = "email-verified:" + user.Id
            },
            cancellationToken);

        return Unit.Value;
    }

    internal static string DecodeToken(string token)
    {
        try
        {
            var padded = token.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        }
        catch (FormatException)
        {
            throw new BusinessRuleException("INVALID_TOKEN", "The supplied token is malformed.");
        }
    }

    internal static string EncodeToken(string token) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(token))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}

public sealed record ResendVerificationCommand : IRequest<Unit>
{
    public required string Email { get; init; }
}

public sealed class ResendVerificationCommandValidator : AbstractValidator<ResendVerificationCommand>
{
    public ResendVerificationCommandValidator() => RuleFor(command => command.Email).ValidEmail();
}

public sealed class ResendVerificationCommandHandler(
    UserManager<ApplicationUser> userManager,
    ITransactionalEmailService emails,
    IConfiguration configuration) : IRequestHandler<ResendVerificationCommand, Unit>
{
    public async Task<Unit> Handle(ResendVerificationCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        // Always report success so the endpoint cannot be used to enumerate accounts.
        if (user is null || user.EmailConfirmed)
        {
            return Unit.Value;
        }

        var rawToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encoded = VerifyEmailCommandHandler.EncodeToken(rawToken);

        var clientBaseUrl = configuration["Email:ClientBaseUrl"] ?? "http://localhost:5173";
        var link = clientBaseUrl.TrimEnd('/') + "/verify-email?email=" + Uri.EscapeDataString(email) + "&token=" + encoded;

        // The link carries a one-time token, so it is sent straight to the address and
        // never stored for a retry.
        await emails.SendToUserAsync(
            user.Id,
            new TransactionalEmailRequest
            {
                Event = EmailEvents.EmailVerification,
                Values = new Dictionary<string, string>
                {
                    ["userName"] = user.DisplayName,
                    ["verificationUrl"] = link
                }
            },
            cancellationToken);

        return Unit.Value;
    }
}
