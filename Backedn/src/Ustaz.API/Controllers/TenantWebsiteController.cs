using Ustaz.Application.Features.Website;

namespace Ustaz.API.Controllers;

/// <summary>Website builder for the caller's workspace.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tenant/website")]
[ApiExplorerSettings(GroupName = "Website")]
[Authorize(Policy = PolicyNames.TenantMember)]
public sealed class TenantWebsiteController : ApiControllerBase
{
    /// <summary>The whole website configuration: hero, about, contact, SEO, sections.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Website"])]
    [ProducesResponseType(typeof(ApiResponse<TenantWebsiteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWebsite(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTenantWebsiteQuery(), cancellationToken));

    /// <summary>Updates the website. Requires Website.Manage and a plan with customisation.</summary>
    [HttpPut]
    [SwaggerOperation(Tags = ["Website"])]
    [ProducesResponseType(typeof(ApiResponse<TenantWebsiteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateWebsite(
        [FromBody] UpdateTenantWebsiteCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Website updated successfully.");

    /// <summary>Ordered list of website sections.</summary>
    [HttpGet("sections")]
    [SwaggerOperation(Tags = ["Website"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<WebsiteSectionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSections(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetWebsiteSectionsQuery(), cancellationToken));

    /// <summary>Adds a section to the website.</summary>
    [HttpPost("sections")]
    [SwaggerOperation(Tags = ["Website"])]
    [ProducesResponseType(typeof(ApiResponse<WebsiteSectionDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSection(
        [FromBody] CreateWebsiteSectionCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(await Mediator.Send(command, cancellationToken), "Section added successfully.");

    /// <summary>Updates one section.</summary>
    [HttpPut("sections/{id:guid}")]
    [SwaggerOperation(Tags = ["Website"])]
    [ProducesResponseType(typeof(ApiResponse<WebsiteSectionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSection(
        Guid id,
        [FromBody] UpdateWebsiteSectionCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { SectionId = id }, cancellationToken),
            "Section updated successfully.");

    /// <summary>Deletes one section.</summary>
    [HttpDelete("sections/{id:guid}")]
    [SwaggerOperation(Tags = ["Website"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSection(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteWebsiteSectionCommand(id), cancellationToken);
        return NoContentResponse();
    }

    /// <summary>Re-orders sections; the array position becomes the new sort order.</summary>
    [HttpPost("sections/reorder")]
    [SwaggerOperation(Tags = ["Website"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<WebsiteSectionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReorderSections(
        [FromBody] ReorderWebsiteSectionsCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(command, cancellationToken), "Sections reordered successfully.");
}

/// <summary>The anonymous, public face of a workspace, addressed by its slug.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public/tenants")]
[ApiExplorerSettings(GroupName = "Website")]
[AllowAnonymous]
public sealed class PublicTenantsController : ApiControllerBase
{
    /// <summary>The tenant's public profile, branding and published website.</summary>
    [HttpGet("{slug}")]
    [SwaggerOperation(Tags = ["Website"])]
    [ProducesResponseType(typeof(ApiResponse<PublicTenantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenant(string slug, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetPublicTenantQuery(slug), cancellationToken));

    /// <summary>Branding only, for a fast first paint.</summary>
    [HttpGet("{slug}/branding")]
    [SwaggerOperation(Tags = ["Website"])]
    public async Task<IActionResult> GetBranding(string slug, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetPublicTenantBrandingQuery(slug), cancellationToken));

    /// <summary>The tenant's published groups.</summary>
    [HttpGet("{slug}/groups")]
    [SwaggerOperation(Tags = ["Website"])]
    public Task<IActionResult> GetGroups(
        string slug,
        [FromQuery] GetPublicTenantGroupsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { Slug = slug }, cancellationToken);

    /// <summary>The tenant's published courses.</summary>
    [HttpGet("{slug}/courses")]
    [SwaggerOperation(Tags = ["Website"])]
    public Task<IActionResult> GetCourses(
        string slug,
        [FromQuery] GetPublicTenantCoursesQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { Slug = slug }, cancellationToken);

    /// <summary>The teachers who work in this workspace.</summary>
    [HttpGet("{slug}/teachers")]
    [SwaggerOperation(Tags = ["Website"])]
    public async Task<IActionResult> GetTeachers(string slug, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetPublicTenantTeachersQuery(slug), cancellationToken));

    /// <summary>SEO metadata and the canonical URL for the tenant's site.</summary>
    [HttpGet("{slug}/seo")]
    [SwaggerOperation(Tags = ["Website"])]
    [ProducesResponseType(typeof(ApiResponse<WebsiteSeoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSeo(string slug, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetPublicTenantSeoQuery(slug), cancellationToken));
}
