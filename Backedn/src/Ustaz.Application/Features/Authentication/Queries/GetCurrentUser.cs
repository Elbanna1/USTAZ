using MediatR;
using Microsoft.AspNetCore.Identity;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Authentication.Dtos;
using Ustaz.Application.Features.Authentication.Services;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Authentication.Queries;

/// <summary>Returns the profile of the caller identified by the bearer token.</summary>
public sealed record GetCurrentUserQuery : IRequest<CurrentUserDto>;

public sealed class GetCurrentUserQueryHandler(
    ICurrentUserService currentUser,
    UserManager<ApplicationUser> userManager,
    IAuthenticationTokenIssuer tokenIssuer) : IRequestHandler<GetCurrentUserQuery, CurrentUserDto>
{
    public async Task<CurrentUserDto> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User", userId);

        return await tokenIssuer.BuildCurrentUserAsync(user, cancellationToken);
    }
}
