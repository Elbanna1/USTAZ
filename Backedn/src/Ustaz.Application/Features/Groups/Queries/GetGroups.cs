using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Groups.Queries;

/// <summary>Public, filterable group discovery.</summary>
public sealed record GetGroupsQuery : PagedRequest, IRequest<PaginatedList<GroupListItemDto>>
{
    public Guid? SubjectId { get; init; }

    public Guid? GradeId { get; init; }

    public Guid? TeacherId { get; init; }

    public TeachingMode? TeachingMode { get; init; }

    public GroupType? Type { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public bool? HasDiscount { get; init; }

    public bool? HasAvailableSeats { get; init; }

    public string? City { get; init; }

    /// <summary>Admin/teacher-scoped switch; never forwarded for anonymous callers.</summary>
    public bool IncludeUnpublished { get; init; }
}

public sealed class GetGroupsQueryValidator : AbstractValidator<GetGroupsQuery>
{
    public GetGroupsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, DomainRules.MaxPageSize);
        RuleFor(query => query.MinPrice).GreaterThanOrEqualTo(0).When(query => query.MinPrice.HasValue);
        RuleFor(query => query.MaxPrice).GreaterThanOrEqualTo(0).When(query => query.MaxPrice.HasValue);
        RuleFor(query => query.TeachingMode).IsInEnum().When(query => query.TeachingMode.HasValue);
        RuleFor(query => query.Type).IsInEnum().When(query => query.Type.HasValue);
        RuleFor(query => query.Search).MaximumLength(200);

        RuleFor(query => query)
            .Must(query => !query.MinPrice.HasValue || !query.MaxPrice.HasValue || query.MinPrice <= query.MaxPrice)
            .WithMessage("The minimum price cannot be greater than the maximum price.")
            .WithName("MinPrice");
    }
}

public sealed class GetGroupsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetGroupsQuery, PaginatedList<GroupListItemDto>>
{
    public Task<PaginatedList<GroupListItemDto>> Handle(GetGroupsQuery query, CancellationToken cancellationToken)
    {
        var search = query.Search?.Trim();
        var city = query.City?.Trim();

        var groups = context.Groups
            .AsNoTracking()
            .WhereIf(!query.IncludeUnpublished, group => group.IsPublished && group.IsActive)
            .WhereIf(query.SubjectId.HasValue, group => group.SubjectId == query.SubjectId!.Value)
            .WhereIf(query.GradeId.HasValue, group => group.GradeId == query.GradeId!.Value)
            .WhereIf(query.TeacherId.HasValue, group => group.TeacherId == query.TeacherId!.Value)
            .WhereIf(query.TeachingMode.HasValue, group => group.TeachingMode == query.TeachingMode!.Value)
            .WhereIf(query.Type.HasValue, group => group.Type == query.Type!.Value)
            .WhereIf(query.MinPrice.HasValue, group => group.MonthlyPrice >= query.MinPrice!.Value)
            .WhereIf(query.MaxPrice.HasValue, group => group.MonthlyPrice <= query.MaxPrice!.Value)
            .WhereIf(query.HasDiscount == true, group =>
                group.DiscountPercentage > 0 || (group.OriginalPrice != null && group.OriginalPrice > group.MonthlyPrice))
            .WhereIf(query.HasAvailableSeats == true, group => group.EnrolledStudentsCount < group.Capacity)
            .WhereIf(!string.IsNullOrWhiteSpace(city), group => group.Location != null && group.Location.Contains(city!))
            .WhereIf(!string.IsNullOrWhiteSpace(search), group =>
                group.Name.Contains(search!)
                || (group.Description != null && group.Description.Contains(search!))
                || group.Teacher.User.DisplayName.Contains(search!)
                || group.Subject.NameAr.Contains(search!)
                || group.Subject.NameEn.Contains(search!));

        groups = ApplySort(groups, query.Sort);

        return groups
            .Select(GroupProjections.ToListItem)
            .ToPaginatedListAsync(query, cancellationToken);
    }

    private static IQueryable<Group> ApplySort(IQueryable<Group> source, string? sort) =>
        sort?.ToLowerInvariant() switch
        {
            "price_asc" or "price" => source.OrderBy(group => group.MonthlyPrice),
            "price_desc" => source.OrderByDescending(group => group.MonthlyPrice),
            "rating" => source.OrderByDescending(group => group.Rating).ThenByDescending(group => group.ReviewCount),
            "popular" => source.OrderByDescending(group => group.EnrolledStudentsCount),
            "starting" => source.OrderBy(group => group.StartDate),
            "oldest" => source.OrderBy(group => group.CreatedAt),
            _ => source.OrderByDescending(group => group.CreatedAt)
        };
}
