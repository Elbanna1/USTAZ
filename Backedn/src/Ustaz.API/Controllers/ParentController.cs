using Ustaz.Application.Features.Announcements;
using Ustaz.Application.Features.Attendance;
using Ustaz.Application.Features.Attendance.Dtos;
using Ustaz.Application.Features.Dashboard.Dtos;
using Ustaz.Application.Features.Dashboard.Queries;
using Ustaz.Application.Features.Exams;
using Ustaz.Application.Features.Exams.Dtos;
using Ustaz.Application.Features.Homework;
using Ustaz.Application.Features.Parents;
using Ustaz.Application.Features.Payments;

namespace Ustaz.API.Controllers;

/// <summary>
/// The guardian workspace. Every route that names a student verifies a confirmed
/// parent-child link first, so a guardian can never reach another family's data.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/parent")]
[ApiExplorerSettings(GroupName = "Parents")]
[Authorize(Policy = PolicyNames.ParentOnly)]
public sealed class ParentController : ApiControllerBase
{
    /// <summary>The calling guardian's profile.</summary>
    [HttpGet("profile")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<ParentProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetParentProfileQuery(), cancellationToken));

    /// <summary>Updates the guardian's profile.</summary>
    [HttpPut("profile")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<ParentProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateParentProfileCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Profile updated successfully.");

    /// <summary>Everything the guardian's home screen shows, across all their children.</summary>
    [HttpGet("dashboard")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<ParentDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetParentDashboardQuery(), cancellationToken));

    /// <summary>The guardian's linked children.</summary>
    [HttpGet("children")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ChildDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildren(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetMyChildrenQuery(), cancellationToken));

    /// <summary>
    /// Links a child by their account email. The link is confirmed automatically only when the
    /// supplied phone number matches the one on the student's profile; otherwise the student
    /// must approve it.
    /// </summary>
    [HttpPost("children/link")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<ChildDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkChild(
        [FromBody] LinkChildCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(await Mediator.Send(command, cancellationToken), "Child linked successfully.");

    /// <summary>Removes the link to a child.</summary>
    [HttpDelete("children/{studentId:guid}")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnlinkChild(Guid studentId, CancellationToken cancellationToken)
    {
        await Mediator.Send(new UnlinkChildCommand(studentId), cancellationToken);
        return NoContentResponse();
    }

    /// <summary>One child's overview.</summary>
    [HttpGet("children/{studentId:guid}")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<ChildDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChild(Guid studentId, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetChildQuery(studentId), cancellationToken));

    /// <summary>One child's academic progress summary.</summary>
    [HttpGet("children/{studentId:guid}/progress")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<ChildOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildProgress(Guid studentId, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetChildProgressQuery(studentId), cancellationToken));

    /// <summary>
    /// One child's full academic standing: averages, attendance, completion, learning streak and
    /// their strongest and weakest subjects. Requires a confirmed link to that child.
    /// </summary>
    [HttpGet("children/{studentId:guid}/analytics")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<StudentAnalyticsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildAnalytics(
        Guid studentId,
        [FromQuery] GetStudentAnalyticsQuery query,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(query with { StudentId = studentId }, cancellationToken));

    /// <summary>One child's attendance history.</summary>
    [HttpGet("children/{studentId:guid}/attendance")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<AttendanceRecordDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetChildAttendance(
        Guid studentId,
        [FromQuery] GetChildAttendanceQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { StudentId = studentId }, cancellationToken);

    /// <summary>One child's exams.</summary>
    [HttpGet("children/{studentId:guid}/exams")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<ExamDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetChildExams(
        Guid studentId,
        [FromQuery] GetChildExamsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { StudentId = studentId }, cancellationToken);

    /// <summary>One child's released grades.</summary>
    [HttpGet("children/{studentId:guid}/grades")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<StudentGradesSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildGrades(Guid studentId, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetChildGradesQuery(studentId), cancellationToken));

    /// <summary>One child's homework and its submission status.</summary>
    [HttpGet("children/{studentId:guid}/homework")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<HomeworkSubmissionDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetChildHomework(
        Guid studentId,
        [FromQuery] GetChildHomeworkQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { StudentId = studentId }, cancellationToken);

    /// <summary>One child's tuition payments.</summary>
    [HttpGet("children/{studentId:guid}/payments")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<EducationalPaymentDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetChildPayments(
        Guid studentId,
        [FromQuery] GetChildPaymentsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { StudentId = studentId }, cancellationToken);

    /// <summary>Announcements addressed to guardians of the caller's children.</summary>
    [HttpGet("announcements")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<AnnouncementDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetAnnouncements(
        [FromQuery] GetParentAnnouncementsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);
}
