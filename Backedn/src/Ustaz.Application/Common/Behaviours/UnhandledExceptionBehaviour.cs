using MediatR;
using Microsoft.Extensions.Logging;
using Ustaz.Application.Common.Exceptions;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Common.Behaviours;

/// <summary>
/// Logs unexpected failures at Error level while letting deliberate domain/validation
/// exceptions bubble up quietly to the global exception middleware.
/// </summary>
public sealed class UnhandledExceptionBehaviour<TRequest, TResponse>(ILogger<TRequest> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (Exception exception) when (exception is ValidationException or UstazDomainException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "USTAZ unhandled exception while processing {RequestName}",
                typeof(TRequest).Name);

            throw;
        }
    }
}
