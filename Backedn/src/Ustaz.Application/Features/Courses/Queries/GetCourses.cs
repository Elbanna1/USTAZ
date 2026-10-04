using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Courses.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Application.Features.Courses.Queries;

/// <summary>Public, filterable course catalogue.</summary>
public sealed record GetCoursesQuery : PagedRequest, IRequest<PaginatedList<CourseListItemDto>>
{
    public Guid? SubjectId { get; init; }

    public Guid? GradeId { get; init; }

    public Guid? TeacherId { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public bool? IsFree { get; init; }

    public bool? FeaturedOnly { get; init; }

    public decimal? MinRating { get; init; }

    public bool IncludeUnpublished { get; init; }
}

public sealed class GetCoursesQueryValidator : AbstractValidator<GetCoursesQuery>
{
    public GetCoursesQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, DomainRules.MaxPageSize);
        RuleFor(query => query.MinPrice).GreaterThanOrEqualTo(0).When(query => query.MinPrice.HasValue);
        RuleFor(query => query.MaxPrice).GreaterThanOrEqualTo(0).When(query => query.MaxPrice.HasValue);
        RuleFor(query => query.MinRating).InclusiveBetween(0, DomainRules.MaxRating).When(query => query.MinRating.HasValue);
        RuleFor(query => query.Search).MaximumLength(200);

        RuleFor(query => query)
            .Must(query => !query.MinPrice.HasValue || !query.MaxPrice.HasValue || query.MinPrice <= query.MaxPrice)
            .WithMessage("The minimum price cannot be greater than the maximum price.")
            .WithName("MinPrice");
    }
}

public sealed class GetCoursesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCoursesQuery, PaginatedList<CourseListItemDto>>
{
    public Task<PaginatedList<CourseListItemDto>> Handle(GetCoursesQuery query, CancellationToken cancellationToken)
    {
        var search = query.Search?.Trim();

        var courses = context.Courses
            .AsNoTracking()
            .WhereIf(!query.IncludeUnpublished, course => course.IsPublished)
            .WhereIf(query.SubjectId.HasValue, course => course.SubjectId == query.SubjectId!.Value)
            .WhereIf(query.GradeId.HasValue, course => course.GradeId == query.GradeId!.Value)
            .WhereIf(query.TeacherId.HasValue, course => course.TeacherId == query.TeacherId!.Value)
            .WhereIf(query.MinPrice.HasValue, course => course.Price >= query.MinPrice!.Value)
            .WhereIf(query.MaxPrice.HasValue, course => course.Price <= query.MaxPrice!.Value)
            .WhereIf(query.IsFree == true, course => course.Price <= 0m)
            .WhereIf(query.IsFree == false, course => course.Price > 0m)
            .WhereIf(query.FeaturedOnly == true, course => course.IsFeatured)
            .WhereIf(query.MinRating.HasValue, course => course.Rating >= query.MinRating!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(search), course =>
                course.Title.Contains(search!)
                || (course.Description != null && course.Description.Contains(search!))
                || course.Teacher.User.DisplayName.Contains(search!)
                || course.Subject.NameAr.Contains(search!)
                || course.Subject.NameEn.Contains(search!));

        courses = ApplySort(courses, query.Sort);

        return courses
            .Select(CourseProjections.ToListItem)
            .ToPaginatedListAsync(query, cancellationToken);
    }

    private static IQueryable<Course> ApplySort(IQueryable<Course> source, string? sort) =>
        sort?.ToLowerInvariant() switch
        {
            "price_asc" or "price" => source.OrderBy(course => course.Price),
            "price_desc" => source.OrderByDescending(course => course.Price),
            "rating" => source.OrderByDescending(course => course.Rating).ThenByDescending(course => course.ReviewCount),
            "popular" => source.OrderByDescending(course => course.StudentCount),
            "duration" => source.OrderByDescending(course => course.DurationInMinutes),
            "oldest" => source.OrderBy(course => course.CreatedAt),
            _ => source
                .OrderByDescending(course => course.IsFeatured)
                .ThenByDescending(course => course.CreatedAt)
        };
}

/// <summary>The calling teacher's own courses, drafts included.</summary>
public sealed record GetTeacherCoursesQuery : PagedRequest, IRequest<PaginatedList<CourseListItemDto>>
{
    public bool? IsPublished { get; init; }

    public Guid? SubjectId { get; init; }

    public Guid? GradeId { get; init; }
}

public sealed class GetTeacherCoursesQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetTeacherCoursesQuery, PaginatedList<CourseListItemDto>>
{
    public Task<PaginatedList<CourseListItemDto>> Handle(
        GetTeacherCoursesQuery query,
        CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();
        var search = query.Search?.Trim();

        return context.Courses
            .AsNoTracking()
            .Where(course => course.TeacherId == teacherId)
            .WhereIf(query.IsPublished.HasValue, course => course.IsPublished == query.IsPublished!.Value)
            .WhereIf(query.SubjectId.HasValue, course => course.SubjectId == query.SubjectId!.Value)
            .WhereIf(query.GradeId.HasValue, course => course.GradeId == query.GradeId!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(search), course => course.Title.Contains(search!))
            .OrderByDescending(course => course.CreatedAt)
            .Select(CourseProjections.ToListItem)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>Public listing of a given teacher's published courses.</summary>
public sealed record GetCoursesByTeacherQuery : PagedRequest, IRequest<PaginatedList<CourseListItemDto>>
{
    public Guid TeacherId { get; init; }
}

public sealed class GetCoursesByTeacherQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCoursesByTeacherQuery, PaginatedList<CourseListItemDto>>
{
    public Task<PaginatedList<CourseListItemDto>> Handle(
        GetCoursesByTeacherQuery query,
        CancellationToken cancellationToken) =>
        context.Courses
            .AsNoTracking()
            .Where(course => course.TeacherId == query.TeacherId && course.IsPublished)
            .OrderByDescending(course => course.IsFeatured)
            .ThenByDescending(course => course.CreatedAt)
            .Select(CourseProjections.ToListItem)
            .ToPaginatedListAsync(query, cancellationToken);
}
