using Ustaz.Application.Features.Calendar;

namespace Ustaz.API.Controllers;

/// <summary>
/// One calendar over a date range for whoever is calling.
///
/// The view is derived from the caller's role, not from a parameter: a student sees the groups
/// they are enrolled in, a guardian sees one confirmed child, and a workspace member sees the
/// workspace. Times are UTC; the response carries the workspace's zone for rendering.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/calendar")]
[ApiExplorerSettings(GroupName = "Calendar")]
[Authorize]
public sealed class CalendarController : ApiControllerBase
{
    /// <summary>
    /// Classes, exams, homework deadlines and held sessions between two dates, ordered by start
    /// time. Both dates are inclusive days; the range may span at most 180 days.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Calendar"])]
    [ProducesResponseType(typeof(ApiResponse<CalendarResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Get(
        [FromQuery] GetCalendarQuery query,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(query, cancellationToken));
}
