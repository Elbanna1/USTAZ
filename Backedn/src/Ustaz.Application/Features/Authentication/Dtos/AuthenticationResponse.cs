namespace Ustaz.Application.Features.Authentication.Dtos;

/// <summary>Everything the React client needs after a successful login/register/refresh.</summary>
public sealed record AuthenticationResponse
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required DateTime AccessTokenExpiresAt { get; init; }

    public int ExpiresInSeconds { get; init; }

    public string TokenType { get; init; } = "Bearer";

    public required CurrentUserDto User { get; init; }
}
