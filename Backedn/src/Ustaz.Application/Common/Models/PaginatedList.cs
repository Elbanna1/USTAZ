using Microsoft.EntityFrameworkCore;

namespace Ustaz.Application.Common.Models;

/// <summary>
/// Standard paged payload returned by every list endpoint. The shape is stable and
/// consumed directly by the React frontend.
/// </summary>
public class PaginatedList<T>
{
    public PaginatedList()
    {
    }

    public PaginatedList(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public IReadOnlyList<T> Items { get; init; } = [];

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => PageNumber > 1;

    public bool HasNext => PageNumber < TotalPages;

    public static async Task<PaginatedList<T>> CreateAsync(
        IQueryable<T> source,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<T>(items, totalCount, pageNumber, pageSize);
    }

    public static PaginatedList<T> Empty(int pageNumber, int pageSize) =>
        new([], 0, pageNumber, pageSize);

    public PaginatedList<TDestination> Map<TDestination>(Func<T, TDestination> projection) =>
        new([.. Items.Select(projection)], TotalCount, PageNumber, PageSize);
}
