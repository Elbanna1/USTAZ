using Ustaz.Application.Features.Inquiries;

namespace Ustaz.API.Controllers;

/// <summary>Lead capture. Guests may submit an inquiry without an account.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/inquiries")]
[ApiExplorerSettings(GroupName = "Inquiries")]
public sealed class InquiriesController : ApiControllerBase
{
    /// <summary>Sends an inquiry to a teacher, optionally about a specific group or course.</summary>
    [HttpPost]
    [AllowAnonymous]
    [SwaggerOperation(Tags = ["Inquiries"])]
    [ProducesResponseType(typeof(ApiResponse<InquiryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateInquiryCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command, cancellationToken),
            "Your inquiry has been sent. The teacher will contact you shortly.");

    /// <summary>Gets one inquiry (its sender, the receiving teacher, or an admin).</summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [SwaggerOperation(Tags = ["Inquiries"])]
    [ProducesResponseType(typeof(ApiResponse<InquiryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetInquiryByIdQuery(id), cancellationToken));
}

/// <summary>Inquiry inbox for the signed-in teacher.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher/inquiries")]
[ApiExplorerSettings(GroupName = "Inquiries")]
[Authorize(Policy = PolicyNames.TeacherOnly)]
public sealed class TeacherInquiriesController : ApiControllerBase
{
    /// <summary>Lists inquiries addressed to the calling teacher, with ready-to-use WhatsApp links.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Inquiries"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<InquiryDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyInquiries(
        [FromQuery] GetTeacherInquiriesQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Gets one inquiry addressed to the calling teacher.</summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Inquiries"])]
    [ProducesResponseType(typeof(ApiResponse<InquiryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetInquiryByIdQuery(id), cancellationToken));

    /// <summary>Moves an inquiry through the New → Contacted → Converted/Closed pipeline.</summary>
    [HttpPut("{id:guid}/status")]
    [SwaggerOperation(Tags = ["Inquiries"])]
    [ProducesResponseType(typeof(ApiResponse<InquiryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateInquiryStatusCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { InquiryId = id }, cancellationToken),
            "Inquiry status updated.");
}

/// <summary>The caller's in-app notification inbox.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[ApiExplorerSettings(GroupName = "Notifications")]
[Authorize]
public sealed class NotificationsController : ApiControllerBase
{
    /// <summary>Lists the caller's notifications.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Notifications"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<Application.Features.Notifications.NotificationDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetAll(
        [FromQuery] Application.Features.Notifications.GetNotificationsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Returns the unread badge count.</summary>
    [HttpGet("unread-count")]
    [SwaggerOperation(Tags = ["Notifications"])]
    [ProducesResponseType(typeof(ApiResponse<Application.Features.Notifications.UnreadCountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(
            new Application.Features.Notifications.GetUnreadNotificationCountQuery(), cancellationToken));

    /// <summary>Marks one notification as read.</summary>
    [HttpPut("{id:guid}/read")]
    [SwaggerOperation(Tags = ["Notifications"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new Application.Features.Notifications.MarkNotificationReadCommand(id), cancellationToken);
        return OkResponse("Notification marked as read.");
    }

    /// <summary>Marks every notification as read.</summary>
    [HttpPut("read-all")]
    [SwaggerOperation(Tags = ["Notifications"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await Mediator.Send(new Application.Features.Notifications.MarkAllNotificationsReadCommand(), cancellationToken);
        return OkResponse("All notifications marked as read.");
    }

    /// <summary>One notification, for a client opening a deep link straight onto it.</summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Notifications"])]
    [ProducesResponseType(typeof(ApiResponse<Application.Features.Notifications.NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(
            new Application.Features.Notifications.GetNotificationByIdQuery(id), cancellationToken));

    /// <summary>Clears the notifications the caller has already read.</summary>
    [HttpDelete("read")]
    [SwaggerOperation(Tags = ["Notifications"])]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearRead(CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(
                new Application.Features.Notifications.ClearReadNotificationsCommand(), cancellationToken),
            "Read notifications cleared.");

    /// <summary>Deletes one notification.</summary>
    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Tags = ["Notifications"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new Application.Features.Notifications.DeleteNotificationCommand(id), cancellationToken);
        return NoContentResponse();
    }

    /// <summary>
    /// The caller's per-category delivery preferences. Every category is returned, with its
    /// default applied, so the client never has to infer a missing row.
    /// </summary>
    [HttpGet("preferences")]
    [SwaggerOperation(Tags = ["Notifications"])]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<Application.Features.Notifications.NotificationPreferenceDto>>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(
            new Application.Features.Notifications.GetNotificationPreferencesQuery(), cancellationToken));

    /// <summary>
    /// Updates which channels each category is delivered on. Guardians use this to choose,
    /// for example, in-app plus email for attendance but in-app only for grades.
    /// </summary>
    [HttpPut("preferences")]
    [SwaggerOperation(Tags = ["Notifications"])]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<Application.Features.Notifications.NotificationPreferenceDto>>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] Application.Features.Notifications.UpdateNotificationPreferencesCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Notification preferences updated.");
}
