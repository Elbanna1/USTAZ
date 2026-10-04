using Ustaz.Application.Features.Courses.Commands;
using Ustaz.Application.Features.Courses.Dtos;
using Ustaz.Application.Features.Courses.Queries;

namespace Ustaz.API.Controllers;

/// <summary>Course authoring for the signed-in teacher.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher/courses")]
[ApiExplorerSettings(GroupName = "Courses")]
[Authorize(Policy = PolicyNames.TeacherOnly)]
public sealed class TeacherCoursesController : ApiControllerBase
{
    /// <summary>Lists the calling teacher's courses, drafts included.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<CourseListItemDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyCourses(
        [FromQuery] GetTeacherCoursesQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Gets one of the calling teacher's courses.</summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse<CourseDetailsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetCourseByIdQuery(id), cancellationToken));

    /// <summary>Creates a course as a draft.</summary>
    [HttpPost]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse<CourseDetailsDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCourseCommand command,
        CancellationToken cancellationToken)
    {
        var course = await Mediator.Send(command, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = course.Id, version = "1" }, course, "Course created successfully.");
    }

    /// <summary>Updates a course the caller owns.</summary>
    [HttpPut("{id:guid}")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse<CourseDetailsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCourseCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { CourseId = id }, cancellationToken),
            "Course updated successfully.");

    /// <summary>Deletes a course, or unpublishes it when students already enrolled.</summary>
    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteCourseCommand(id), cancellationToken);
        return NoContentResponse();
    }

    /// <summary>Publishes the course. Requires a verified teacher, a description and content.</summary>
    [HttpPost("{id:guid}/publish")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new PublishCourseCommand(id), cancellationToken);
        return OkResponse("Course published successfully.");
    }

    /// <summary>Hides the course from the public catalogue.</summary>
    [HttpPost("{id:guid}/unpublish")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new UnpublishCourseCommand(id), cancellationToken);
        return OkResponse("Course unpublished successfully.");
    }

    // ---- Sections ---------------------------------------------------------

    /// <summary>Adds a section (chapter) to the course.</summary>
    [HttpPost("{id:guid}/sections")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse<CourseSectionDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddSection(
        Guid id,
        [FromBody] AddCourseSectionCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command with { CourseId = id }, cancellationToken),
            "Section added successfully.");

    /// <summary>Updates a section.</summary>
    [HttpPut("{id:guid}/sections/{sectionId:guid}")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse<CourseSectionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSection(
        Guid id,
        Guid sectionId,
        [FromBody] UpdateCourseSectionCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { CourseId = id, SectionId = sectionId }, cancellationToken),
            "Section updated successfully.");

    /// <summary>Deletes a section and its lessons.</summary>
    [HttpDelete("{id:guid}/sections/{sectionId:guid}")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSection(
        Guid id,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteCourseSectionCommand(id, sectionId), cancellationToken);
        return NoContentResponse();
    }

    // ---- Lessons ----------------------------------------------------------

    /// <summary>Adds a lesson to one of the course's sections.</summary>
    [HttpPost("{id:guid}/lessons")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse<CourseLessonDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddLesson(
        Guid id,
        [FromBody] AddCourseLessonCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command with { CourseId = id }, cancellationToken),
            "Lesson added successfully.");

    /// <summary>Updates a lesson.</summary>
    [HttpPut("{id:guid}/lessons/{lessonId:guid}")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse<CourseLessonDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateLesson(
        Guid id,
        Guid lessonId,
        [FromBody] UpdateCourseLessonCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { CourseId = id, LessonId = lessonId }, cancellationToken),
            "Lesson updated successfully.");

    /// <summary>Deletes a lesson.</summary>
    [HttpDelete("{id:guid}/lessons/{lessonId:guid}")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteLesson(
        Guid id,
        Guid lessonId,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteCourseLessonCommand(id, lessonId), cancellationToken);
        return NoContentResponse();
    }
}
