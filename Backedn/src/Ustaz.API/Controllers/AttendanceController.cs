using Ustaz.Application.Features.Attendance;
using Ustaz.Application.Features.Attendance.Dtos;

namespace Ustaz.API.Controllers;

/// <summary>Attendance registers for the caller's workspace.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher/attendance")]
[ApiExplorerSettings(GroupName = "Attendance")]
[Authorize(Policy = PolicyNames.TenantMember)]
public sealed class TeacherAttendanceController : ApiControllerBase
{
    /// <summary>Opens a register for one group on one date, optionally pre-filled as present.</summary>
    [HttpPost("sessions")]
    [SwaggerOperation(Tags = ["Attendance"])]
    [ProducesResponseType(typeof(ApiResponse<AttendanceSessionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateAttendanceSessionCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(await Mediator.Send(command, cancellationToken), "Attendance session created.");

    /// <summary>Attendance sessions, filterable by group, date range and status.</summary>
    [HttpGet("sessions")]
    [SwaggerOperation(Tags = ["Attendance"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<AttendanceSessionDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetSessions(
        [FromQuery] GetAttendanceSessionsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>One session with all its student records.</summary>
    [HttpGet("sessions/{id:guid}")]
    [SwaggerOperation(Tags = ["Attendance"])]
    [ProducesResponseType(typeof(ApiResponse<AttendanceSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSession(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetAttendanceSessionQuery(id), cancellationToken));

    /// <summary>Updates a session's details or closes it.</summary>
    [HttpPut("sessions/{id:guid}")]
    [SwaggerOperation(Tags = ["Attendance"])]
    [ProducesResponseType(typeof(ApiResponse<AttendanceSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSession(
        Guid id,
        [FromBody] UpdateAttendanceSessionCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { SessionId = id }, cancellationToken),
            "Attendance session updated.");

    /// <summary>
    /// Records attendance for one or more students. Marking a student absent or late notifies
    /// them and their confirmed guardians, honouring each recipient's preferences.
    /// </summary>
    [HttpPost("sessions/{id:guid}/records")]
    [SwaggerOperation(Tags = ["Attendance"])]
    [ProducesResponseType(typeof(ApiResponse<AttendanceSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordAttendance(
        Guid id,
        [FromBody] RecordAttendanceCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { SessionId = id }, cancellationToken),
            "Attendance recorded.");

    /// <summary>Bulk entry point; identical semantics to the records endpoint.</summary>
    [HttpPost("sessions/{id:guid}/bulk")]
    [SwaggerOperation(Tags = ["Attendance"])]
    [ProducesResponseType(typeof(ApiResponse<AttendanceSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordAttendanceBulk(
        Guid id,
        [FromBody] RecordAttendanceCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { SessionId = id }, cancellationToken),
            "Attendance recorded for the whole group.");

    /// <summary>Corrects a single attendance row.</summary>
    [HttpPut("records/{id:guid}")]
    [SwaggerOperation(Tags = ["Attendance"])]
    [ProducesResponseType(typeof(ApiResponse<AttendanceRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRecord(
        Guid id,
        [FromBody] UpdateAttendanceRecordCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { RecordId = id }, cancellationToken),
            "Attendance record updated.");

    /// <summary>One student's attendance history in this workspace.</summary>
    [HttpGet("student/{studentId:guid}")]
    [SwaggerOperation(Tags = ["Attendance"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<AttendanceRecordDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetStudentAttendance(
        Guid studentId,
        [FromQuery] GetStudentAttendanceQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { StudentId = studentId }, cancellationToken);

    /// <summary>Attendance records for one group.</summary>
    [HttpGet("group/{groupId:guid}")]
    [SwaggerOperation(Tags = ["Attendance"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<AttendanceSessionDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetGroupAttendance(
        Guid groupId,
        [FromQuery] GetAttendanceSessionsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { GroupId = groupId }, cancellationToken);

    /// <summary>Attendance rates for the workspace, a group, or one student.</summary>
    [HttpGet("statistics")]
    [SwaggerOperation(Tags = ["Attendance"])]
    [ProducesResponseType(typeof(ApiResponse<AttendanceStatisticsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] GetAttendanceStatisticsQuery query,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(query, cancellationToken));
}
