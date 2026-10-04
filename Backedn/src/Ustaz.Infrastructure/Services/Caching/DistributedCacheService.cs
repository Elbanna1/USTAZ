using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Infrastructure.Options;

namespace Ustaz.Infrastructure.Services.Caching;

/// <summary>
/// Redis-backed when configured, in-memory otherwise. Tag membership is tracked locally so
/// invalidating "teachers" clears every derived listing key without a Redis KEYS scan.
/// </summary>
public sealed class DistributedCacheService(
    IDistributedCache cache,
    IOptions<RedisOptions> options,
    ILogger<DistributedCacheService> logger) : ICacheService
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> TaggedKeys = new();

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly RedisOptions _options = options.Value;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = await cache.GetStringAsync(key, cancellationToken);
            return string.IsNullOrEmpty(payload)
                ? default
                : JsonSerializer.Deserialize<T>(payload, SerializerOptions);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cache read failed for {CacheKey}.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, SerializerOptions);

            await cache.SetStringAsync(
                key,
                payload,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow =
                        absoluteExpiration ?? TimeSpan.FromMinutes(_options.DefaultExpirationMinutes)
                },
                cancellationToken);

            TrackKey(key);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cache write failed for {CacheKey}.", key);
        }
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);

        if (value is not null)
        {
            await SetAsync(key, value, absoluteExpiration, cancellationToken);
        }

        return value;
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveAsync(key, cancellationToken);
            foreach (var bucket in TaggedKeys.Values)
            {
                bucket.TryRemove(key, out _);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cache removal failed for {CacheKey}.", key);
        }
    }

    public async Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        if (!TaggedKeys.TryRemove(tag, out var keys))
        {
            return;
        }

        foreach (var key in keys.Keys)
        {
            try
            {
                await cache.RemoveAsync(key, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Cache removal failed for {CacheKey} under tag {Tag}.", key, tag);
            }
        }
    }

    private static void TrackKey(string key)
    {
        var separatorIndex = key.IndexOf(':', StringComparison.Ordinal);
        var tag = separatorIndex > 0 ? key[..separatorIndex] : key;

        TaggedKeys
            .GetOrAdd(tag, _ => new ConcurrentDictionary<string, byte>())
            .TryAdd(key, 0);
    }
}
