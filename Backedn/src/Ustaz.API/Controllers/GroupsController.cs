using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Application.Features.Groups.Queries;
using Ustaz.Application.Features.Reviews.Queries;

namespace Ustaz.API.Controllers;

/// <summary>Public group discovery and detail pages.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/groups")]
[ApiExplorerSettings(GroupName = "Groups")]
[AllowAnonymous]
public sealed class GroupsController : ApiControllerBase
{
    /// <summary>
    /// Searches published groups.
    /// Filters: subjectId, gradeId, teacherId, teachingMode, type, minPrice, maxPrice,
    /// hasDiscount, hasAvailableSeats, city, search, sort, page, pageSize.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<GroupListItemDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetGroups(
        [FromQuery] GetGroupsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { IncludeUnpublished = false }, cancellationToken);

    /// <summary>Gets a full group page, including teacher, subject, grade, schedule and curriculum.</summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse<GroupDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetGroupByIdQuery(id), cancellationToken));

    /// <summary>The group's weekly sessions.</summary>
    [HttpGet("{id:guid}/schedule")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GroupScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchedule(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetGroupScheduleQuery(id), cancellationToken));

    /// <summary>The syllabus the group covers.</summary>
    [HttpGet("{id:guid}/curriculum")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GroupCurriculumDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurriculum(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetGroupCurriculumQuery(id), cancellationToken));

    /// <summary>The group's materials. Restricted files are returned locked for non-enrolled callers.</summary>
    [HttpGet("{id:guid}/materials")]
    [SwaggerOperation(Tags = ["Groups"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GroupMaterialDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMaterials(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetGroupMaterialsQuery(id), cancellationToken));

    /// <summary>Approved reviews for this group.</summary>
    [HttpGet("{id:guid}/reviews")]
    [SwaggerOperation(Tags = ["Groups"])]
    public Task<IActionResult> GetReviews(
        Guid id,
        [FromQuery] GetReviewsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { GroupId = id, IncludeUnapproved = false }, cancellationToken);
}
