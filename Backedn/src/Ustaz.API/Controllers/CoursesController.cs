using Ustaz.Application.Features.Courses.Dtos;
using Ustaz.Application.Features.Courses.Queries;
using Ustaz.Application.Features.Reviews.Queries;

namespace Ustaz.API.Controllers;

/// <summary>Public course catalogue and detail pages.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/courses")]
[ApiExplorerSettings(GroupName = "Courses")]
[AllowAnonymous]
public sealed class CoursesController : ApiControllerBase
{
    /// <summary>
    /// Searches published courses.
    /// Filters: subjectId, gradeId, teacherId, minPrice, maxPrice, isFree, featuredOnly,
    /// minRating, search, sort, page, pageSize.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<CourseListItemDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetCourses(
        [FromQuery] GetCoursesQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { IncludeUnpublished = false }, cancellationToken);

    /// <summary>
    /// Gets a full course page. Video URLs are only included for preview lessons unless the
    /// caller is enrolled, owns the course, or is an admin.
    /// </summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse<CourseDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetCourseByIdQuery(id), cancellationToken));

    /// <summary>The course outline: sections and their lessons.</summary>
    [HttpGet("{id:guid}/sections")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CourseSectionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSections(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetCourseSectionsQuery(id), cancellationToken));

    /// <summary>Approved reviews for this course.</summary>
    [HttpGet("{id:guid}/reviews")]
    [SwaggerOperation(Tags = ["Courses"])]
    public Task<IActionResult> GetReviews(
        Guid id,
        [FromQuery] GetReviewsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { CourseId = id, IncludeUnapproved = false }, cancellationToken);
}
