using MediatR;
using Ustaz.Application.Features.Exams;

namespace Ustaz.API.Services;

/// <summary>
/// Periodically closes exam sittings whose window has passed.
///
/// This is the only background worker in the platform and it exists because a student who
/// closes the tab leaves an attempt open forever: the teacher's list stays wrong and the
/// student's remaining attempts stay blocked. Everything else in USTAZ resolves lazily on
/// read, and that lazy path stays in place as the safety net if this worker is switched off.
///
/// The work itself lives in a MediatR command, so it is unit-testable without a host and the
/// sweep here is only scheduling.
/// </summary>
public sealed class ExamAttemptExpiryService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ExamAttemptExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            Math.Clamp(configuration.GetValue("Exams:ExpirySweepSeconds", 300), 30, 3600));

        logger.LogInformation("Exam attempt expiry sweep started, running every {Interval}.", interval);

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A failed sweep must never take the host down; the next tick tries again and
                // the lazy expiry path still protects correctness in the meantime.
                logger.LogError(exception, "The exam attempt expiry sweep failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        await mediator.Send(new CloseExpiredExamAttemptsCommand(), cancellationToken);
    }
}
