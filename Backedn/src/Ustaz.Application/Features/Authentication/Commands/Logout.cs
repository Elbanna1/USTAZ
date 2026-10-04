using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;

namespace Ustaz.Application.Features.Authentication.Commands;

/// <summary>Revokes the presented refresh token and every sibling in its rotation family.</summary>
public sealed record LogoutCommand : IRequest<Unit>
{
    public required string RefreshToken { get; init; }
}

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator() =>
        RuleFor(command => command.RefreshToken).NotEmpty().WithMessage("A refresh token is required.");
}

public sealed class LogoutCommandHandler(
    IApplicationDbContext context,
    IJwtTokenService jwtTokenService,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<LogoutCommand, Unit>
{
    public async Task<Unit> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var tokenHash = jwtTokenService.HashRefreshToken(command.RefreshToken);

        var token = await context.RefreshTokens
            .FirstOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);

        // Logging out with an unknown token is a no-op: the client is already signed out.
        if (token is null)
        {
            return Unit.Value;
        }

        var utcNow = dateTimeProvider.UtcNow;

        var family = await context.RefreshTokens
            .Where(refreshToken => refreshToken.FamilyId == token.FamilyId && refreshToken.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var familyMember in family)
        {
            familyMember.Revoke(utcNow, "User signed out.");
        }

        await context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
