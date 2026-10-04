using Microsoft.AspNetCore.RateLimiting;
using Ustaz.API.Extensions;
using Ustaz.Application.Features.Tenants;
using Ustaz.Application.Features.Tenants.Dtos;

namespace Ustaz.API.Controllers;

/// <summary>
/// The caller's workspace: identity, settings, branding and custom domains.
/// Every action re-resolves workspace permissions from the database.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tenant")]
[ApiExplorerSettings(GroupName = "Tenant")]
[Authorize(Policy = PolicyNames.TenantMember)]
public sealed class TenantController : ApiControllerBase
{
    /// <summary>The workspace the caller is acting inside.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Tenant"])]
    [ProducesResponseType(typeof(ApiResponse<TenantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTenant(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTenantQuery(), cancellationToken));

    /// <summary>Updates the workspace name, slug and contact details. Requires Tenant.Manage.</summary>
    [HttpPut]
    [SwaggerOperation(Tags = ["Tenant"])]
    [ProducesResponseType(typeof(ApiResponse<TenantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTenant(
        [FromBody] UpdateTenantCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Workspace updated successfully.");

    /// <summary>Operational settings, including the parent notification thresholds.</summary>
    [HttpGet("settings")]
    [SwaggerOperation(Tags = ["Tenant"])]
    [ProducesResponseType(typeof(ApiResponse<TenantSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTenantSettingsQuery(), cancellationToken));

    /// <summary>Updates operational settings. Requires Tenant.Manage.</summary>
    [HttpPut("settings")]
    [SwaggerOperation(Tags = ["Tenant"])]
    [ProducesResponseType(typeof(ApiResponse<TenantSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateTenantSettingsCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Settings updated successfully.");

    /// <summary>Colours, fonts and imagery for the workspace and its public site.</summary>
    [HttpGet("branding")]
    [SwaggerOperation(Tags = ["Tenant"])]
    [ProducesResponseType(typeof(ApiResponse<TenantBrandingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBranding(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTenantBrandingQuery(), cancellationToken));

    /// <summary>Updates branding. Requires Website.Manage.</summary>
    [HttpPut("branding")]
    [SwaggerOperation(Tags = ["Tenant"])]
    [ProducesResponseType(typeof(ApiResponse<TenantBrandingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateBranding(
        [FromBody] UpdateTenantBrandingCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Branding updated successfully.");

    /// <summary>Uploads the workspace logo.</summary>
    [HttpPost("logo")]
    [EnableRateLimiting(ApiServiceExtensions.UploadRateLimitPolicy)]
    [SwaggerOperation(Tags = ["Tenant"])]
    [ProducesResponseType(typeof(ApiResponse<MediaUploadResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadLogo(IFormFile file, CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(
                new UploadTenantImageCommand { File = await file.ToUploadRequestAsync(cancellationToken), Kind = TenantImageKind.Logo },
                cancellationToken),
            "Logo updated successfully.");

    /// <summary>Uploads the workspace cover image.</summary>
    [HttpPost("cover")]
    [EnableRateLimiting(ApiServiceExtensions.UploadRateLimitPolicy)]
    [SwaggerOperation(Tags = ["Tenant"])]
    [ProducesResponseType(typeof(ApiResponse<MediaUploadResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadCover(IFormFile file, CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(
                new UploadTenantImageCommand { File = await file.ToUploadRequestAsync(cancellationToken), Kind = TenantImageKind.Cover },
                cancellationToken),
            "Cover image updated successfully.");

    // ---- Custom domains --------------------------------------------------

    /// <summary>Custom domains pointed at this workspace's public site.</summary>
    [HttpGet("domains")]
    [SwaggerOperation(Tags = ["Tenant"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TenantDomainDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDomains(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTenantDomainsQuery(), cancellationToken));

    /// <summary>Registers a custom domain. Requires a plan that includes custom domains.</summary>
    [HttpPost("domains")]
    [SwaggerOperation(Tags = ["Tenant"])]
    [ProducesResponseType(typeof(ApiResponse<TenantDomainDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddDomain(
        [FromBody] AddTenantDomainCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command, cancellationToken),
            "Domain registered. Publish the DNS TXT record, then verify it.");

    /// <summary>Updates a domain, e.g. promoting a verified one to primary.</summary>
    [HttpPut("domains/{id:guid}")]
    [SwaggerOperation(Tags = ["Tenant"])]
    [ProducesResponseType(typeof(ApiResponse<TenantDomainDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateDomain(
        Guid id,
        [FromBody] UpdateTenantDomainCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { DomainId = id }, cancellationToken),
            "Domain updated successfully.");

    /// <summary>Checks the DNS TXT record and marks the domain verified when it matches.</summary>
    [HttpPost("domains/{id:guid}/verify")]
    [SwaggerOperation(Tags = ["Tenant"])]
    [ProducesResponseType(typeof(ApiResponse<TenantDomainDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyDomain(Guid id, CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(new VerifyTenantDomainCommand(id), cancellationToken),
            "Domain verification attempted.");

    /// <summary>Removes a custom domain.</summary>
    [HttpDelete("domains/{id:guid}")]
    [SwaggerOperation(Tags = ["Tenant"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteDomain(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteTenantDomainCommand(id), cancellationToken);
        return NoContentResponse();
    }
}
