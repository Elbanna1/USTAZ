using Ustaz.Application.Features.Courses.Queries;
using Ustaz.Application.Features.Groups.Queries;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Application.Features.Subjects.Queries;
using Ustaz.Application.Features.Teachers.Queries;

namespace Ustaz.API.Controllers;

/// <summary>The public subject catalogue and everything filed under a subject.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/subjects")]
[ApiExplorerSettings(GroupName = "Subjects")]
[AllowAnonymous]
public sealed class SubjectsController : ApiControllerBase
{
    /// <summary>Lists every active subject with teacher/group/course counters.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Subjects"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubjectDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetSubjectsQuery(), cancellationToken));

    /// <summary>Gets one subject by id or slug.</summary>
    [HttpGet("{idOrSlug}")]
    [SwaggerOperation(Tags = ["Subjects"])]
    [ProducesResponseType(typeof(ApiResponse<SubjectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string idOrSlug, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetSubjectByIdQuery(idOrSlug), cancellationToken));

    /// <summary>Verified teachers who teach this subject.</summary>
    [HttpGet("{id:guid}/teachers")]
    [SwaggerOperation(Tags = ["Subjects"])]
    public Task<IActionResult> GetTeachers(
        Guid id,
        [FromQuery] GetTeachersQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { SubjectId = id, IncludeUnverified = false }, cancellationToken);

    /// <summary>Published groups for this subject.</summary>
    [HttpGet("{id:guid}/groups")]
    [SwaggerOperation(Tags = ["Subjects"])]
    public Task<IActionResult> GetGroups(
        Guid id,
        [FromQuery] GetGroupsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { SubjectId = id, IncludeUnpublished = false }, cancellationToken);

    /// <summary>Published courses for this subject.</summary>
    [HttpGet("{id:guid}/courses")]
    [SwaggerOperation(Tags = ["Subjects"])]
    public Task<IActionResult> GetCourses(
        Guid id,
        [FromQuery] GetCoursesQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { SubjectId = id, IncludeUnpublished = false }, cancellationToken);
}

/// <summary>The public educational-grade catalogue and everything filed under a grade.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/grades")]
[ApiExplorerSettings(GroupName = "Grades")]
[AllowAnonymous]
public sealed class GradesController : ApiControllerBase
{
    /// <summary>Lists every active educational grade.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Grades"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GradeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new Application.Features.Grades.Queries.GetGradesQuery(), cancellationToken));

    /// <summary>Gets one grade by id or slug.</summary>
    [HttpGet("{idOrSlug}")]
    [SwaggerOperation(Tags = ["Grades"])]
    [ProducesResponseType(typeof(ApiResponse<GradeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string idOrSlug, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(
            new Application.Features.Grades.Queries.GetGradeByIdQuery(idOrSlug), cancellationToken));

    /// <summary>Verified teachers who serve this grade.</summary>
    [HttpGet("{id:guid}/teachers")]
    [SwaggerOperation(Tags = ["Grades"])]
    public Task<IActionResult> GetTeachers(
        Guid id,
        [FromQuery] GetTeachersQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { GradeId = id, IncludeUnverified = false }, cancellationToken);

    /// <summary>Published groups for this grade.</summary>
    [HttpGet("{id:guid}/groups")]
    [SwaggerOperation(Tags = ["Grades"])]
    public Task<IActionResult> GetGroups(
        Guid id,
        [FromQuery] GetGroupsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { GradeId = id, IncludeUnpublished = false }, cancellationToken);

    /// <summary>Published courses for this grade.</summary>
    [HttpGet("{id:guid}/courses")]
    [SwaggerOperation(Tags = ["Grades"])]
    public Task<IActionResult> GetCourses(
        Guid id,
        [FromQuery] GetCoursesQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { GradeId = id, IncludeUnpublished = false }, cancellationToken);
}
