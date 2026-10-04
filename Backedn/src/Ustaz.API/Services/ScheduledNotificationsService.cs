using MediatR;
using Ustaz.Application.Features.Analytics;
using Ustaz.Application.Features.Billing;
using Ustaz.Infrastructure.Services.Email;
using Ustaz.Application.Features.Notifications;

namespace Ustaz.API.Services;

/// <summary>
/// The platform's scheduled pass: deadline and payment reminders, plus the weekly early
/// warning to guardians of a child who is slipping.
///
/// Scheduled rather than event-driven on purpose: a parent wants one useful message, not a
/// notification per missed homework. The command itself deduplicates per student per calendar
/// week, so running this more often than weekly changes nothing — it simply catches students
/// who crossed the threshold since the last pass.
/// </summary>
public sealed class ScheduledNotificationsService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ScheduledNotificationsService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(
            Math.Clamp(configuration.GetValue("EarlyWarning:IntervalInHours", 24), 1, 168));

        // A short settle before the first pass keeps startup responsive.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

                // Time-based reminders first: a deadline nudge is only useful before the
                // deadline, and the risk pass is the slower, weekly-deduplicated one.
                var reminders = await mediator.Send(
                    new DispatchScheduledRemindersCommand(), stoppingToken);

                reminders += await mediator.Send(
                    new DispatchSubscriptionRemindersCommand(), stoppingToken);

                // Emails that failed earlier get one more chance, capped by configuration.
                var resent = await mediator.Send(new RetryFailedEmailsCommand(), stoppingToken);

                if (resent > 0)
                {
                    logger.LogInformation("Re-sent {Count} previously failed email(s).", resent);
                }

                var warnings = await mediator.Send(
                    new DispatchParentEarlyWarningsCommand(), stoppingToken);

                if (reminders + warnings > 0)
                {
                    logger.LogInformation(
                        "Sent {Reminders} reminder(s) and {Warnings} early-warning notification(s).",
                        reminders,
                        warnings);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The parent early-warning pass failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
