namespace Ustaz.Application.Common.Interfaces;

/// <summary>
/// Marks a query whose response may be served from the distributed cache.
/// Only anonymous, read-only public data should implement this.
/// </summary>
public interface ICacheableQuery
{
    string CacheKey { get; }

    /// <summary>Logical group used for bulk invalidation, e.g. "teachers".</summary>
    string? CacheTag { get; }

    TimeSpan? Expiration { get; }
}
