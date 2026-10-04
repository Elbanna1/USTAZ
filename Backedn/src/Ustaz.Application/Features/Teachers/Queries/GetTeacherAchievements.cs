using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Teachers.Queries;

public sealed record GetTeacherAchievementsQuery(Guid TeacherId) : IRequest<IReadOnlyList<TeacherAchievementDto>>;

public sealed class GetTeacherAchievementsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetTeacherAchievementsQuery, IReadOnlyList<TeacherAchievementDto>>
{
    public async Task<IReadOnlyList<TeacherAchievementDto>> Handle(
        GetTeacherAchievementsQuery query,
        CancellationToken cancellationToken)
    {
        if (!await context.TeacherProfiles.AnyAsync(teacher => teacher.Id == query.TeacherId, cancellationToken))
        {
            throw new NotFoundException("Teacher", query.TeacherId);
        }

        return await context.TeacherAchievements
            .AsNoTracking()
            .Where(achievement => achievement.TeacherId == query.TeacherId)
            .OrderBy(achievement => achievement.SortOrder)
            .Select(achievement => new TeacherAchievementDto
            {
                Id = achievement.Id,
                Title = achievement.Title,
                Description = achievement.Description,
                Year = achievement.Year,
                ImageUrl = achievement.ImageUrl,
                SortOrder = achievement.SortOrder
            })
            .ToListAsync(cancellationToken);
    }
}
