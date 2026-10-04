namespace Ustaz.Application.Common.Models;

/// <summary>A signed JWT together with its expiry, ready to be handed to the client.</summary>
public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc, int ExpiresInSeconds);

/// <summary>
/// The opaque refresh token given to the client plus the hash that is safe to persist.
/// The raw token never touches the database.
/// </summary>
public sealed record RefreshTokenResult(string Token, string TokenHash, DateTime ExpiresAtUtc);
