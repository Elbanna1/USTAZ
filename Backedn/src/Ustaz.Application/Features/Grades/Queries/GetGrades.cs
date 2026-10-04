using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Grades.Queries;

public sealed record GetGradesQuery : IRequest<IReadOnlyList<GradeDto>>, ICacheableQuery
{
    public bool IncludeInactive { get; init; }

    public string CacheKey => CacheKeys.GradesAll + (IncludeInactive ? ":all" : ":active");

    public string? CacheTag => "grades";

    public TimeSpan? Expiration => TimeSpan.FromMinutes(30);
}

public sealed class GetGradesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetGradesQuery, IReadOnlyList<GradeDto>>
{
    public async Task<IReadOnlyList<GradeDto>> Handle(GetGradesQuery query, CancellationToken cancellationToken) =>
        await context.EducationalGrades
            .AsNoTracking()
            .Where(grade => query.IncludeInactive || grade.IsActive)
            .OrderBy(grade => grade.SortOrder)
            .Select(grade => new GradeDto
            {
                Id = grade.Id,
                NameAr = grade.NameAr,
                NameEn = grade.NameEn,
                Slug = grade.Slug,
                Stage = grade.Stage,
                SortOrder = grade.SortOrder,
                IsActive = grade.IsActive,
                TeachersCount = grade.TeacherGrades.Count(link => link.Teacher.IsVerified),
                GroupsCount = grade.Groups.Count(group => group.IsPublished && group.IsActive),
                CoursesCount = grade.Courses.Count(course => course.IsPublished)
            })
            .ToListAsync(cancellationToken);
}

public sealed record GetGradeByIdQuery(string IdOrSlug) : IRequest<GradeDto>;

public sealed class GetGradeByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetGradeByIdQuery, GradeDto>
{
    public async Task<GradeDto> Handle(GetGradeByIdQuery query, CancellationToken cancellationToken)
    {
        var isGuid = Guid.TryParse(query.IdOrSlug, out var id);
        var slug = query.IdOrSlug.Trim().ToLowerInvariant();

        var grade = await context.EducationalGrades
            .AsNoTracking()
            .Where(entity => isGuid ? entity.Id == id : entity.Slug == slug)
            .Select(entity => new GradeDto
            {
                Id = entity.Id,
                NameAr = entity.NameAr,
                NameEn = entity.NameEn,
                Slug = entity.Slug,
                Stage = entity.Stage,
                SortOrder = entity.SortOrder,
                IsActive = entity.IsActive,
                TeachersCount = entity.TeacherGrades.Count(link => link.Teacher.IsVerified),
                GroupsCount = entity.Groups.Count(group => group.IsPublished && group.IsActive),
                CoursesCount = entity.Courses.Count(course => course.IsPublished)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return grade ?? throw new NotFoundException("EducationalGrade", query.IdOrSlug);
    }
}
