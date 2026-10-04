using Ustaz.Application.Features.Courses.Queries;
using Ustaz.Application.Features.Groups.Queries;
using Ustaz.Application.Features.Reviews.Queries;
using Ustaz.Application.Features.Schedule;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Application.Features.Teachers.Queries;

namespace Ustaz.API.Controllers;

/// <summary>Public teacher discovery and profile pages.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teachers")]
[ApiExplorerSettings(GroupName = "Teachers")]
[AllowAnonymous]
public sealed class TeachersController : ApiControllerBase
{
    /// <summary>
    /// Searches the verified teacher directory.
    /// Filters: subjectId, gradeId, city, teachingMode, minRating, featuredOnly, search, sort, page, pageSize.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<TeacherListItemDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetTeachers(
        [FromQuery] GetTeachersQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { IncludeUnverified = false }, cancellationToken);

    /// <summary>Gets a teacher's public profile.</summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<TeacherDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTeacherByIdQuery(id), cancellationToken));

    /// <summary>Alias of GET /teachers/{id}, kept because the client links to /profile.</summary>
    [HttpGet("{id:guid}/profile")]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<TeacherDetailsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTeacherByIdQuery(id), cancellationToken));

    /// <summary>The teacher's published groups.</summary>
    [HttpGet("{id:guid}/groups")]
    [SwaggerOperation(Tags = ["Teachers"])]
    public Task<IActionResult> GetGroups(
        Guid id,
        [FromQuery] GetGroupsByTeacherQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { TeacherId = id }, cancellationToken);

    /// <summary>The teacher's published courses.</summary>
    [HttpGet("{id:guid}/courses")]
    [SwaggerOperation(Tags = ["Teachers"])]
    public Task<IActionResult> GetCourses(
        Guid id,
        [FromQuery] GetCoursesByTeacherQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { TeacherId = id }, cancellationToken);

    /// <summary>The teacher's weekly timetable across their published groups.</summary>
    [HttpGet("{id:guid}/schedule")]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScheduleSlotDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchedule(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(
            new GetTeacherScheduleQuery { TeacherId = id, IncludeUnpublished = false },
            cancellationToken));

    /// <summary>Approved reviews written about this teacher.</summary>
    [HttpGet("{id:guid}/reviews")]
    [SwaggerOperation(Tags = ["Teachers"])]
    public Task<IActionResult> GetReviews(
        Guid id,
        [FromQuery] GetReviewsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { TeacherId = id, IncludeUnapproved = false }, cancellationToken);

    /// <summary>Star distribution for this teacher.</summary>
    [HttpGet("{id:guid}/reviews/summary")]
    [SwaggerOperation(Tags = ["Teachers"])]
    public async Task<IActionResult> GetReviewSummary(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetReviewSummaryQuery { TeacherId = id }, cancellationToken));

    /// <summary>The teacher's listed achievements.</summary>
    [HttpGet("{id:guid}/achievements")]
    [SwaggerOperation(Tags = ["Teachers"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TeacherAchievementDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAchievements(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTeacherAchievementsQuery(id), cancellationToken));
}
