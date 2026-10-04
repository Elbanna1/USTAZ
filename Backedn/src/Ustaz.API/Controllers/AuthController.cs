using Microsoft.AspNetCore.RateLimiting;
using Ustaz.API.Extensions;
using Ustaz.Application.Features.Authentication.Commands;
using Ustaz.Application.Features.Authentication.Dtos;
using Ustaz.Application.Features.Authentication.Queries;

namespace Ustaz.API.Controllers;

/// <summary>Registration, sign-in, token rotation and account recovery.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[ApiExplorerSettings(GroupName = "Authentication")]
[EnableRateLimiting(ApiServiceExtensions.AuthRateLimitPolicy)]
// Endpoints are anonymous by default here; a controller-level [AllowAnonymous] would
// silently override the [Authorize] on /auth/me.
public sealed class AuthController : ApiControllerBase
{
    /// <summary>Creates a student account and returns an access/refresh token pair.</summary>
    [HttpPost("register/student")]
    [SwaggerOperation(Tags = ["Authentication"])]
    [ProducesResponseType(typeof(ApiResponse<AuthenticationResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterStudent(
        [FromBody] RegisterStudentCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command, cancellationToken),
            "Your student account was created successfully.");

    /// <summary>Creates a teacher account. The profile starts unverified and cannot publish yet.</summary>
    [HttpPost("register/teacher")]
    [SwaggerOperation(Tags = ["Authentication"])]
    [ProducesResponseType(typeof(ApiResponse<AuthenticationResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterTeacher(
        [FromBody] RegisterTeacherCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command, cancellationToken),
            "Your teacher account was created. Complete verification to publish content.");

    /// <summary>
    /// Creates a guardian account, optionally requesting access to one child.
    /// </summary>
    /// <remarks>
    /// Access to a child is never granted by asking for it: the link is only confirmed when the
    /// supplied phone number matches the parent number already on the student's profile, and the
    /// student is notified either way.
    /// </remarks>
    [HttpPost("register/parent")]
    [SwaggerOperation(Tags = ["Authentication"])]
    [ProducesResponseType(typeof(ApiResponse<AuthenticationResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterParent(
        [FromBody] RegisterParentCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command, cancellationToken),
            "Your guardian account was created successfully.");

    /// <summary>Signs in with email and password.</summary>
    [HttpPost("login")]
    [SwaggerOperation(Tags = ["Authentication"])]
    [ProducesResponseType(typeof(ApiResponse<AuthenticationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Signed in successfully.");

    /// <summary>Exchanges a refresh token for a new token pair (rotation with reuse detection).</summary>
    [HttpPost("refresh-token")]
    [SwaggerOperation(Tags = ["Authentication"])]
    [ProducesResponseType(typeof(ApiResponse<AuthenticationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Token refreshed.");

    /// <summary>Revokes the presented refresh token and its whole rotation family.</summary>
    [HttpPost("logout")]
    [SwaggerOperation(Tags = ["Authentication"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutCommand command,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(command, cancellationToken);
        return OkResponse("Signed out successfully.");
    }

    /// <summary>Confirms an email address using the token from the verification email.</summary>
    [HttpPost("verify-email")]
    [SwaggerOperation(Tags = ["Authentication"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailCommand command,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(command, cancellationToken);
        return OkResponse("Your email address has been verified.");
    }

    /// <summary>Re-sends the verification email. Always succeeds, to avoid account enumeration.</summary>
    [HttpPost("resend-verification")]
    [SwaggerOperation(Tags = ["Authentication"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendVerification(
        [FromBody] ResendVerificationCommand command,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(command, cancellationToken);
        return OkResponse("If the account exists and is unverified, a verification email has been sent.");
    }

    /// <summary>Starts password recovery. Always succeeds, to avoid account enumeration.</summary>
    [HttpPost("forgot-password")]
    [SwaggerOperation(Tags = ["Authentication"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordCommand command,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(command, cancellationToken);
        return OkResponse("If the account exists, a password reset email has been sent.");
    }

    /// <summary>Completes password recovery and revokes every existing session.</summary>
    [HttpPost("reset-password")]
    [SwaggerOperation(Tags = ["Authentication"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(command, cancellationToken);
        return OkResponse("Your password has been reset. Please sign in again.");
    }

    /// <summary>Changes the signed-in user's password and revokes every existing session.</summary>
    [HttpPost("change-password")]
    [Authorize]
    [SwaggerOperation(Tags = ["Authentication"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordCommand command,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(command, cancellationToken);
        return OkResponse("Your password has been changed. Please sign in again.");
    }

    /// <summary>Sets the language the caller reads notifications and emails in.</summary>
    [HttpPut("me/language")]
    [Authorize]
    [EnableRateLimiting(ApiServiceExtensions.GlobalRateLimitPolicy)]
    [SwaggerOperation(Tags = ["Authentication"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateLanguage(
        [FromBody] UpdateLanguagePreferenceCommand command,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(command, cancellationToken);
        return OkResponse("Language preference updated.");
    }

    /// <summary>Returns the profile of the currently authenticated user.</summary>
    /// <remarks>
    /// Deliberately on the global budget rather than the credential budget: a single-page app
    /// re-reads this on every route change and on every tab focus, which the ten-per-minute
    /// brute-force limit would throttle into a broken session. There is nothing to brute force
    /// here — the caller already holds a valid access token.
    /// </remarks>
    [HttpGet("me")]
    [Authorize]
    [EnableRateLimiting(ApiServiceExtensions.GlobalRateLimitPolicy)]
    [SwaggerOperation(Tags = ["Authentication"])]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetCurrentUserQuery(), cancellationToken));
}
