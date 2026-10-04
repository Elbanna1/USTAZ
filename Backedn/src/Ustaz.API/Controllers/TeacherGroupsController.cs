using Ustaz.Application.Features.Enrollments.Commands;
using Ustaz.Application.Features.Enrollments.Dtos;
using Ustaz.Application.Features.Groups.Commands;
using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Application.Features.Groups.Queries;

namespace Ustaz.API.Controllers;

/// <summary>
/// Group management for the signed-in teacher. Ownership is enforced in the handlers from
/// the JWT, so a teacher can never touch another teacher's groups.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher/groups")]
[ApiExplorerSettings(GroupName = "Groups")]
[Authorize(Policy = PolicyNames.TeacherOnly)]
public sealed class TeacherGroupsController : ApiControllerBase
{
    /// <summary>Lists the calling teacher's groups, drafts included.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<GroupListItemDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyGroups(
        [FromQuery] GetTeacherGroupsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Gets one of the calling teacher's groups.</summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse<GroupDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetGroupByIdQuery(id), cancellationToken));

    /// <summary>Creates a group as a draft. Publishing is a separate, validated step.</summary>
    [HttpPost]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse<GroupDetailsDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateGroupCommand command,
        CancellationToken cancellationToken)
    {
        var group = await Mediator.Send(command, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = group.Id, version = "1" }, group, "Group created successfully.");
    }

    /// <summary>Updates a group the caller owns.</summary>
    [HttpPut("{id:guid}")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse<GroupDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateGroupCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { GroupId = id }, cancellationToken),
            "Group updated successfully.");

    /// <summary>Deletes a group, or archives it when it already has enrollment history.</summary>
    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteGroupCommand(id), cancellationToken);
        return NoContentResponse();
    }

    /// <summary>Publishes the group. Requires a verified teacher and at least one active session.</summary>
    [HttpPost("{id:guid}/publish")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new PublishGroupCommand(id), cancellationToken);
        return OkResponse("Group published successfully.");
    }

    /// <summary>Hides the group from public discovery without deleting anything.</summary>
    [HttpPost("{id:guid}/unpublish")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new UnpublishGroupCommand(id), cancellationToken);
        return OkResponse("Group unpublished successfully.");
    }

    // ---- Weekly schedule --------------------------------------------------

    /// <summary>Adds a weekly session. Overlapping sessions are rejected.</summary>
    [HttpPost("{id:guid}/schedule")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse<GroupScheduleDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddSchedule(
        Guid id,
        [FromBody] AddGroupScheduleCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command with { GroupId = id }, cancellationToken),
            "Session added successfully.");

    /// <summary>Updates one weekly session.</summary>
    [HttpPut("{id:guid}/schedule/{scheduleId:guid}")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse<GroupScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSchedule(
        Guid id,
        Guid scheduleId,
        [FromBody] UpdateGroupScheduleCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { GroupId = id, ScheduleId = scheduleId }, cancellationToken),
            "Session updated successfully.");

    /// <summary>Deletes one weekly session.</summary>
    [HttpDelete("{id:guid}/schedule/{scheduleId:guid}")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSchedule(
        Guid id,
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteGroupScheduleCommand(id, scheduleId), cancellationToken);
        return NoContentResponse();
    }

    // ---- Curriculum -------------------------------------------------------

    /// <summary>Adds a syllabus item.</summary>
    [HttpPost("{id:guid}/curriculum")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse<GroupCurriculumDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddCurriculum(
        Guid id,
        [FromBody] AddGroupCurriculumCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command with { GroupId = id }, cancellationToken),
            "Curriculum item added successfully.");

    /// <summary>Updates a syllabus item.</summary>
    [HttpPut("{id:guid}/curriculum/{curriculumId:guid}")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse<GroupCurriculumDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCurriculum(
        Guid id,
        Guid curriculumId,
        [FromBody] UpdateGroupCurriculumCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { GroupId = id, CurriculumId = curriculumId }, cancellationToken),
            "Curriculum item updated successfully.");

    /// <summary>Deletes a syllabus item.</summary>
    [HttpDelete("{id:guid}/curriculum/{curriculumId:guid}")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteCurriculum(
        Guid id,
        Guid curriculumId,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteGroupCurriculumCommand(id, curriculumId), cancellationToken);
        return NoContentResponse();
    }
}

/// <summary>
/// Roster management for a group. This sits on the workspace policy rather than the
/// teacher one because an assistant holding Students.Manage registers students too.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher/groups/{groupId:guid}/students")]
[ApiExplorerSettings(GroupName = "Groups")]
[Authorize(Policy = PolicyNames.TenantMember)]
public sealed class TeacherGroupStudentsController : ApiControllerBase
{
    /// <summary>Enrolls a student into the group on their behalf.</summary>
    [HttpPost]
    [SwaggerOperation(Tags = ["Enrollments"])]
    [ProducesResponseType(typeof(ApiResponse<GroupEnrollmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EnrollStudent(
        Guid groupId,
        [FromBody] EnrollStudentInGroupCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command with { GroupId = groupId }, cancellationToken),
            "The student has been enrolled in the group.");
}
