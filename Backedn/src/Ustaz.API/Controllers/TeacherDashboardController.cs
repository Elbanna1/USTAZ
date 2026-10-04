using Ustaz.Application.Features.Dashboard.Dtos;
using Ustaz.Application.Features.Dashboard.Queries;
using Ustaz.Application.Features.Enrollments.Queries;

namespace Ustaz.API.Controllers;

/// <summary>Dashboard and analytics for the signed-in teacher.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher")]
[ApiExplorerSettings(GroupName = "Dashboard")]
[Authorize(Policy = PolicyNames.TeacherOnly)]
public sealed class TeacherDashboardController : ApiControllerBase
{
    /// <summary>Headline numbers, upcoming classes, top groups and recent activity.</summary>
    [HttpGet("dashboard")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<TeacherDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTeacherDashboardQuery(), cancellationToken));

    /// <summary>Full analytics: revenue trend, student growth and per-group performance.</summary>
    [HttpGet("analytics")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<TeacherAnalyticsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default) =>
        OkResponse(await Mediator.Send(new GetTeacherAnalyticsQuery { Months = months }, cancellationToken));

    /// <summary>
    /// The workspace KPI strip for a date window, with the preceding window of equal length
    /// alongside each count. Use <c>period=Custom</c> with <c>from</c> and <c>to</c> for an
    /// arbitrary range, up to three years wide.
    /// </summary>
    [HttpGet("analytics/overview")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<TeacherAnalyticsOverviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetAnalyticsOverview(
        [FromQuery] GetTeacherAnalyticsOverviewQuery query,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(query, cancellationToken));

    /// <summary>
    /// One student's academic standing. The student must belong to this workspace; anyone
    /// else's id reads as not found.
    /// </summary>
    [HttpGet("analytics/students/{studentId:guid}")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<StudentAnalyticsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentAnalytics(
        Guid studentId,
        [FromQuery] GetStudentAnalyticsQuery query,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(query with { StudentId = studentId }, cancellationToken));

    /// <summary>Monthly revenue split between groups and courses.</summary>
    [HttpGet("analytics/revenue")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RevenuePointDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default)
    {
        var analytics = await Mediator.Send(new GetTeacherAnalyticsQuery { Months = months }, cancellationToken);
        return OkResponse(analytics.RevenueTrend);
    }

    /// <summary>Monthly new and cumulative student counts.</summary>
    [HttpGet("analytics/students")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StudentGrowthPointDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentGrowth(
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default)
    {
        var analytics = await Mediator.Send(new GetTeacherAnalyticsQuery { Months = months }, cancellationToken);
        return OkResponse(analytics.StudentGrowth);
    }

    /// <summary>Fill rate, revenue and rating per group.</summary>
    [HttpGet("analytics/groups")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GroupPerformanceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroupPerformance(CancellationToken cancellationToken)
    {
        var analytics = await Mediator.Send(new GetTeacherAnalyticsQuery { Months = 12 }, cancellationToken);
        return OkResponse(analytics.GroupPerformance);
    }

    /// <summary>Every student enrolled in the calling teacher's groups.</summary>
    [HttpGet("enrollments/groups")]
    [SwaggerOperation(Tags = ["Enrollments"])]
    public Task<IActionResult> GetGroupEnrollments(
        [FromQuery] GetTeacherGroupEnrollmentsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Every student enrolled in the calling teacher's courses.</summary>
    [HttpGet("enrollments/courses")]
    [SwaggerOperation(Tags = ["Enrollments"])]
    public Task<IActionResult> GetCourseEnrollments(
        [FromQuery] GetTeacherCourseEnrollmentsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);
}

/// <summary>Cross-entity search for the header search box.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/search")]
[ApiExplorerSettings(GroupName = "Search")]
[AllowAnonymous]
public sealed class SearchController : ApiControllerBase
{
    /// <summary>Searches teachers, groups, courses and subjects at once.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Search"])]
    [ProducesResponseType(typeof(ApiResponse<Application.Features.Search.GlobalSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] Application.Features.Search.GlobalSearchQuery query,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(query, cancellationToken));
}
