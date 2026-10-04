using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Localization;
using Ustaz.Application.Features.Authentication.Dtos;
using Ustaz.Application.Features.Authentication.Services;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Authentication.Commands;

public sealed record RefreshTokenCommand : IRequest<AuthenticationResponse>
{
    public required string RefreshToken { get; init; }
}

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() =>
        RuleFor(command => command.RefreshToken).NotEmpty().WithMessage("A refresh token is required.");
}

/// <summary>
/// Implements refresh token rotation with reuse detection: presenting a token that was
/// already rotated revokes the whole family, because it means the token leaked.
/// </summary>
public sealed class RefreshTokenCommandHandler(
    IApplicationDbContext context,
    IJwtTokenService jwtTokenService,
    IAuthenticationTokenIssuer tokenIssuer,
    IDateTimeProvider dateTimeProvider,
    ILogger<RefreshTokenCommandHandler> logger) : IRequestHandler<RefreshTokenCommand, AuthenticationResponse>
{
    public async Task<AuthenticationResponse> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var tokenHash = jwtTokenService.HashRefreshToken(command.RefreshToken);
        var utcNow = dateTimeProvider.UtcNow;

        var stored = await context.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (stored is null)
        {
            throw new UnauthorizedException(ErrorCodes.RefreshTokenInvalid, "The refresh token is invalid or has already been used.");
        }

        if (stored.IsRevoked)
        {
            await RevokeFamilyAsync(stored.FamilyId, utcNow, "Refresh token reuse detected.", cancellationToken);

            logger.LogWarning(
                "Refresh token reuse detected for user {UserId}; revoked token family {FamilyId}.",
                stored.UserId,
                stored.FamilyId);

            throw new UnauthorizedException(ErrorCodes.RefreshTokenInvalid, "The refresh token is invalid or has already been used.");
        }

        if (stored.IsExpired(utcNow))
        {
            throw new UnauthorizedException(ErrorCodes.TokenExpired, "The refresh token has expired. Please sign in again.");
        }

        if (!stored.User.IsActive)
        {
            throw new ForbiddenAccessException("This account has been suspended. Please contact USTAZ support.");
        }

        var response = await tokenIssuer.IssueAsync(stored.User, stored.FamilyId, cancellationToken);

        stored.Revoke(utcNow, "Rotated.", jwtTokenService.HashRefreshToken(response.RefreshToken));
        await context.SaveChangesAsync(cancellationToken);

        return response;
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTime utcNow, string reason, CancellationToken cancellationToken)
    {
        var family = await context.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in family)
        {
            token.Revoke(utcNow, reason);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
