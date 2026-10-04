using Ustaz.Application.Features.Dashboard.Dtos;
using Ustaz.Application.Features.Dashboard.Queries;
using Ustaz.Application.Features.Enrollments.Queries;
using Ustaz.Application.Features.Inquiries;
using Ustaz.Application.Features.Schedule;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Application.Features.Students;

namespace Ustaz.API.Controllers;

/// <summary>The signed-in student's own workspace.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/student")]
[ApiExplorerSettings(GroupName = "Students")]
[Authorize(Policy = PolicyNames.StudentOnly)]
public sealed class StudentController : ApiControllerBase
{
    /// <summary>The student's own profile.</summary>
    [HttpGet("profile")]
    [SwaggerOperation(Tags = ["Students"])]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetStudentProfileQuery(), cancellationToken));

    /// <summary>Updates the student's own profile. Omitted fields are left untouched.</summary>
    [HttpPut("profile")]
    [SwaggerOperation(Tags = ["Students"])]
    [ProducesResponseType(typeof(ApiResponse<StudentProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateStudentProfileCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command, cancellationToken),
            "Your profile has been updated.");

    /// <summary>Everything the student home screen renders: groups, courses, classes, saves, notifications.</summary>
    [HttpGet("dashboard")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<StudentDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetStudentDashboardQuery(), cancellationToken));

    /// <summary>Per-course learning progress.</summary>
    [HttpGet("progress")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CourseProgressDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProgress(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetStudentProgressQuery(), cancellationToken));

    /// <summary>The next classes from the student's active group enrollments.</summary>
    [HttpGet("upcoming-classes")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScheduleSlotDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcomingClasses(
        [FromQuery] int take = 5,
        CancellationToken cancellationToken = default) =>
        OkResponse(await Mediator.Send(new GetUpcomingClassesQuery { Take = take }, cancellationToken));

    /// <summary>
    /// The student's own academic standing: averages, attendance, completion, learning streak
    /// and their strongest and weakest subjects.
    /// </summary>
    [HttpGet("analytics")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<StudentAnalyticsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] GetStudentAnalyticsQuery query,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(query with { StudentId = null }, cancellationToken));

    /// <summary>Recent enrollments, reviews and other activity.</summary>
    [HttpGet("recent-activity")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ActivityItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentActivity(
        [FromQuery] int take = 10,
        CancellationToken cancellationToken = default) =>
        OkResponse(await Mediator.Send(new GetStudentRecentActivityQuery { Take = take }, cancellationToken));

    /// <summary>The student's full weekly timetable.</summary>
    [HttpGet("schedule")]
    [SwaggerOperation(Tags = ["Schedule"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScheduleSlotDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchedule(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetStudentScheduleQuery(), cancellationToken));

    /// <summary>The student's next classes.</summary>
    [HttpGet("schedule/upcoming")]
    [SwaggerOperation(Tags = ["Schedule"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScheduleSlotDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcomingSchedule(
        [FromQuery] int take = 5,
        CancellationToken cancellationToken = default) =>
        OkResponse(await Mediator.Send(new GetUpcomingClassesQuery { Take = take }, cancellationToken));

    /// <summary>The student's group subscriptions.</summary>
    [HttpGet("enrollments/groups")]
    [SwaggerOperation(Tags = ["Enrollments"])]
    public Task<IActionResult> GetGroupEnrollments(
        [FromQuery] GetStudentGroupEnrollmentsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>The student's course enrollments.</summary>
    [HttpGet("enrollments/courses")]
    [SwaggerOperation(Tags = ["Enrollments"])]
    public Task<IActionResult> GetCourseEnrollments(
        [FromQuery] GetStudentCourseEnrollmentsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Inquiries the student has sent to teachers.</summary>
    [HttpGet("inquiries")]
    [SwaggerOperation(Tags = ["Inquiries"])]
    public Task<IActionResult> GetInquiries(
        [FromQuery] GetStudentInquiriesQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);
}
