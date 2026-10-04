using Ustaz.Application.Features.Admin.Commands;
using Ustaz.Application.Features.Analytics;
using Ustaz.Application.Features.Admin.Queries;
using Ustaz.Application.Features.Attendance;
using Ustaz.Application.Features.Attendance.Dtos;
using Ustaz.Application.Features.Billing;
using Ustaz.Application.Features.Billing.Dtos;
using Ustaz.Application.Features.Dashboard.Dtos;
using Ustaz.Application.Features.Dashboard.Queries;

namespace Ustaz.API.Controllers;

/// <summary>
/// Platform administration of the SaaS layer: workspaces, subscriptions and audit.
/// This is platform-level authority and is never exposed to tenant members.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[ApiExplorerSettings(GroupName = "Admin")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminSaasController : ApiControllerBase
{
    /// <summary>Every workspace on the platform, with its plan and lifetime revenue.</summary>
    [HttpGet("tenants")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<AdminTenantDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetTenants(
        [FromQuery] GetAdminTenantsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Suspends a workspace. Its members immediately lose access.</summary>
    [HttpPost("tenants/{id:guid}/suspend")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SuspendTenant(
        Guid id,
        [FromBody] SuspendUserRequest request,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(
            new SetTenantStatusCommand { TenantId = id, IsActive = false, Reason = request.Reason },
            cancellationToken);

        return OkResponse("Workspace suspended.");
    }

    /// <summary>Reactivates a suspended workspace.</summary>
    [HttpPost("tenants/{id:guid}/activate")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateTenant(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new SetTenantStatusCommand { TenantId = id, IsActive = true }, cancellationToken);
        return OkResponse("Workspace activated.");
    }

    /// <summary>All subscription plans, inactive ones included.</summary>
    [HttpGet("plans")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriptionPlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(
            new GetSubscriptionPlansQuery { IncludeInactive = true }, cancellationToken));

    /// <summary>Monthly SaaS revenue: what tenants paid USTAZ.</summary>
    [HttpGet("analytics/revenue")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RevenuePointDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlatformRevenue(
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default) =>
        OkResponse(await Mediator.Send(new GetPlatformRevenueQuery { Months = months }, cancellationToken));

    /// <summary>Workspace growth and subscription mix.</summary>
    [HttpGet("analytics/tenants")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<AdminTenantDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetTenantAnalytics(
        [FromQuery] GetAdminTenantsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>The platform-wide audit trail.</summary>
    [HttpGet("audit-logs")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<AuditLogDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetAuditLogs(
        [FromQuery] GetAuditLogsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);
}

/// <summary>Audit trail scoped to the caller's own workspace. Owner only.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tenant/audit-logs")]
[ApiExplorerSettings(GroupName = "Tenant")]
[Authorize(Policy = PolicyNames.TenantMember)]
public sealed class TenantAuditController : ApiControllerBase
{
    /// <summary>Sensitive actions taken inside this workspace.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Tenant"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<AuditLogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> GetAuditLogs(
        [FromQuery] GetAuditLogsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);
}

/// <summary>Attendance and grade analytics for the caller's workspace.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher/analytics")]
[ApiExplorerSettings(GroupName = "Dashboard")]
[Authorize(Policy = PolicyNames.TenantMember)]
public sealed class TeacherEducationAnalyticsController : ApiControllerBase
{
    /// <summary>Attendance rates for the workspace, broken down by group.</summary>
    [HttpGet("attendance")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<AttendanceStatisticsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttendanceAnalytics(
        [FromQuery] GetAttendanceStatisticsQuery query,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(query, cancellationToken));

    /// <summary>Exam performance across the workspace.</summary>
    [HttpGet("grades")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<GradeAnalyticsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGradeAnalytics(
        [FromQuery] GetGradeAnalyticsQuery query,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(query, cancellationToken));

    /// <summary>
    /// Students who need attention, worst first, with the numbers behind every flag. The
    /// scoring is deterministic: attendance, homework, exams, progress and recency.
    /// </summary>
    [HttpGet("at-risk")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AtRiskStudentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAtRiskStudents(
        [FromQuery] GetAtRiskStudentsQuery query,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(query, cancellationToken));

    /// <summary>One student's standing, with the same explainable breakdown.</summary>
    [HttpGet("at-risk/{studentId:guid}")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<AtRiskStudentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentRisk(Guid studentId, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetStudentRiskQuery(studentId), cancellationToken));

    /// <summary>Course performance across the workspace.</summary>
    [HttpGet("courses")]
    [SwaggerOperation(Tags = ["Dashboard"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CoursePerformanceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourseAnalytics(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetCourseAnalyticsQuery(), cancellationToken));
}
