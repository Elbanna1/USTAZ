using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Models;

namespace Ustaz.Application.Common.Extensions;

public static class QueryableExtensions
{
    /// <summary>Applies the predicate only when <paramref name="condition"/> holds.</summary>
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> source,
        bool condition,
        System.Linq.Expressions.Expression<Func<T, bool>> predicate) =>
        condition ? source.Where(predicate) : source;

    /// <summary>Counts and pages in a single round trip, projecting on the database side.</summary>
    public static Task<PaginatedList<T>> ToPaginatedListAsync<T>(
        this IQueryable<T> source,
        PagedRequest request,
        CancellationToken cancellationToken = default) =>
        PaginatedList<T>.CreateAsync(source, request.Page, request.PageSize, cancellationToken);

    public static Task<PaginatedList<T>> ToPaginatedListAsync<T>(
        this IQueryable<T> source,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        PaginatedList<T>.CreateAsync(source, page, pageSize, cancellationToken);
}
