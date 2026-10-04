using Ustaz.Application.Features.Admin.Commands;
using Ustaz.Application.Features.Admin.Queries;
using Ustaz.Application.Features.Courses.Queries;
using Ustaz.Application.Features.Dashboard.Dtos;
using Ustaz.Application.Features.Discounts.Queries;
using Ustaz.Application.Features.Enrollments.Queries;
using Ustaz.Application.Features.Groups.Queries;
using Ustaz.Application.Features.Inquiries;
using Ustaz.Application.Features.Reviews.Commands;
using Ustaz.Application.Features.Reviews.Queries;
using Ustaz.Application.Features.Teachers.Queries;

namespace Ustaz.API.Controllers;

/// <summary>Platform administration. Every route requires the Admin role.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[ApiExplorerSettings(GroupName = "Admin")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminController : ApiControllerBase
{
    /// <summary>Platform-wide counters, revenue trend and the newest teachers.</summary>
    [HttpGet("dashboard")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default) =>
        OkResponse(await Mediator.Send(new GetAdminDashboardQuery { Months = months }, cancellationToken));

    /// <summary>Alias of the dashboard, kept for the analytics screen.</summary>
    [HttpGet("analytics")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default) =>
        OkResponse(await Mediator.Send(new GetAdminDashboardQuery { Months = months }, cancellationToken));

    /// <summary>Monthly platform revenue, suitable for export.</summary>
    [HttpGet("reports")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RevenuePointDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReports(
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default)
    {
        var dashboard = await Mediator.Send(new GetAdminDashboardQuery { Months = months }, cancellationToken);
        return OkResponse(dashboard.RevenueTrend);
    }

    // ---- Teachers ---------------------------------------------------------

    /// <summary>Lists teacher accounts, including unverified and suspended ones.</summary>
    [HttpGet("teachers")]
    [SwaggerOperation(Tags = ["Admin"])]
    public Task<IActionResult> GetTeachers(
        [FromQuery] GetAdminTeachersQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Gets any teacher's profile, verified or not.</summary>
    [HttpGet("teachers/{id:guid}")]
    [SwaggerOperation(Tags = ["Admin"])]
    public async Task<IActionResult> GetTeacher(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetTeacherByIdQuery(id), cancellationToken));

    /// <summary>Approves or rejects a teacher's verification request.</summary>
    [HttpPost("teachers/{id:guid}/verify")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyTeacher(
        Guid id,
        [FromBody] VerifyTeacherRequest? request,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(
            new VerifyTeacherCommand
            {
                TeacherId = id,
                Approve = request?.Approve ?? true,
                Notes = request?.Notes
            },
            cancellationToken);

        return OkResponse("Teacher verification updated.");
    }

    /// <summary>Moves a pending verification request into review.</summary>
    [HttpPost("teachers/{id:guid}/begin-review")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> BeginTeacherReview(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new BeginTeacherReviewCommand(id), cancellationToken);
        return OkResponse("Verification request moved into review.");
    }

    /// <summary>Suspends a teacher account and unpublishes their groups.</summary>
    [HttpPost("teachers/{id:guid}/suspend")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SuspendTeacher(
        Guid id,
        [FromBody] SuspendUserRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveTeacherUserIdAsync(id, cancellationToken);

        await Mediator.Send(
            new SetUserActiveStateCommand { UserId = userId, IsActive = false, Reason = request.Reason },
            cancellationToken);

        return OkResponse("Teacher account suspended.");
    }

    /// <summary>Reactivates a suspended teacher account.</summary>
    [HttpPost("teachers/{id:guid}/activate")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateTeacher(Guid id, CancellationToken cancellationToken)
    {
        var userId = await ResolveTeacherUserIdAsync(id, cancellationToken);

        await Mediator.Send(
            new SetUserActiveStateCommand { UserId = userId, IsActive = true },
            cancellationToken);

        return OkResponse("Teacher account activated.");
    }

    /// <summary>Features or unfeatures a verified teacher on the home page.</summary>
    [HttpPost("teachers/{id:guid}/feature")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> FeatureTeacher(
        Guid id,
        [FromQuery] bool featured = true,
        CancellationToken cancellationToken = default)
    {
        await Mediator.Send(new SetTeacherFeaturedCommand(id, featured), cancellationToken);
        return OkResponse(featured ? "Teacher featured." : "Teacher unfeatured.");
    }

    // ---- Everything else --------------------------------------------------

    /// <summary>Lists student accounts.</summary>
    [HttpGet("students")]
    [SwaggerOperation(Tags = ["Admin"])]
    public Task<IActionResult> GetStudents(
        [FromQuery] GetAdminStudentsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Lists every group, drafts included.</summary>
    [HttpGet("groups")]
    [SwaggerOperation(Tags = ["Admin"])]
    public Task<IActionResult> GetGroups(
        [FromQuery] GetGroupsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { IncludeUnpublished = true }, cancellationToken);

    /// <summary>Lists every course, drafts included.</summary>
    [HttpGet("courses")]
    [SwaggerOperation(Tags = ["Admin"])]
    public Task<IActionResult> GetCourses(
        [FromQuery] GetCoursesQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { IncludeUnpublished = true }, cancellationToken);

    /// <summary>Lists group enrollments across the platform.</summary>
    [HttpGet("enrollments")]
    [SwaggerOperation(Tags = ["Admin"])]
    public Task<IActionResult> GetEnrollments(
        [FromQuery] GetTeacherGroupEnrollmentsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Lists reviews, unapproved ones included, for moderation.</summary>
    [HttpGet("reviews")]
    [SwaggerOperation(Tags = ["Admin"])]
    public Task<IActionResult> GetReviews(
        [FromQuery] GetReviewsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { IncludeUnapproved = true }, cancellationToken);

    /// <summary>Approves or hides a review, then recalculates the affected ratings.</summary>
    [HttpPost("reviews/{id:guid}/moderate")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ModerateReview(
        Guid id,
        [FromBody] ModerateReviewCommand command,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(command with { ReviewId = id }, cancellationToken);
        return OkResponse("Review moderation applied.");
    }

    /// <summary>Lists inquiries across the platform.</summary>
    [HttpGet("inquiries")]
    [SwaggerOperation(Tags = ["Admin"])]
    public Task<IActionResult> GetInquiries(
        [FromQuery] GetTeacherInquiriesQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Lists discount codes across the platform.</summary>
    [HttpGet("discounts")]
    [SwaggerOperation(Tags = ["Admin"])]
    public Task<IActionResult> GetDiscounts(
        [FromQuery] GetTeacherDiscountsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Suspends or reactivates any account by its user id.</summary>
    [HttpPost("users/{userId:guid}/state")]
    [SwaggerOperation(Tags = ["Admin"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetUserState(
        Guid userId,
        [FromBody] SetUserActiveStateCommand command,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(command with { UserId = userId }, cancellationToken);
        return OkResponse(command.IsActive ? "Account activated." : "Account suspended.");
    }

    private async Task<Guid> ResolveTeacherUserIdAsync(Guid teacherId, CancellationToken cancellationToken)
    {
        var teacher = await Mediator.Send(new GetTeacherByIdQuery(teacherId), cancellationToken);
        return teacher.UserId;
    }
}

/// <summary>Body for POST /admin/teachers/{id}/verify.</summary>
public sealed record VerifyTeacherRequest
{
    public bool Approve { get; init; } = true;

    public string? Notes { get; init; }
}

/// <summary>Body for POST /admin/teachers/{id}/suspend.</summary>
public sealed record SuspendUserRequest
{
    public required string Reason { get; init; }
}
