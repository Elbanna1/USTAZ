using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ustaz.Application.Common.Emails;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Enums;
using Ustaz.Infrastructure.Options;

namespace Ustaz.Infrastructure.Services.Email;

/// <summary>
/// Re-sends emails whose first attempt failed.
///
/// Deliberately conservative: only rows that are still <see cref="EmailDeliveryStatus.Failed"/>
/// and still carry a payload are retried, the attempt count is capped by configuration, and a
/// row that exhausts its attempts becomes <see cref="EmailDeliveryStatus.Abandoned"/> rather
/// than looping forever. Security emails have no payload, so they are never retried — a reset
/// link that arrives an hour late is worse than none, and the user can simply ask again.
/// </summary>
public sealed record RetryFailedEmailsCommand : IRequest<int>
{
    public int BatchSize { get; init; } = 50;

    /// <summary>How long to wait after a failure before trying again.</summary>
    public int BackoffMinutes { get; init; } = 10;
}

public sealed class RetryFailedEmailsCommandHandler(
    IApplicationDbContext context,
    ITransactionalEmailService transactionalEmail,
    IDateTimeProvider dateTimeProvider,
    IOptions<EmailOptions> options,
    ILogger<RetryFailedEmailsCommandHandler> logger) : IRequestHandler<RetryFailedEmailsCommand, int>
{
    private readonly EmailOptions _options = options.Value;

    public async Task<int> Handle(RetryFailedEmailsCommand command, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return 0;
        }

        var utcNow = dateTimeProvider.UtcNow;
        var retryBefore = utcNow.AddMinutes(-Math.Clamp(command.BackoffMinutes, 1, 720));

        var pending = await context.EmailDeliveries
            .Where(delivery => delivery.Status == EmailDeliveryStatus.Failed
                && delivery.PayloadJson != null
                && delivery.Attempts < _options.MaxDeliveryAttempts
                && delivery.FailedAt != null
                && delivery.FailedAt < retryBefore)
            .OrderBy(delivery => delivery.FailedAt)
            .Take(Math.Clamp(command.BatchSize, 1, 200))
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        var sent = 0;

        foreach (var delivery in pending)
        {
            if (!EmailCatalog.TryGet(delivery.Event, out var template))
            {
                // The template was removed since the row was written; there is nothing left
                // to render, so stop trying rather than failing forever.
                delivery.MarkSkipped(utcNow, "TemplateMissing");
                continue;
            }

            var values = Deserialize(delivery.PayloadJson!);

            var message = EmailRenderer.Render(
                template,
                delivery.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                    ? EmailLanguage.English
                    : EmailLanguage.Arabic,
                delivery.Recipient,
                null,
                values,
                new EmailBranding
                {
                    ClientBaseUrl = _options.ClientBaseUrl,
                    SupportEmail = _options.SupportEmail,
                    ProductName = _options.FromName
                });

            if (transactionalEmail is TransactionalEmailService service
                && await service.AttemptAsync(delivery, message, cancellationToken))
            {
                sent++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        if (sent > 0)
        {
            logger.LogInformation("Re-sent {Count} previously failed email(s).", sent);
        }

        return sent;
    }

    private static IReadOnlyDictionary<string, string>? Deserialize(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
