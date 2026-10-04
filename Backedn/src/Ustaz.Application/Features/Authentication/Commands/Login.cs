using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Ustaz.Application.Common.Localization;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Authentication.Dtos;
using Ustaz.Application.Features.Authentication.Services;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Authentication.Commands;

public sealed record LoginCommand : IRequest<AuthenticationResponse>
{
    public required string Email { get; init; }

    public required string Password { get; init; }
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Password).NotEmpty().WithMessage("Password is required.");
    }
}

public sealed class LoginCommandHandler(
    UserManager<ApplicationUser> userManager,
    IAuthenticationTokenIssuer tokenIssuer,
    ILogger<LoginCommandHandler> logger) : IRequestHandler<LoginCommand, AuthenticationResponse>
{
    public async Task<AuthenticationResponse> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        // The same message is returned for unknown emails and wrong passwords so the
        // endpoint cannot be used to enumerate accounts.
        if (user is null)
        {
            throw new UnauthorizedException(ErrorCodes.InvalidCredentials, "Invalid email or password.");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException(ErrorCodes.AccountLocked, "This account is temporarily locked. Please try again later.");
        }

        if (!await userManager.CheckPasswordAsync(user, command.Password))
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("Failed login attempt for user {UserId}.", user.Id);
            throw new UnauthorizedException(ErrorCodes.InvalidCredentials, "Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenAccessException("This account has been suspended. Please contact USTAZ support.");
        }

        await userManager.ResetAccessFailedCountAsync(user);

        logger.LogInformation("User {UserId} signed in successfully.", user.Id);

        return await tokenIssuer.IssueAsync(user, cancellationToken: cancellationToken);
    }
}
