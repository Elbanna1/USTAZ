using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Courses.Dtos;
using Ustaz.Application.Features.Courses.Queries;
using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Application.Features.Groups.Queries;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Application.Features.Teachers.Queries;

namespace Ustaz.Application.Features.Search;

public sealed record GlobalSearchResultDto
{
    public string Query { get; init; } = string.Empty;

    public int TotalResults { get; init; }

    public IReadOnlyList<TeacherListItemDto> Teachers { get; init; } = [];

    public IReadOnlyList<GroupListItemDto> Groups { get; init; } = [];

    public IReadOnlyList<CourseListItemDto> Courses { get; init; } = [];

    public IReadOnlyList<SubjectDto> Subjects { get; init; } = [];
}

/// <summary>Cross-entity search used by the header search box.</summary>
public sealed record GlobalSearchQuery : IRequest<GlobalSearchResultDto>
{
    public string Q { get; init; } = string.Empty;

    /// <summary>Maximum results per entity type.</summary>
    public int Take { get; init; } = 5;

    public Guid? SubjectId { get; init; }

    public Guid? GradeId { get; init; }
}

public sealed class GlobalSearchQueryValidator : AbstractValidator<GlobalSearchQuery>
{
    public GlobalSearchQueryValidator()
    {
        RuleFor(query => query.Q)
            .NotEmpty().WithMessage("Enter something to search for.")
            .MinimumLength(2).WithMessage("Enter at least 2 characters.")
            .MaximumLength(200);

        RuleFor(query => query.Take).InclusiveBetween(1, 25);
    }
}

public sealed class GlobalSearchQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GlobalSearchQuery, GlobalSearchResultDto>
{
    public async Task<GlobalSearchResultDto> Handle(GlobalSearchQuery query, CancellationToken cancellationToken)
    {
        var term = query.Q.Trim();
        var take = Math.Clamp(query.Take, 1, 25);

        var teachers = await context.TeacherProfiles
            .AsNoTracking()
            .Where(teacher => teacher.IsVerified
                && teacher.User.IsActive
                && (teacher.User.DisplayName.Contains(term)
                    || (teacher.Headline != null && teacher.Headline.Contains(term))
                    || teacher.TeacherSubjects.Any(link =>
                        link.Subject.NameAr.Contains(term) || link.Subject.NameEn.Contains(term))))
            .Where(teacher => query.SubjectId == null
                || teacher.TeacherSubjects.Any(link => link.SubjectId == query.SubjectId))
            .Where(teacher => query.GradeId == null
                || teacher.TeacherGrades.Any(link => link.GradeId == query.GradeId))
            .OrderByDescending(teacher => teacher.Rating)
            .ThenByDescending(teacher => teacher.ReviewCount)
            .Take(take)
            .Select(TeacherProjections.ToListItem)
            .ToListAsync(cancellationToken);

        var groups = await context.Groups
            .AsNoTracking()
            .Where(group => group.IsPublished
                && group.IsActive
                && (group.Name.Contains(term)
                    || (group.Description != null && group.Description.Contains(term))
                    || group.Teacher.User.DisplayName.Contains(term)))
            .Where(group => query.SubjectId == null || group.SubjectId == query.SubjectId)
            .Where(group => query.GradeId == null || group.GradeId == query.GradeId)
            .OrderByDescending(group => group.Rating)
            .ThenByDescending(group => group.CreatedAt)
            .Take(take)
            .Select(GroupProjections.ToListItem)
            .ToListAsync(cancellationToken);

        var courses = await context.Courses
            .AsNoTracking()
            .Where(course => course.IsPublished
                && (course.Title.Contains(term)
                    || (course.Description != null && course.Description.Contains(term))
                    || course.Teacher.User.DisplayName.Contains(term)))
            .Where(course => query.SubjectId == null || course.SubjectId == query.SubjectId)
            .Where(course => query.GradeId == null || course.GradeId == query.GradeId)
            .OrderByDescending(course => course.Rating)
            .ThenByDescending(course => course.CreatedAt)
            .Take(take)
            .Select(CourseProjections.ToListItem)
            .ToListAsync(cancellationToken);

        var subjects = await context.Subjects
            .AsNoTracking()
            .Where(subject => subject.IsActive
                && (subject.NameAr.Contains(term) || subject.NameEn.Contains(term)))
            .OrderBy(subject => subject.SortOrder)
            .Take(take)
            .Select(subject => new SubjectDto
            {
                Id = subject.Id,
                NameAr = subject.NameAr,
                NameEn = subject.NameEn,
                Slug = subject.Slug,
                Icon = subject.Icon,
                ColorHex = subject.ColorHex,
                IsActive = subject.IsActive
            })
            .ToListAsync(cancellationToken);

        return new GlobalSearchResultDto
        {
            Query = term,
            TotalResults = teachers.Count + groups.Count + courses.Count + subjects.Count,
            Teachers = teachers,
            Groups = groups,
            Courses = courses,
            Subjects = subjects
        };
    }
}
