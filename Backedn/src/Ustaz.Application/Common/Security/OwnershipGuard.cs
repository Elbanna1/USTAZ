using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Common.Security;

/// <summary>
/// Resource ownership checks shared by every teacher/student scoped handler.
/// Admins bypass ownership but never bypass existence checks.
/// </summary>
public static class OwnershipGuard
{
    /// <summary>Ensures the caller owns the teacher scoped resource, or is an administrator.</summary>
    public static void EnsureTeacherOwns(ICurrentUserService currentUser, Guid resourceTeacherId)
    {
        if (currentUser.IsAdmin)
        {
            return;
        }

        if (currentUser.TeacherId is null || currentUser.TeacherId.Value != resourceTeacherId)
        {
            throw new ForbiddenAccessException("You can only manage resources that belong to your own teacher account.");
        }
    }

    /// <summary>Ensures the caller owns the student scoped resource, or is an administrator.</summary>
    public static void EnsureStudentOwns(ICurrentUserService currentUser, Guid resourceStudentId)
    {
        if (currentUser.IsAdmin)
        {
            return;
        }

        if (currentUser.StudentId is null || currentUser.StudentId.Value != resourceStudentId)
        {
            throw new ForbiddenAccessException("You can only access your own data.");
        }
    }

    /// <summary>Ensures the caller owns the user scoped resource (notifications, account), or is an administrator.</summary>
    public static void EnsureUserOwns(ICurrentUserService currentUser, Guid resourceUserId)
    {
        if (currentUser.IsAdmin)
        {
            return;
        }

        if (currentUser.UserId is null || currentUser.UserId.Value != resourceUserId)
        {
            throw new ForbiddenAccessException("You can only access your own data.");
        }
    }
}
