using Microsoft.AspNetCore.RateLimiting;
using Ustaz.API.Extensions;
using Ustaz.Application.Features.Messaging;
using Ustaz.Application.Features.Payments;

namespace Ustaz.API.Controllers;

/// <summary>
/// Internal messaging between teachers, assistants, students and guardians.
/// A user only ever sees conversations they participate in.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/messages")]
[ApiExplorerSettings(GroupName = "Messages")]
[Authorize]
public sealed class MessagesController : ApiControllerBase
{
    /// <summary>The caller's conversations, most recent first.</summary>
    [HttpGet("conversations")]
    [SwaggerOperation(Tags = ["Messages"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<ConversationDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetConversations(
        [FromQuery] GetConversationsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Unread message totals for the caller, for the client's badge.</summary>
    [HttpGet("unread-count")]
    [SwaggerOperation(Tags = ["Messages"])]
    [ProducesResponseType(typeof(ApiResponse<UnreadMessagesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetUnreadMessagesCountQuery(), cancellationToken));

    /// <summary>Starts a conversation. Every participant must belong to the workspace.</summary>
    [HttpPost("conversations")]
    [SwaggerOperation(Tags = ["Messages"])]
    [ProducesResponseType(typeof(ApiResponse<ConversationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> StartConversation(
        [FromBody] StartConversationCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(await Mediator.Send(command, cancellationToken), "Conversation started.");

    /// <summary>Posts a file into a conversation the caller participates in.</summary>
    [HttpPost("conversations/{id:guid}/attachments")]
    [EnableRateLimiting(ApiServiceExtensions.UploadRateLimitPolicy)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [SwaggerOperation(Tags = ["Messages"])]
    [ProducesResponseType(typeof(ApiResponse<MessageDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SendAttachment(
        Guid id,
        IFormFile file,
        [FromForm] string? body,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(
                new SendMessageAttachmentCommand
                {
                    ConversationId = id,
                    File = await file.ToUploadRequestAsync(cancellationToken),
                    Body = body
                },
                cancellationToken),
            "File sent.");

    /// <summary>
    /// Streams one attachment. Only a participant of the conversation can read it, and the
    /// response is always a download rather than something the browser will render.
    /// </summary>
    [HttpGet("attachments/{attachmentId:guid}")]
    [SwaggerOperation(Tags = ["Messages"])]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await Mediator.Send(
            new DownloadMessageAttachmentQuery(attachmentId), cancellationToken);

        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    /// <summary>Messages in one conversation. Non-participants get a 404.</summary>
    [HttpGet("conversations/{id:guid}")]
    [SwaggerOperation(Tags = ["Messages"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<MessageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetConversation(
        Guid id,
        [FromQuery] GetConversationQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { ConversationId = id }, cancellationToken);

    /// <summary>Posts a message into a conversation.</summary>
    [HttpPost("conversations/{id:guid}/messages")]
    [SwaggerOperation(Tags = ["Messages"])]
    [ProducesResponseType(typeof(ApiResponse<MessageDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(
        Guid id,
        [FromBody] SendMessageCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command with { ConversationId = id }, cancellationToken),
            "Message sent.");

    /// <summary>Marks a message, and everything before it, as read.</summary>
    [HttpPut("messages/{id:guid}/read")]
    [SwaggerOperation(Tags = ["Messages"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new MarkMessageReadCommand(id), cancellationToken);
        return OkResponse("Message marked as read.");
    }
}

/// <summary>
/// Tuition payments: what students and guardians pay the workspace.
/// Distinct from /billing, which is what the workspace pays USTAZ.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher/payments")]
[ApiExplorerSettings(GroupName = "Payments")]
[Authorize(Policy = PolicyNames.TenantMember)]
public sealed class TeacherPaymentsController : ApiControllerBase
{
    /// <summary>Tuition recorded in this workspace.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Payments"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<EducationalPaymentDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetPayments(
        [FromQuery] GetEducationalPaymentsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Records a tuition payment against a student.</summary>
    [HttpPost]
    [SwaggerOperation(Tags = ["Payments"])]
    [ProducesResponseType(typeof(ApiResponse<EducationalPaymentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordPayment(
        [FromBody] RecordEducationalPaymentCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(await Mediator.Send(command, cancellationToken), "Payment recorded successfully.");

    /// <summary>Marks a pending tuition payment as settled.</summary>
    [HttpPost("{id:guid}/mark-paid")]
    [SwaggerOperation(Tags = ["Payments"])]
    [ProducesResponseType(typeof(ApiResponse<EducationalPaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkPaid(Guid id, CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(new MarkEducationalPaymentPaidCommand(id), cancellationToken),
            "Payment marked as paid.");

    /// <summary>Refunds a settled tuition payment.</summary>
    [HttpPost("{id:guid}/refund")]
    [SwaggerOperation(Tags = ["Payments"])]
    [ProducesResponseType(typeof(ApiResponse<EducationalPaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Refund(
        Guid id,
        [FromBody] RefundPaymentRequest? request,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(new RefundEducationalPaymentCommand(id, request?.Reason), cancellationToken),
            "Payment refunded.");
}

/// <summary>Optional body for the refund endpoint.</summary>
public sealed record RefundPaymentRequest
{
    public string? Reason { get; init; }
}
