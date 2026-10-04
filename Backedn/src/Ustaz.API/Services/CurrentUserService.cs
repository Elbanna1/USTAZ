using System.Security.Claims;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Exceptions;

namespace Ustaz.API.Services;

/// <summary>
/// Reads the caller's identity from the validated JWT. Nothing here trusts request bodies
/// or headers other than the bearer token itself.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid? UserId => ParseGuid(
        Principal?.FindFirstValue(CustomClaimTypes.UserId)
        ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier));

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email)
        ?? Principal?.FindFirstValue("email");

    public Guid? TeacherId => ParseGuid(Principal?.FindFirstValue(CustomClaimTypes.TeacherId));

    public Guid? StudentId => ParseGuid(Principal?.FindFirstValue(CustomClaimTypes.StudentId));

    public Guid? ParentId => ParseGuid(Principal?.FindFirstValue(CustomClaimTypes.ParentId));

    public Guid? TenantId => ParseGuid(Principal?.FindFirstValue(CustomClaimTypes.TenantId));

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray() ?? [];

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsAdmin => IsInRole(RoleNames.Admin);

    public bool IsTeacher => IsInRole(RoleNames.Teacher);

    public bool IsStudent => IsInRole(RoleNames.Student);

    public bool IsParent => IsInRole(RoleNames.Parent);

    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    public Guid RequireUserId() =>
        UserId ?? throw new UnauthorizedException("You must be signed in to perform this action.");

    public Guid RequireTeacherId() =>
        TeacherId ?? throw new ForbiddenAccessException("This action requires a teacher account.");

    public Guid RequireStudentId() =>
        StudentId ?? throw new ForbiddenAccessException("This action requires a student account.");

    public Guid RequireParentId() =>
        ParentId ?? throw new ForbiddenAccessException("This action requires a parent account.");

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed : null;
}
