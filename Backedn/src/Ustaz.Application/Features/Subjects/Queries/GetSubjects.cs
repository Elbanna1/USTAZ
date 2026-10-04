using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Domain.Constants;

namespace Ustaz.Application.Features.Subjects.Queries;

/// <summary>Public subject catalogue. Cached because it changes rarely and is requested on every page.</summary>
public sealed record GetSubjectsQuery : IRequest<IReadOnlyList<SubjectDto>>, ICacheableQuery
{
    public bool IncludeInactive { get; init; }

    public string CacheKey => CacheKeys.SubjectsAll + (IncludeInactive ? ":all" : ":active");

    public string? CacheTag => "subjects";

    public TimeSpan? Expiration => TimeSpan.FromMinutes(30);
}

public sealed class GetSubjectsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSubjectsQuery, IReadOnlyList<SubjectDto>>
{
    public async Task<IReadOnlyList<SubjectDto>> Handle(GetSubjectsQuery query, CancellationToken cancellationToken) =>
        await context.Subjects
            .AsNoTracking()
            .Where(subject => query.IncludeInactive || subject.IsActive)
            .OrderBy(subject => subject.SortOrder)
            .ThenBy(subject => subject.NameEn)
            .Select(subject => new SubjectDto
            {
                Id = subject.Id,
                NameAr = subject.NameAr,
                NameEn = subject.NameEn,
                Slug = subject.Slug,
                Description = subject.Description,
                Icon = subject.Icon,
                ColorHex = subject.ColorHex,
                SortOrder = subject.SortOrder,
                IsActive = subject.IsActive,
                TeachersCount = subject.TeacherSubjects.Count(link => link.Teacher.IsVerified),
                GroupsCount = subject.Groups.Count(group => group.IsPublished && group.IsActive),
                CoursesCount = subject.Courses.Count(course => course.IsPublished)
            })
            .ToListAsync(cancellationToken);
}
