using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Subjects.Queries;

/// <summary>Resolves a subject either by its id or by its slug, so the frontend can use pretty URLs.</summary>
public sealed record GetSubjectByIdQuery(string IdOrSlug) : IRequest<SubjectDto>;

public sealed class GetSubjectByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSubjectByIdQuery, SubjectDto>
{
    public async Task<SubjectDto> Handle(GetSubjectByIdQuery query, CancellationToken cancellationToken)
    {
        var isGuid = Guid.TryParse(query.IdOrSlug, out var id);
        var slug = query.IdOrSlug.Trim().ToLowerInvariant();

        var subject = await context.Subjects
            .AsNoTracking()
            .Where(entity => isGuid ? entity.Id == id : entity.Slug == slug)
            .Select(entity => new SubjectDto
            {
                Id = entity.Id,
                NameAr = entity.NameAr,
                NameEn = entity.NameEn,
                Slug = entity.Slug,
                Description = entity.Description,
                Icon = entity.Icon,
                ColorHex = entity.ColorHex,
                SortOrder = entity.SortOrder,
                IsActive = entity.IsActive,
                TeachersCount = entity.TeacherSubjects.Count(link => link.Teacher.IsVerified),
                GroupsCount = entity.Groups.Count(group => group.IsPublished && group.IsActive),
                CoursesCount = entity.Courses.Count(course => course.IsPublished)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return subject ?? throw new NotFoundException("Subject", query.IdOrSlug);
    }
}
