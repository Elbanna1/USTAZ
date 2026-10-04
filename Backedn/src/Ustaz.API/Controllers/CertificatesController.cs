using Ustaz.Application.Features.Certificates;

namespace Ustaz.API.Controllers;

/// <summary>The signed-in student's completion certificates.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/student/certificates")]
[ApiExplorerSettings(GroupName = "Certificates")]
[Authorize(Policy = PolicyNames.StudentOnly)]
public sealed class StudentCertificatesController : ApiControllerBase
{
    /// <summary>Certificates the student has earned.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Certificates"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<CertificateDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetMine(
        [FromQuery] GetMyCertificatesQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>One certificate, including the code to hand to a verifier.</summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Certificates"])]
    [ProducesResponseType(typeof(ApiResponse<CertificateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetCertificateByIdQuery(id), cancellationToken));
}

/// <summary>Certificates issued by the caller's workspace.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher/certificates")]
[ApiExplorerSettings(GroupName = "Certificates")]
[Authorize(Policy = PolicyNames.TenantMember)]
public sealed class TeacherCertificatesController : ApiControllerBase
{
    /// <summary>Every certificate the workspace has issued.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Certificates"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<CertificateDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> GetAll(
        [FromQuery] GetWorkspaceCertificatesQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>One certificate issued by this workspace.</summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Certificates"])]
    [ProducesResponseType(typeof(ApiResponse<CertificateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetCertificateByIdQuery(id), cancellationToken));

    /// <summary>Withdraws a certificate. Verification then reports it as invalid.</summary>
    [HttpPost("{id:guid}/revoke")]
    [SwaggerOperation(Tags = ["Certificates"])]
    [ProducesResponseType(typeof(ApiResponse<CertificateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Revoke(
        Guid id,
        [FromBody] RevokeCertificateRequest? request,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(new RevokeCertificateCommand(id, request?.Reason), cancellationToken),
            "Certificate revoked.");
}

/// <summary>
/// Public certificate verification. Anonymous on purpose — an employer or a school checking a
/// certificate has no USTAZ account — but it answers only for an exact verification code and
/// returns nothing beyond what is printed on the certificate itself.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public/certificates")]
[ApiExplorerSettings(GroupName = "Certificates")]
[AllowAnonymous]
public sealed class PublicCertificatesController : ApiControllerBase
{
    /// <summary>Verifies a certificate by its verification code.</summary>
    [HttpGet("verify/{code}")]
    [SwaggerOperation(Tags = ["Certificates"])]
    [ProducesResponseType(typeof(ApiResponse<CertificateVerificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Verify(string code, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new VerifyCertificateQuery(code), cancellationToken));
}

/// <summary>Optional body for the revoke endpoint.</summary>
public sealed record RevokeCertificateRequest
{
    public string? Reason { get; init; }
}
