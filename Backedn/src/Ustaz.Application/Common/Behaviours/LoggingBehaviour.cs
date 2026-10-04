using MediatR;
using MediatR.Pipeline;
using Microsoft.Extensions.Logging;
using Ustaz.Application.Common.Interfaces;

namespace Ustaz.Application.Common.Behaviours;

/// <summary>
/// Logs who issued which request. Request payloads are intentionally not logged because
/// commands carry passwords, tokens and personal data.
/// </summary>
public sealed class LoggingBehaviour<TRequest>(
    ILogger<TRequest> logger,
    ICurrentUserService currentUser) : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "USTAZ request {RequestName} issued by user {UserId} with roles {Roles}",
            typeof(TRequest).Name,
            currentUser.UserId,
            string.Join(',', currentUser.Roles));

        return Task.CompletedTask;
    }
}
