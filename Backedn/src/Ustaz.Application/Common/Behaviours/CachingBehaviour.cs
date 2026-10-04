using MediatR;
using Microsoft.Extensions.Logging;
using Ustaz.Application.Common.Interfaces;

namespace Ustaz.Application.Common.Behaviours;

/// <summary>
/// Transparently caches responses of queries implementing <see cref="ICacheableQuery"/>.
/// A cache outage must never break a request, so failures fall through to the handler.
/// </summary>
public sealed class CachingBehaviour<TRequest, TResponse>(
    ICacheService cache,
    ILogger<TRequest> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheableQuery cacheable)
        {
            return await next(cancellationToken);
        }

        try
        {
            var cached = await cache.GetAsync<TResponse>(cacheable.CacheKey, cancellationToken);
            if (cached is not null)
            {
                return cached;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cache read failed for key {CacheKey}; falling back to the handler.", cacheable.CacheKey);
        }

        var response = await next(cancellationToken);

        try
        {
            if (response is not null)
            {
                await cache.SetAsync(cacheable.CacheKey, response, cacheable.Expiration, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cache write failed for key {CacheKey}.", cacheable.CacheKey);
        }

        return response;
    }
}
