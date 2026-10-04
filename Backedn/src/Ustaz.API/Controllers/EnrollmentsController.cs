using Ustaz.Application.Features.Enrollments.Commands;
using Ustaz.Application.Features.Enrollments.Dtos;
using Ustaz.Application.Features.Enrollments.Queries;

namespace Ustaz.API.Controllers;

/// <summary>Group and course enrollment operations.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/enrollments")]
[ApiExplorerSettings(GroupName = "Enrollments")]
[Authorize]
public sealed class EnrollmentsController : ApiControllerBase
{
    /// <summary>
    /// Subscribes the calling student to a group. Capacity, publication state and duplicate
    /// enrollment are all validated, and an optional discount code is applied.
    /// </summary>
    [HttpPost("groups")]
    [Authorize(Policy = PolicyNames.StudentOnly)]
    [SwaggerOperation(Tags = ["Enrollments"])]
    [ProducesResponseType(typeof(ApiResponse<GroupEnrollmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EnrollInGroup(
        [FromBody] EnrollInGroupCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command, cancellationToken),
            "You have joined the group successfully.");

    /// <summary>Gets one group enrollment (student owner, owning teacher or admin).</summary>
    [HttpGet("groups/{id:guid}")]
    [SwaggerOperation(Tags = ["Enrollments"])]
    [ProducesResponseType(typeof(ApiResponse<GroupEnrollmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetGroupEnrollment(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetGroupEnrollmentByIdQuery(id), cancellationToken));

    /// <summary>Cancels a group enrollment and frees the seat.</summary>
    [HttpPost("groups/{id:guid}/cancel")]
    [SwaggerOperation(Tags = ["Enrollments"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelGroupEnrollment(
        Guid id,
        [FromBody] CancelGroupEnrollmentRequest? request,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(
            new CancelGroupEnrollmentCommand { EnrollmentId = id, Reason = request?.Reason },
            cancellationToken);

        return OkResponse("The enrollment has been cancelled.");
    }

    /// <summary>Enrolls the calling student in a recorded course.</summary>
    [HttpPost("courses")]
    [Authorize(Policy = PolicyNames.StudentOnly)]
    [SwaggerOperation(Tags = ["Enrollments"])]
    [ProducesResponseType(typeof(ApiResponse<CourseEnrollmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EnrollInCourse(
        [FromBody] EnrollInCourseCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command, cancellationToken),
            "You have enrolled in the course successfully.");

    /// <summary>Gets one course enrollment (student owner, owning teacher or admin).</summary>
    [HttpGet("courses/{id:guid}")]
    [SwaggerOperation(Tags = ["Enrollments"])]
    [ProducesResponseType(typeof(ApiResponse<CourseEnrollmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourseEnrollment(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetCourseEnrollmentByIdQuery(id), cancellationToken));

    /// <summary>Cancels a course enrollment.</summary>
    [HttpPost("courses/{id:guid}/cancel")]
    [SwaggerOperation(Tags = ["Enrollments"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelCourseEnrollment(
        Guid id,
        [FromBody] CancelGroupEnrollmentRequest? request,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(
            new CancelCourseEnrollmentCommand { EnrollmentId = id, Reason = request?.Reason },
            cancellationToken);

        return OkResponse("The enrollment has been cancelled.");
    }

    /// <summary>Updates the calling student's watch progress for a course.</summary>
    [HttpPut("courses/{id:guid}/progress")]
    [Authorize(Policy = PolicyNames.StudentOnly)]
    [SwaggerOperation(Tags = ["Enrollments"])]
    [ProducesResponseType(typeof(ApiResponse<CourseEnrollmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProgress(
        Guid id,
        [FromBody] UpdateProgressRequest request,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(
                new UpdateCourseProgressCommand { EnrollmentId = id, ProgressPercentage = request.ProgressPercentage },
                cancellationToken),
            "Progress updated.");
}

/// <summary>Optional body for the cancellation endpoints.</summary>
public sealed record CancelGroupEnrollmentRequest
{
    public string? Reason { get; init; }
}

/// <summary>Body for PUT /enrollments/courses/{id}/progress.</summary>
public sealed record UpdateProgressRequest
{
    public int ProgressPercentage { get; init; }
}
