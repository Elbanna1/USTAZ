using Ustaz.Application.Features.Authentication.Dtos;
using Ustaz.Domain.Entities.Identity;

namespace Ustaz.Application.Features.Authentication.Services;

/// <summary>
/// Shared token issuing pipeline used by login, register and refresh so the claim set
/// and the refresh token rotation rules stay in exactly one place.
/// </summary>
public interface IAuthenticationTokenIssuer
{
    /// <summary>Issues a fresh access token plus a new refresh token family.</summary>
    Task<AuthenticationResponse> IssueAsync(
        ApplicationUser user,
        Guid? familyId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Builds the current-user projection (roles, teacher/student ids) for a user.</summary>
    Task<CurrentUserDto> BuildCurrentUserAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}
