using Ustaz.Application.Features.Announcements;
using Ustaz.Application.Features.Homework;

namespace Ustaz.API.Controllers;

/// <summary>Homework authoring and grading for the caller's workspace.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher/homework")]
[ApiExplorerSettings(GroupName = "Homework")]
[Authorize(Policy = PolicyNames.TenantMember)]
public sealed class TeacherHomeworkController : ApiControllerBase
{
    /// <summary>Creates a homework assignment as a draft.</summary>
    [HttpPost]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<HomeworkDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateHomeworkCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(await Mediator.Send(command, cancellationToken), "Homework created successfully.");

    /// <summary>Homework in the workspace.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<HomeworkDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetHomework(
        [FromQuery] GetHomeworkListQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>One homework assignment.</summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<HomeworkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetHomeworkByIdQuery(id), cancellationToken));

    /// <summary>Updates a homework assignment.</summary>
    [HttpPut("{id:guid}")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<HomeworkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateHomeworkCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { HomeworkId = id }, cancellationToken),
            "Homework updated successfully.");

    /// <summary>Deletes homework that has no submissions.</summary>
    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteHomeworkCommand(id), cancellationToken);
        return NoContentResponse();
    }

    /// <summary>Publishes the assignment and notifies the group's students.</summary>
    [HttpPost("{id:guid}/publish")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new PublishHomeworkCommand(id), cancellationToken);
        return OkResponse("Homework published successfully.");
    }

    /// <summary>
    /// Closes the assignment. Nothing further is accepted, whatever the deadline and the
    /// late-submission setting say. Students with outstanding work are told once.
    /// </summary>
    [HttpPost("{id:guid}/close")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<HomeworkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(new CloseHomeworkCommand(id), cancellationToken),
            "Homework closed.");

    /// <summary>Reopens a closed assignment so outstanding work can still be handed in.</summary>
    [HttpPost("{id:guid}/reopen")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<HomeworkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(new ReopenHomeworkCommand(id), cancellationToken),
            "Homework reopened.");

    /// <summary>
    /// Grades a batch of submissions for one assignment in a single transaction. Either every
    /// entry is applied or none is.
    /// </summary>
    [HttpPost("{id:guid}/submissions/grade")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<BulkGradeResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> BulkGrade(
        Guid id,
        [FromBody] BulkGradeHomeworkCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { HomeworkId = id }, cancellationToken),
            "Submissions graded successfully.");

    /// <summary>Submissions for one assignment.</summary>
    [HttpGet("{id:guid}/submissions")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<HomeworkSubmissionDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetSubmissions(
        Guid id,
        [FromQuery] GetHomeworkSubmissionsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { HomeworkId = id }, cancellationToken);

    /// <summary>One submission.</summary>
    [HttpGet("submissions/{submissionId:guid}")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<HomeworkSubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmission(Guid submissionId, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetHomeworkSubmissionQuery(submissionId), cancellationToken));

    /// <summary>Grades a submission and notifies the student.</summary>
    [HttpPut("submissions/{submissionId:guid}/grade")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<HomeworkSubmissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Grade(
        Guid submissionId,
        [FromBody] GradeHomeworkSubmissionCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { SubmissionId = submissionId }, cancellationToken),
            "Submission graded successfully.");

    /// <summary>
    /// Sends a submission back for revision with a reason. The student gets one more attempt
    /// whatever the assignment's resubmission policy says, and a closed assignment is reopened
    /// so they can act on it.
    /// </summary>
    [HttpPost("submissions/{submissionId:guid}/return")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<HomeworkSubmissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Return(
        Guid submissionId,
        [FromBody] ReturnHomeworkSubmissionCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { SubmissionId = submissionId }, cancellationToken),
            "Submission returned for revision.");
}

/// <summary>Announcements for the caller's workspace.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher/announcements")]
[ApiExplorerSettings(GroupName = "Announcements")]
[Authorize(Policy = PolicyNames.TenantMember)]
public sealed class TeacherAnnouncementsController : ApiControllerBase
{
    /// <summary>Creates an announcement as a draft.</summary>
    [HttpPost]
    [SwaggerOperation(Tags = ["Announcements"])]
    [ProducesResponseType(typeof(ApiResponse<AnnouncementDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAnnouncementCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(await Mediator.Send(command, cancellationToken), "Announcement created successfully.");

    /// <summary>Announcements in the workspace.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Announcements"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<AnnouncementDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetAnnouncements(
        [FromQuery] GetAnnouncementsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>One announcement.</summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Announcements"])]
    [ProducesResponseType(typeof(ApiResponse<AnnouncementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetAnnouncementByIdQuery(id), cancellationToken));

    /// <summary>Updates an announcement.</summary>
    [HttpPut("{id:guid}")]
    [SwaggerOperation(Tags = ["Announcements"])]
    [ProducesResponseType(typeof(ApiResponse<AnnouncementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAnnouncementCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { AnnouncementId = id }, cancellationToken),
            "Announcement updated successfully.");

    /// <summary>Deletes an announcement.</summary>
    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Tags = ["Announcements"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteAnnouncementCommand(id), cancellationToken);
        return NoContentResponse();
    }

    /// <summary>Publishes an announcement and notifies its audience.</summary>
    [HttpPost("{id:guid}/publish")]
    [SwaggerOperation(Tags = ["Announcements"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new PublishAnnouncementCommand(id), cancellationToken);
        return OkResponse("Announcement published successfully.");
    }
}
