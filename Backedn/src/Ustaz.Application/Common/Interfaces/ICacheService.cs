namespace Ustaz.Application.Common.Interfaces;

/// <summary>
/// Distributed cache abstraction. The application never talks to Redis directly, and the
/// default implementation degrades to an in-memory cache when Redis is not configured.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, CancellationToken cancellationToken = default);

    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Removes every key created under a logical tag, e.g. all cached teacher listings.</summary>
    Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
}
