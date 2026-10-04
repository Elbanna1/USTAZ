using Ustaz.Application.Features.Admin.Commands;
using Ustaz.Application.Features.Admin.Queries;

namespace Ustaz.API.Controllers;

/// <summary>
/// The global platform administrator's own surface: USTAZ as a business, not any one school.
///
/// This sits beside the older <c>/admin</c> routes rather than replacing them. Those grew up
/// around the marketplace — verifying teachers, moderating reviews — and remain where they are.
/// What was missing was the platform operator's view: how many <em>schools</em> there are and
/// how many are running, who else holds platform authority, and a way to find a person across
/// every school at once.
///
/// Authorization is the platform <c>Admin</c> identity role, which the seeder is the only thing
/// that grants initially. Owning a school confers none of it: a school admin is a
/// <c>TenantMemberRole.Owner</c> inside one workspace and is refused here. That boundary is
/// proven by <c>PlatformAdminSeparationTests</c>, which walks every route on this controller
/// with a school owner's token and requires a 403.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/platform")]
[ApiExplorerSettings(GroupName = "Platform")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class PlatformAdminController : ApiControllerBase
{
    /// <summary>
    /// Schools, people and subscriptions in one call. Built for a dashboard that would
    /// otherwise need a dozen requests to render a row of tiles.
    /// </summary>
    [HttpGet("overview")]
    [SwaggerOperation(Tags = ["Platform"])]
    [ProducesResponseType(typeof(ApiResponse<PlatformOverviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetPlatformOverviewQuery(), cancellationToken));

    /// <summary>
    /// Searches every account on the platform, across all schools, with pagination.
    /// The only list in USTAZ that is not tenant-scoped, which is why it lives here.
    /// </summary>
    [HttpGet("users")]
    [SwaggerOperation(Tags = ["Platform"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<PlatformUserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> SearchUsers(
        [FromQuery] SearchPlatformUsersQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    // -----------------------------------------------------------------------
    // Platform administrators
    // -----------------------------------------------------------------------

    /// <summary>
    /// Everyone who holds platform authority. Each row says whether it is the caller and
    /// whether it is the last remaining administrator, so a console can disable the controls
    /// that would fail rather than offering them.
    /// </summary>
    [HttpGet("administrators")]
    [SwaggerOperation(Tags = ["Platform"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PlatformAdministratorDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdministrators(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetPlatformAdministratorsQuery(), cancellationToken));

    /// <summary>
    /// Grants platform-administrator authority to an existing, active account. Audited.
    /// </summary>
    [HttpPost("administrators/{userId:guid}")]
    [SwaggerOperation(Tags = ["Platform"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GrantAdministrator(
        Guid userId,
        [FromBody] GrantPlatformAdminCommand command,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(command with { UserId = userId }, cancellationToken);

        return OkResponse("Platform administrator access granted.");
    }

    /// <summary>
    /// Removes platform-administrator authority and closes that account's open sessions.
    ///
    /// Refused in two cases, both of which would otherwise be irreversible from inside the
    /// product: revoking your own access, and revoking the last active administrator's.
    /// </summary>
    [HttpDelete("administrators/{userId:guid}")]
    [SwaggerOperation(Tags = ["Platform"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RevokeAdministrator(
        Guid userId,
        [FromBody] RevokePlatformAdminCommand command,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(command with { UserId = userId }, cancellationToken);

        return OkResponse("Platform administrator access revoked.");
    }
}
