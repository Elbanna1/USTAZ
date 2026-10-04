using FluentValidation;
using MediatR;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ustaz.Application.Features.Teachers.Queries;

/// <summary>Public, filterable teacher directory.</summary>
public sealed record GetTeachersQuery : PagedRequest, IRequest<PaginatedList<TeacherListItemDto>>
{
    public Guid? SubjectId { get; init; }

    public Guid? GradeId { get; init; }

    public string? City { get; init; }

    public TeachingMode? TeachingMode { get; init; }

    public decimal? MinRating { get; init; }

    public bool? FeaturedOnly { get; init; }

    /// <summary>Admin-only switch; the controller never forwards it for anonymous callers.</summary>
    public bool IncludeUnverified { get; init; }
}

public sealed class GetTeachersQueryValidator : AbstractValidator<GetTeachersQuery>
{
    public GetTeachersQueryValidator()
    {
        RuleFor(query => query.PageSize).InclusiveBetween(1, DomainRules.MaxPageSize);
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.MinRating).InclusiveBetween(0, DomainRules.MaxRating).When(query => query.MinRating.HasValue);
        RuleFor(query => query.TeachingMode).IsInEnum().When(query => query.TeachingMode.HasValue);
        RuleFor(query => query.City).MaximumLength(100);
        RuleFor(query => query.Search).MaximumLength(200);
    }
}

public sealed class GetTeachersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetTeachersQuery, PaginatedList<TeacherListItemDto>>
{
    public async Task<PaginatedList<TeacherListItemDto>> Handle(
        GetTeachersQuery query,
        CancellationToken cancellationToken)
    {
        var search = query.Search?.Trim();
        var city = query.City?.Trim();

        var teachers = context.TeacherProfiles
            .AsNoTracking()
            .Where(teacher => teacher.User.IsActive)
            .WhereIf(!query.IncludeUnverified, teacher => teacher.IsVerified)
            .WhereIf(query.FeaturedOnly == true, teacher => teacher.IsFeatured)
            .WhereIf(query.SubjectId.HasValue,
                teacher => teacher.TeacherSubjects.Any(link => link.SubjectId == query.SubjectId!.Value))
            .WhereIf(query.GradeId.HasValue,
                teacher => teacher.TeacherGrades.Any(link => link.GradeId == query.GradeId!.Value))
            .WhereIf(!string.IsNullOrWhiteSpace(city), teacher => teacher.City != null && teacher.City.Contains(city!))
            .WhereIf(query.TeachingMode.HasValue, teacher => teacher.PreferredTeachingMode == query.TeachingMode!.Value)
            .WhereIf(query.MinRating.HasValue, teacher => teacher.Rating >= query.MinRating!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(search), teacher =>
                teacher.User.DisplayName.Contains(search!)
                || (teacher.Headline != null && teacher.Headline.Contains(search!))
                || teacher.TeacherSubjects.Any(link =>
                    link.Subject.NameAr.Contains(search!) || link.Subject.NameEn.Contains(search!)));

        teachers = ApplySort(teachers, query.Sort);

        return await teachers
            .Select(TeacherProjections.ToListItem)
            .ToPaginatedListAsync(query, cancellationToken);
    }

    private static IQueryable<TeacherProfile> ApplySort(IQueryable<TeacherProfile> source, string? sort) =>
        sort?.ToLowerInvariant() switch
        {
            "rating" or "rating_desc" => source.OrderByDescending(teacher => teacher.Rating).ThenByDescending(teacher => teacher.ReviewCount),
            "rating_asc" => source.OrderBy(teacher => teacher.Rating),
            "students" => source.OrderByDescending(teacher => teacher.StudentsCount),
            "experience" => source.OrderByDescending(teacher => teacher.YearsOfExperience),
            "newest" => source.OrderByDescending(teacher => teacher.CreatedAt),
            "name" => source.OrderBy(teacher => teacher.User.DisplayName),
            _ => source
                .OrderByDescending(teacher => teacher.IsFeatured)
                .ThenByDescending(teacher => teacher.Rating)
                .ThenByDescending(teacher => teacher.ReviewCount)
        };
}
