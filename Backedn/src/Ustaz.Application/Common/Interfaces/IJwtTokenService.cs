using Ustaz.Application.Common.Models;
using Ustaz.Domain.Entities.Identity;

namespace Ustaz.Application.Common.Interfaces;

/// <summary>Issues access tokens and opaque refresh tokens.</summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Builds a signed JWT carrying user id, email, roles, profile ids and the active
    /// workspace. Workspace claims are a convenience for the client; authorization is always
    /// re-resolved server-side from the database.
    /// </summary>
    AccessTokenResult CreateAccessToken(
        ApplicationUser user,
        IEnumerable<string> roles,
        Guid? teacherId,
        Guid? studentId,
        Guid? parentId = null,
        Guid? tenantId = null,
        string? tenantSlug = null,
        string? tenantRole = null);

    /// <summary>Creates a cryptographically random refresh token together with its storage hash.</summary>
    RefreshTokenResult CreateRefreshToken();

    /// <summary>Hashes a client supplied refresh token so it can be matched against storage.</summary>
    string HashRefreshToken(string token);

    TimeSpan RefreshTokenLifetime { get; }
}
