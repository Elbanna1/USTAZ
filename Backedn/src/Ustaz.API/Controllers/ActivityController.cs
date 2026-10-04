using Ustaz.Application.Features.Activity;

namespace Ustaz.API.Controllers;

/// <summary>
/// The workspace activity timeline.
///
/// Entries are recorded when a business action succeeds and are never reconstructed from live
/// tables, which is what makes paging through the feed stable. Only entries marked visible to
/// the workspace are returned here, so a learner's private timeline never appears in a staff
/// feed, and the whole query is bounded by the caller's workspace.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher/activity")]
[ApiExplorerSettings(GroupName = "Activity")]
[Authorize(Policy = PolicyNames.TenantMember)]
public sealed class WorkspaceActivityController : ApiControllerBase
{
    /// <summary>
    /// What has happened in the workspace, newest first. Filter by activity type, date range,
    /// or one student. Requires the <c>Analytics.View</c> workspace permission.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Activity"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<ActivityEventDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Get(
        [FromQuery] GetWorkspaceActivityQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>
    /// One student's timeline as staff see it. The student must belong to this workspace;
    /// anyone else's id reads as not found.
    /// </summary>
    [HttpGet("students/{studentId:guid}")]
    [SwaggerOperation(Tags = ["Activity"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<ActivityEventDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetForStudent(
        Guid studentId,
        [FromQuery] GetStudentActivityQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { StudentId = studentId }, cancellationToken);
}
