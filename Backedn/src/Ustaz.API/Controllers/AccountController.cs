using Microsoft.AspNetCore.RateLimiting;
using Ustaz.API.Extensions;
using Ustaz.Application.Features.Accounts;

namespace Ustaz.API.Controllers;

/// <summary>
/// The signed-in user's own account, regardless of which role they hold.
///
/// These routes exist because a profile picture is not a teacher feature. Students, parents and
/// administrators all have their avatar rendered across the product, but only teachers had an
/// endpoint to set one — so for everyone else the field stayed empty forever. Rather than add a
/// near-identical upload route under <c>/student</c>, <c>/parent</c> and <c>/admin</c>, the
/// picture lives on the identity user and is reached the same way by all of them.
///
/// There is no id in any of these requests, which is what makes them safe: the resource is
/// always the caller's own, so there is nothing a client could point somewhere else.
/// Teachers keep <c>POST /teacher/profile/image</c> as well — it also handles the cover image
/// on their public page — and the two stay in step because the handler writes both rows.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me")]
[ApiExplorerSettings(GroupName = "Account")]
[Authorize]
public sealed class AccountController : ApiControllerBase
{
    /// <summary>The caller's current profile picture URL, or null when they have none.</summary>
    [HttpGet("profile-picture")]
    [SwaggerOperation(Tags = ["Account"])]
    [ProducesResponseType(typeof(ApiResponse<ProfilePictureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfilePicture(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetMyProfilePictureQuery(), cancellationToken));

    /// <summary>
    /// Uploads a profile picture, replacing any existing one. The previous file is removed only
    /// after the new one is committed, so a failed upload never leaves the user with no avatar.
    /// </summary>
    [HttpPost("profile-picture")]
    [EnableRateLimiting(ApiServiceExtensions.UploadRateLimitPolicy)]
    [SwaggerOperation(Tags = ["Account"])]
    [ProducesResponseType(typeof(ApiResponse<ProfilePictureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadProfilePicture(
        IFormFile file,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(
                new UploadMyProfilePictureCommand
                {
                    // The async conversion captures the file's leading bytes, so validation can
                    // reject a mislabelled upload as a field error rather than letting the
                    // storage layer throw on it later.
                    File = await file.ToUploadRequestAsync(cancellationToken)
                },
                cancellationToken),
            "Your profile picture has been updated.");

    /// <summary>
    /// Removes the caller's profile picture and returns them to the default avatar. Idempotent.
    /// </summary>
    [HttpDelete("profile-picture")]
    [SwaggerOperation(Tags = ["Account"])]
    [ProducesResponseType(typeof(ApiResponse<ProfilePictureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveProfilePicture(CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(new RemoveMyProfilePictureCommand(), cancellationToken),
            "Your profile picture has been removed.");
}
