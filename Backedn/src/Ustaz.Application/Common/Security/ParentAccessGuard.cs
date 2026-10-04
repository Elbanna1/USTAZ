using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Common.Security;

/// <summary>
/// The one place a parent's access to a child is decided. Every parent-facing query routes
/// through here, so a guardian can never reach a student who is not confirmed as their child
/// by changing a route parameter.
/// </summary>
public interface IParentAccessGuard
{
    /// <summary>
    /// Verifies the calling parent has a confirmed link to the student. Platform admins pass.
    /// Throws <see cref="NotFoundException"/> rather than Forbidden, so the endpoint does not
    /// confirm that an unrelated student exists.
    /// </summary>
    Task EnsureLinkedAsync(Guid studentId, CancellationToken cancellationToken = default);

    /// <summary>Ids of every child the calling parent may see.</summary>
    Task<IReadOnlyList<Guid>> LinkedStudentIdsAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class ParentAccessGuard(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IParentAccessGuard
{
    public async Task EnsureLinkedAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        if (currentUser.IsAdmin)
        {
            return;
        }

        var parentId = currentUser.RequireParentId();

        var isLinked = await context.ParentStudentLinks
            .AsNoTracking()
            .AnyAsync(
                link => link.ParentId == parentId && link.StudentId == studentId && link.IsConfirmed,
                cancellationToken);

        if (!isLinked)
        {
            throw new NotFoundException("No child with this identifier is linked to your account.");
        }
    }

    public async Task<IReadOnlyList<Guid>> LinkedStudentIdsAsync(CancellationToken cancellationToken = default)
    {
        var parentId = currentUser.RequireParentId();

        return await context.ParentStudentLinks
            .AsNoTracking()
            .Where(link => link.ParentId == parentId && link.IsConfirmed)
            .Select(link => link.StudentId)
            .ToListAsync(cancellationToken);
    }
}
