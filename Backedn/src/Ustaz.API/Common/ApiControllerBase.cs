using MediatR;
using Microsoft.AspNetCore.Mvc;
using Ustaz.Application.Common.Models;

namespace Ustaz.API.Common;

/// <summary>
/// Thin base for every controller: it resolves MediatR lazily and wraps results in the
/// standard envelope. Controllers contain no business logic.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;

    protected ISender Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected IActionResult OkResponse<T>(T data, string? message = null) =>
        Ok(ApiResponse<T>.Ok(data, message));

    protected IActionResult OkResponse(string? message = null) =>
        Ok(ApiResponse.Ok(message));

    protected IActionResult CreatedResponse<T>(string actionName, object routeValues, T data, string? message = null) =>
        CreatedAtAction(actionName, routeValues, ApiResponse<T>.Ok(data, message ?? "Created successfully."));

    /// <summary>201 without a Location header, for resources that have no dedicated GET route.</summary>
    protected IActionResult CreatedResponse<T>(T data, string? message = null) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse<T>.Ok(data, message ?? "Created successfully."));

    protected IActionResult NoContentResponse() => NoContent();

    /// <summary>Sends a request and returns the paged payload inside the standard envelope.</summary>
    protected async Task<IActionResult> SendPagedAsync<T>(
        IRequest<PaginatedList<T>> request,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(request, cancellationToken));
}
