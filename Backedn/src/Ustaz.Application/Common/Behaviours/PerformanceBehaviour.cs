using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Ustaz.Application.Common.Interfaces;

namespace Ustaz.Application.Common.Behaviours;

/// <summary>Warns when a handler exceeds the latency budget so slow queries surface early.</summary>
public sealed class PerformanceBehaviour<TRequest, TResponse>(
    ILogger<TRequest> logger,
    ICurrentUserService currentUser) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int LongRunningThresholdMs = 800;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next(cancellationToken);

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > LongRunningThresholdMs)
        {
            logger.LogWarning(
                "USTAZ long running request {RequestName} took {ElapsedMilliseconds}ms for user {UserId}",
                typeof(TRequest).Name,
                stopwatch.ElapsedMilliseconds,
                currentUser.UserId);
        }

        return response;
    }
}
