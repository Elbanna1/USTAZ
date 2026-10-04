using Ustaz.Application.Features.Discounts.Commands;
using Ustaz.Application.Features.Discounts.Dtos;
using Ustaz.Application.Features.Discounts.Queries;

namespace Ustaz.API.Controllers;

/// <summary>Public discount-code validation used at checkout.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/discounts")]
[ApiExplorerSettings(GroupName = "Discounts")]
[AllowAnonymous]
public sealed class DiscountsController : ApiControllerBase
{
    /// <summary>
    /// Validates a promo code against a group, a course, or an explicit teacher/amount pair.
    /// Never increments the usage counter; redemption happens during enrollment.
    /// </summary>
    [HttpPost("validate")]
    [SwaggerOperation(Tags = ["Discounts"])]
    [ProducesResponseType(typeof(ApiResponse<DiscountValidationResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Validate(
        [FromBody] ValidateDiscountQuery query,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(query, cancellationToken));
}

/// <summary>Discount management for the signed-in teacher.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher/discounts")]
[ApiExplorerSettings(GroupName = "Discounts")]
[Authorize(Policy = PolicyNames.TeacherOnly)]
public sealed class TeacherDiscountsController : ApiControllerBase
{
    /// <summary>Lists the calling teacher's promo codes.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Discounts"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<DiscountDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyDiscounts(
        [FromQuery] GetTeacherDiscountsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Gets one of the calling teacher's promo codes.</summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Discounts"])]
    [ProducesResponseType(typeof(ApiResponse<DiscountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetDiscountByIdQuery(id), cancellationToken));

    /// <summary>Creates a promo code. Codes are unique per teacher and stored upper-cased.</summary>
    [HttpPost]
    [SwaggerOperation(Tags = ["Discounts"])]
    [ProducesResponseType(typeof(ApiResponse<DiscountDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDiscountCommand command,
        CancellationToken cancellationToken)
    {
        var discount = await Mediator.Send(command, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = discount.Id, version = "1" }, discount, "Discount created successfully.");
    }

    /// <summary>Updates a promo code the caller owns.</summary>
    [HttpPut("{id:guid}")]
    [SwaggerOperation(Tags = ["Discounts"])]
    [ProducesResponseType(typeof(ApiResponse<DiscountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateDiscountCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { DiscountId = id }, cancellationToken),
            "Discount updated successfully.");

    /// <summary>Deletes a promo code, or deactivates it when it has already been redeemed.</summary>
    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Tags = ["Discounts"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteDiscountCommand(id), cancellationToken);
        return NoContentResponse();
    }

    /// <summary>Turns a promo code on.</summary>
    [HttpPost("{id:guid}/activate")]
    [SwaggerOperation(Tags = ["Discounts"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new ActivateDiscountCommand(id), cancellationToken);
        return OkResponse("Discount activated.");
    }

    /// <summary>Turns a promo code off.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [SwaggerOperation(Tags = ["Discounts"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeactivateDiscountCommand(id), cancellationToken);
        return OkResponse("Discount deactivated.");
    }
}
