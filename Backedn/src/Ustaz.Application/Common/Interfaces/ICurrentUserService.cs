namespace Ustaz.Application.Common.Interfaces;

/// <summary>Ambient information about the caller, resolved from the JWT by the API layer.</summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }

    string? Email { get; }

    /// <summary>Teacher profile id when the caller holds the Teacher role.</summary>
    Guid? TeacherId { get; }

    /// <summary>Student profile id when the caller holds the Student role.</summary>
    Guid? StudentId { get; }

    /// <summary>Parent profile id when the caller holds the Parent role.</summary>
    Guid? ParentId { get; }

    /// <summary>Workspace carried by the token, if any. Never trusted on its own.</summary>
    Guid? TenantId { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsAuthenticated { get; }

    bool IsAdmin { get; }

    bool IsTeacher { get; }

    bool IsStudent { get; }

    bool IsParent { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    bool IsInRole(string role);

    /// <summary>Returns the caller's user id or throws when the request is anonymous.</summary>
    Guid RequireUserId();

    /// <summary>Returns the caller's teacher profile id or throws when the caller is not a teacher.</summary>
    Guid RequireTeacherId();

    /// <summary>Returns the caller's student profile id or throws when the caller is not a student.</summary>
    Guid RequireStudentId();

    /// <summary>Returns the caller's parent profile id or throws when the caller is not a parent.</summary>
    Guid RequireParentId();
}
