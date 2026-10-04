using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;

namespace Ustaz.Application.Features.Reviews.Services;

/// <summary>
/// Recomputes the denormalised rating snapshots after a review is created, edited,
/// moderated or deleted. Aggregation happens on the database side.
/// </summary>
public interface IRatingRecalculator
{
    Task RecalculateAsync(Guid teacherId, Guid? groupId, Guid? courseId, CancellationToken cancellationToken = default);
}

public sealed class RatingRecalculator(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IRatingRecalculator
{
    public async Task RecalculateAsync(
        Guid teacherId,
        Guid? groupId,
        Guid? courseId,
        CancellationToken cancellationToken = default)
    {
        var utcNow = dateTimeProvider.UtcNow;

        await RecalculateTeacherAsync(teacherId, utcNow, cancellationToken);

        if (groupId.HasValue)
        {
            await RecalculateGroupAsync(groupId.Value, utcNow, cancellationToken);
        }

        if (courseId.HasValue)
        {
            await RecalculateCourseAsync(courseId.Value, utcNow, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task RecalculateTeacherAsync(Guid teacherId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var teacher = await context.TeacherProfiles
            .FirstOrDefaultAsync(profile => profile.Id == teacherId, cancellationToken);

        if (teacher is null)
        {
            return;
        }

        var stats = await context.Reviews
            .AsNoTracking()
            .Where(review => review.TeacherId == teacherId && review.IsApproved)
            .GroupBy(review => 1)
            .Select(group => new { Average = group.Average(review => (decimal)review.Rating), Count = group.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        teacher.ApplyRatingSnapshot(stats?.Average ?? 0m, stats?.Count ?? 0, utcNow);
    }

    private async Task RecalculateGroupAsync(Guid groupId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var group = await context.Groups.FirstOrDefaultAsync(entity => entity.Id == groupId, cancellationToken);

        if (group is null)
        {
            return;
        }

        var stats = await context.Reviews
            .AsNoTracking()
            .Where(review => review.GroupId == groupId && review.IsApproved)
            .GroupBy(review => 1)
            .Select(g => new { Average = g.Average(review => (decimal)review.Rating), Count = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        group.ApplyRatingSnapshot(stats?.Average ?? 0m, stats?.Count ?? 0, utcNow);
    }

    private async Task RecalculateCourseAsync(Guid courseId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var course = await context.Courses.FirstOrDefaultAsync(entity => entity.Id == courseId, cancellationToken);

        if (course is null)
        {
            return;
        }

        var stats = await context.Reviews
            .AsNoTracking()
            .Where(review => review.CourseId == courseId && review.IsApproved)
            .GroupBy(review => 1)
            .Select(g => new { Average = g.Average(review => (decimal)review.Rating), Count = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        course.ApplyRatingSnapshot(stats?.Average ?? 0m, stats?.Count ?? 0, utcNow);
    }
}
