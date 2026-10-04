using Ustaz.Application.Features.Reviews.Commands;
using Ustaz.Application.Features.Reviews.Dtos;
using Ustaz.Application.Features.Reviews.Queries;

namespace Ustaz.API.Controllers;

/// <summary>Writing and managing reviews.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reviews")]
[ApiExplorerSettings(GroupName = "Reviews")]
public sealed class ReviewsController : ApiControllerBase
{
    /// <summary>Lists approved reviews, filtered by teacher, group or course.</summary>
    [HttpGet]
    [AllowAnonymous]
    [SwaggerOperation(Tags = ["Reviews"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<ReviewDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetReviews(
        [FromQuery] GetReviewsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { IncludeUnapproved = false }, cancellationToken);

    /// <summary>
    /// Writes a review. The student must have a non-cancelled enrollment with the target,
    /// and may review each target only once.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.StudentOnly)]
    [SwaggerOperation(Tags = ["Reviews"])]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateReviewCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command, cancellationToken),
            "Thank you! Your review has been published.");

    /// <summary>Edits the caller's own review. Ratings are recalculated automatically.</summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    [SwaggerOperation(Tags = ["Reviews"])]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateReviewCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { ReviewId = id }, cancellationToken),
            "Review updated successfully.");

    /// <summary>Deletes the caller's own review (admins may delete any).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [SwaggerOperation(Tags = ["Reviews"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteReviewCommand(id), cancellationToken);
        return NoContentResponse();
    }
}

/// <summary>Saved teachers and courses for the signed-in student.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/student/favorites")]
[ApiExplorerSettings(GroupName = "Favorites")]
[Authorize(Policy = PolicyNames.StudentOnly)]
public sealed class FavoritesController : ApiControllerBase
{
    /// <summary>The student's saved teachers.</summary>
    [HttpGet("teachers")]
    [SwaggerOperation(Tags = ["Favorites"])]
    public Task<IActionResult> GetTeachers(
        [FromQuery] Application.Features.Favorites.GetFavoriteTeachersQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Saves a teacher. Saving twice is a no-op.</summary>
    [HttpPost("teachers/{teacherId:guid}")]
    [SwaggerOperation(Tags = ["Favorites"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddTeacher(Guid teacherId, CancellationToken cancellationToken)
    {
        await Mediator.Send(new Application.Features.Favorites.AddFavoriteTeacherCommand(teacherId), cancellationToken);
        return OkResponse("Teacher saved to your favorites.");
    }

    /// <summary>Removes a saved teacher.</summary>
    [HttpDelete("teachers/{teacherId:guid}")]
    [SwaggerOperation(Tags = ["Favorites"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveTeacher(Guid teacherId, CancellationToken cancellationToken)
    {
        await Mediator.Send(new Application.Features.Favorites.RemoveFavoriteTeacherCommand(teacherId), cancellationToken);
        return NoContentResponse();
    }

    /// <summary>The student's saved courses.</summary>
    [HttpGet("courses")]
    [SwaggerOperation(Tags = ["Favorites"])]
    public Task<IActionResult> GetCourses(
        [FromQuery] Application.Features.Favorites.GetFavoriteCoursesQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Saves a course.</summary>
    [HttpPost("courses/{courseId:guid}")]
    [SwaggerOperation(Tags = ["Favorites"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddCourse(Guid courseId, CancellationToken cancellationToken)
    {
        await Mediator.Send(new Application.Features.Favorites.AddFavoriteCourseCommand(courseId), cancellationToken);
        return OkResponse("Course saved to your favorites.");
    }

    /// <summary>Removes a saved course.</summary>
    [HttpDelete("courses/{courseId:guid}")]
    [SwaggerOperation(Tags = ["Favorites"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveCourse(Guid courseId, CancellationToken cancellationToken)
    {
        await Mediator.Send(new Application.Features.Favorites.RemoveFavoriteCourseCommand(courseId), cancellationToken);
        return NoContentResponse();
    }
}
