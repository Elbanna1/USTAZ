using Ustaz.Application.Features.Team;
using Ustaz.Application.Features.Team.Dtos;
using Ustaz.Domain.Enums;

namespace Ustaz.API.Controllers;

/// <summary>
/// Workspace team management. Every mutation is checked against the caller's live
/// permissions, and nobody can act on their own membership row.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tenant/team")]
[ApiExplorerSettings(GroupName = "Team")]
[Authorize(Policy = PolicyNames.TenantMember)]
public sealed class TeamController : ApiControllerBase
{
    /// <summary>Members of the caller's workspace.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Team"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<TenantMemberDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetMembers(
        [FromQuery] GetTeamMembersQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>One member, with their effective and explicit permissions.</summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Team"])]
    [ProducesResponseType(typeof(ApiResponse<TenantMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMember(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTeamMemberQuery(id), cancellationToken));

    /// <summary>Invites someone to the workspace. Requires Team.Manage and an available seat.</summary>
    [HttpPost("invite")]
    [SwaggerOperation(Tags = ["Team"])]
    [ProducesResponseType(typeof(ApiResponse<TenantInvitationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Invite(
        [FromBody] InviteTeamMemberCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(await Mediator.Send(command, cancellationToken), "Invitation sent successfully.");

    /// <summary>Changes a member's workspace role. Ownership can never be granted here.</summary>
    [HttpPut("{id:guid}/role")]
    [SwaggerOperation(Tags = ["Team"])]
    [ProducesResponseType(typeof(ApiResponse<TenantMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ChangeRole(
        Guid id,
        [FromBody] ChangeMemberRoleCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { MemberId = id }, cancellationToken),
            "Member role updated successfully.");

    /// <summary>Activates or deactivates a member.</summary>
    [HttpPut("{id:guid}/status")]
    [SwaggerOperation(Tags = ["Team"])]
    [ProducesResponseType(typeof(ApiResponse<TenantMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        [FromBody] ChangeMemberStatusCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { MemberId = id }, cancellationToken),
            "Member status updated successfully.");

    /// <summary>Removes a member and revokes their permissions.</summary>
    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Tags = ["Team"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new RemoveTeamMemberCommand(id), cancellationToken);
        return NoContentResponse();
    }
}

/// <summary>Accepting or declining a workspace invitation.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/team/invitations")]
[ApiExplorerSettings(GroupName = "Team")]
public sealed class TeamInvitationsController : ApiControllerBase
{
    /// <summary>Accepts an invitation. The signed-in account's email must match the invite.</summary>
    [HttpPost("accept")]
    [Authorize]
    [SwaggerOperation(Tags = ["Team"])]
    [ProducesResponseType(typeof(ApiResponse<TenantMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Accept(
        [FromBody] AcceptInvitationCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command, cancellationToken),
            "You have joined the workspace. Sign in again to refresh your access token.");

    /// <summary>Declines an invitation.</summary>
    [HttpPost("decline")]
    [AllowAnonymous]
    [SwaggerOperation(Tags = ["Team"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Decline(
        [FromBody] DeclineInvitationCommand command,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(command, cancellationToken);
        return OkResponse("Invitation declined.");
    }
}

/// <summary>
/// Assistant management. An assistant is a workspace member whose role is Assistant; this
/// controller is the focused surface for granting and revoking their capabilities.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tenant/assistants")]
[ApiExplorerSettings(GroupName = "Assistants")]
[Authorize(Policy = PolicyNames.TenantMember)]
public sealed class AssistantsController : ApiControllerBase
{
    /// <summary>Assistants in the caller's workspace.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Assistants"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<TenantMemberDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetAssistants(
        [FromQuery] GetTeamMembersQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { AssistantsOnly = true }, cancellationToken);

    /// <summary>The permissions an assistant may be granted, grouped for the editor.</summary>
    [HttpGet("permissions/catalog")]
    [SwaggerOperation(Tags = ["Assistants"])]
    [ProducesResponseType(typeof(ApiResponse<PermissionCatalogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPermissionCatalog(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetPermissionCatalogQuery(), cancellationToken));

    /// <summary>One assistant, with their effective permissions.</summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Assistants"])]
    [ProducesResponseType(typeof(ApiResponse<TenantMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssistant(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTeamMemberQuery(id), cancellationToken));

    /// <summary>
    /// Invites an assistant with an initial permission set. Assistants start with no
    /// capabilities, so each one must be granted deliberately.
    /// </summary>
    [HttpPost("invite")]
    [SwaggerOperation(Tags = ["Assistants"])]
    [ProducesResponseType(typeof(ApiResponse<TenantInvitationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> InviteAssistant(
        [FromBody] InviteTeamMemberCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command with { Role = TenantMemberRole.Assistant }, cancellationToken),
            "Assistant invitation sent successfully.");

    /// <summary>Updates an assistant's title or role.</summary>
    [HttpPut("{id:guid}")]
    [SwaggerOperation(Tags = ["Assistants"])]
    [ProducesResponseType(typeof(ApiResponse<TenantMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAssistant(
        Guid id,
        [FromBody] ChangeMemberRoleCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { MemberId = id }, cancellationToken),
            "Assistant updated successfully.");

    /// <summary>
    /// Replaces an assistant's permissions. Unknown permissions and owner-only capabilities
    /// are rejected, and nobody can edit their own permissions.
    /// </summary>
    [HttpPut("{id:guid}/permissions")]
    [SwaggerOperation(Tags = ["Assistants"])]
    [ProducesResponseType(typeof(ApiResponse<TenantMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdatePermissions(
        Guid id,
        [FromBody] UpdateMemberPermissionsCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { MemberId = id }, cancellationToken),
            "Permissions updated successfully.");

    /// <summary>Activates or deactivates an assistant.</summary>
    [HttpPut("{id:guid}/status")]
    [SwaggerOperation(Tags = ["Assistants"])]
    [ProducesResponseType(typeof(ApiResponse<TenantMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] ChangeMemberStatusCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { MemberId = id }, cancellationToken),
            "Assistant status updated successfully.");

    /// <summary>Removes an assistant from the workspace.</summary>
    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Tags = ["Assistants"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveAssistant(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new RemoveTeamMemberCommand(id), cancellationToken);
        return NoContentResponse();
    }
}

/// <summary>Accepting an assistant invitation.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/assistants/invitations")]
[ApiExplorerSettings(GroupName = "Assistants")]
[Authorize]
public sealed class AssistantInvitationsController : ApiControllerBase
{
    /// <summary>Accepts an assistant invitation and applies the granted permissions.</summary>
    [HttpPost("accept")]
    [SwaggerOperation(Tags = ["Assistants"])]
    [ProducesResponseType(typeof(ApiResponse<TenantMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept(
        [FromBody] AcceptInvitationCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command, cancellationToken),
            "You have joined the workspace as an assistant. Sign in again to refresh your token.");
}
