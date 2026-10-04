using Microsoft.AspNetCore.RateLimiting;
using Ustaz.API.Extensions;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Application.Features.Teachers.Commands;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Application.Features.Teachers.Queries;

namespace Ustaz.API.Controllers;

/// <summary>Self-service management of the signed-in teacher's own profile.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher")]
[ApiExplorerSettings(GroupName = "Teachers")]
[Authorize(Policy = PolicyNames.TeacherOnly)]
public sealed class TeacherProfileController : ApiControllerBase
{
    /// <summary>Returns the calling teacher's own profile.</summary>
    [HttpGet("profile")]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<TeacherDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProfile(
        [FromServices] Application.Common.Interfaces.ICurrentUserService currentUser,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTeacherByIdQuery(currentUser.RequireTeacherId()), cancellationToken));

    /// <summary>Updates the calling teacher's profile. Only supplied fields change.</summary>
    [HttpPut("profile")]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<TeacherDetailsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateTeacherProfileCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Profile updated successfully.");

    /// <summary>
    /// Returns the calling teacher's verification state and what is still missing before
    /// they can ask to be reviewed.
    /// </summary>
    [HttpGet("profile/verification")]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<TeacherVerificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVerification(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTeacherVerificationQuery(), cancellationToken));

    /// <summary>
    /// Submits the profile for verification. Rejected with a coded business-rule error when
    /// the profile is incomplete or a request is already in review.
    /// </summary>
    [HttpPost("profile/verification")]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<TeacherVerificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitVerification(CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(new SubmitTeacherVerificationCommand(), cancellationToken),
            "Verification request sent.");

    /// <summary>Updates only the biography/headline part of the profile.</summary>
    [HttpPut("about")]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<TeacherDetailsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAbout(
        [FromBody] UpdateTeacherAboutRequest request,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(
                new UpdateTeacherProfileCommand
                {
                    Headline = request.Headline,
                    Bio = request.Bio,
                    YearsOfExperience = request.YearsOfExperience
                },
                cancellationToken),
            "About section updated successfully.");

    /// <summary>Replaces the subjects the teacher is listed under.</summary>
    [HttpPut("subjects")]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubjectDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSubjects(
        [FromBody] UpdateTeacherSubjectsCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Subjects updated successfully.");

    /// <summary>Replaces the educational grades the teacher serves.</summary>
    [HttpPut("grades")]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GradeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateGrades(
        [FromBody] UpdateTeacherGradesCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Grades updated successfully.");

    /// <summary>Replaces the teacher's social links. Each URL is validated against its platform.</summary>
    [HttpPut("social-links")]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TeacherSocialLinkDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSocialLinks(
        [FromBody] UpdateTeacherSocialLinksCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Social links updated successfully.");

    /// <summary>Replaces the teacher's achievement list.</summary>
    [HttpPut("achievements")]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TeacherAchievementDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAchievements(
        [FromBody] UpdateTeacherAchievementsCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Achievements updated successfully.");

    /// <summary>Uploads a new profile picture.</summary>
    [HttpPost("profile/image")]
    [EnableRateLimiting(ApiServiceExtensions.UploadRateLimitPolicy)]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<MediaUploadResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadProfileImage(
        IFormFile file,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(
                new UploadTeacherImageCommand { File = await file.ToUploadRequestAsync(cancellationToken), Kind = TeacherImageKind.Profile },
                cancellationToken),
            "Profile image updated successfully.");

    /// <summary>Uploads a new cover image.</summary>
    [HttpPost("profile/cover")]
    [EnableRateLimiting(ApiServiceExtensions.UploadRateLimitPolicy)]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<MediaUploadResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadCoverImage(
        IFormFile file,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(
                new UploadTeacherImageCommand { File = await file.ToUploadRequestAsync(cancellationToken), Kind = TeacherImageKind.Cover },
                cancellationToken),
            "Cover image updated successfully.");
}

/// <summary>Request body for PUT /api/v1/teacher/about.</summary>
public sealed record UpdateTeacherAboutRequest
{
    public string? Headline { get; init; }

    public string? Bio { get; init; }

    public int? YearsOfExperience { get; init; }
}
