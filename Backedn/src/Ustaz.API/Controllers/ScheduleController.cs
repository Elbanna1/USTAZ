using Ustaz.Application.Features.Groups.Commands;
using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Application.Features.Schedule;
using Ustaz.Application.Features.Shared.Dtos;

namespace Ustaz.API.Controllers;

/// <summary>Public schedule browsing.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/schedule")]
[ApiExplorerSettings(GroupName = "Schedule")]
[AllowAnonymous]
public sealed class ScheduleController : ApiControllerBase
{
    /// <summary>
    /// The published weekly timetable across the platform.
    /// Filters: teacherId, subjectId, gradeId, dayOfWeek, teachingMode.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Schedule"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScheduleSlotDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchedule(
        [FromQuery] GetPublicScheduleQuery query,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(query, cancellationToken));

    /// <summary>The next sessions, ordered by their next concrete occurrence.</summary>
    [HttpGet("upcoming")]
    [SwaggerOperation(Tags = ["Schedule"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScheduleSlotDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcoming(
        [FromQuery] int take = 10,
        CancellationToken cancellationToken = default) =>
        OkResponse(await Mediator.Send(new GetUpcomingClassesQuery { Take = take }, cancellationToken));
}

/// <summary>Timetable management for the signed-in teacher.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher/schedule")]
[ApiExplorerSettings(GroupName = "Schedule")]
[Authorize(Policy = PolicyNames.TeacherOnly)]
public sealed class TeacherScheduleController : ApiControllerBase
{
    /// <summary>The calling teacher's full weekly timetable, drafts included.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Schedule"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScheduleSlotDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySchedule(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTeacherScheduleQuery(), cancellationToken));

    /// <summary>Adds a weekly session to one of the calling teacher's groups.</summary>
    [HttpPost]
    [SwaggerOperation(Tags = ["Schedule"])]
    [ProducesResponseType(typeof(ApiResponse<GroupScheduleDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddSession(
        [FromBody] AddGroupScheduleCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(await Mediator.Send(command, cancellationToken), "Session added successfully.");

    /// <summary>Updates one weekly session.</summary>
    [HttpPut("{id:guid}")]
    [SwaggerOperation(Tags = ["Schedule"])]
    [ProducesResponseType(typeof(ApiResponse<GroupScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSession(
        Guid id,
        [FromBody] UpdateGroupScheduleCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { ScheduleId = id }, cancellationToken),
            "Session updated successfully.");

    /// <summary>Deletes one weekly session.</summary>
    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Tags = ["Schedule"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSession(
        Guid id,
        [FromQuery] Guid groupId,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteGroupScheduleCommand(groupId, id), cancellationToken);
        return NoContentResponse();
    }
}
