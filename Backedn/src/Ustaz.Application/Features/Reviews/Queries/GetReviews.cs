using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Reviews.Commands;
using Ustaz.Application.Features.Reviews.Dtos;
using Ustaz.Domain.Entities;

namespace Ustaz.Application.Features.Reviews.Queries;

/// <summary>Paged, approved reviews for a teacher, a group or a course.</summary>
public sealed record GetReviewsQuery : PagedRequest, IRequest<PaginatedList<ReviewDto>>
{
    public Guid? TeacherId { get; init; }

    public Guid? GroupId { get; init; }

    public Guid? CourseId { get; init; }

    public int? MinRating { get; init; }

    /// <summary>Admin-only switch used by the moderation screens.</summary>
    public bool IncludeUnapproved { get; init; }
}

public sealed class GetReviewsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetReviewsQuery, PaginatedList<ReviewDto>>
{
    public Task<PaginatedList<ReviewDto>> Handle(GetReviewsQuery query, CancellationToken cancellationToken)
    {
        var includeUnapproved = query.IncludeUnapproved && currentUser.IsAdmin;

        var reviews = context.Reviews
            .AsNoTracking()
            .WhereIf(!includeUnapproved, review => review.IsApproved)
            .WhereIf(query.TeacherId.HasValue, review => review.TeacherId == query.TeacherId!.Value)
            .WhereIf(query.GroupId.HasValue, review => review.GroupId == query.GroupId!.Value)
            .WhereIf(query.CourseId.HasValue, review => review.CourseId == query.CourseId!.Value)
            .WhereIf(query.MinRating.HasValue, review => review.Rating >= query.MinRating!.Value);

        reviews = query.Sort?.ToLowerInvariant() switch
        {
            "rating" or "rating_desc" => reviews.OrderByDescending(review => review.Rating),
            "rating_asc" => reviews.OrderBy(review => review.Rating),
            "oldest" => reviews.OrderBy(review => review.CreatedAt),
            _ => reviews.OrderByDescending(review => review.CreatedAt)
        };

        return reviews
            .Select(ReviewQueryHelper.Projection(currentUser.StudentId))
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>Star distribution and average for a teacher, group or course.</summary>
public sealed record GetReviewSummaryQuery : IRequest<ReviewSummaryDto>
{
    public Guid? TeacherId { get; init; }

    public Guid? GroupId { get; init; }

    public Guid? CourseId { get; init; }
}

public sealed class GetReviewSummaryQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetReviewSummaryQuery, ReviewSummaryDto>
{
    public async Task<ReviewSummaryDto> Handle(GetReviewSummaryQuery query, CancellationToken cancellationToken)
    {
        var reviews = context.Reviews
            .AsNoTracking()
            .Where(review => review.IsApproved)
            .WhereIf(query.TeacherId.HasValue, review => review.TeacherId == query.TeacherId!.Value)
            .WhereIf(query.GroupId.HasValue, review => review.GroupId == query.GroupId!.Value)
            .WhereIf(query.CourseId.HasValue, review => review.CourseId == query.CourseId!.Value);

        var buckets = await reviews
            .GroupBy(review => review.Rating)
            .Select(group => new { Rating = group.Key, Count = group.Count() })
            .ToDictionaryAsync(bucket => bucket.Rating, bucket => bucket.Count, cancellationToken);

        var total = buckets.Values.Sum();
        var weighted = buckets.Sum(bucket => bucket.Key * bucket.Value);

        return new ReviewSummaryDto
        {
            TotalReviews = total,
            AverageRating = total == 0
                ? 0m
                : Math.Round((decimal)weighted / total, 2, MidpointRounding.AwayFromZero),
            FiveStar = buckets.GetValueOrDefault(5),
            FourStar = buckets.GetValueOrDefault(4),
            ThreeStar = buckets.GetValueOrDefault(3),
            TwoStar = buckets.GetValueOrDefault(2),
            OneStar = buckets.GetValueOrDefault(1)
        };
    }
}
