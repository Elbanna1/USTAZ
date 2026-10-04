using Ustaz.Domain.Constants;

namespace Ustaz.Application.Common.Models;

/// <summary>Base for every paged/sorted/searchable query parameter object.</summary>
public abstract record PagedRequest
{
    private int _pageNumber = 1;
    private int _pageSize = DomainRules.DefaultPageSize;

    /// <summary>1-based page index.</summary>
    public int Page
    {
        get => _pageNumber;
        init => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value switch
        {
            < 1 => DomainRules.DefaultPageSize,
            > DomainRules.MaxPageSize => DomainRules.MaxPageSize,
            _ => value
        };
    }

    /// <summary>Free text search term applied by the individual query handler.</summary>
    public string? Search { get; init; }

    /// <summary>Sort key, e.g. "rating", "price_asc", "newest".</summary>
    public string? Sort { get; init; }
}
