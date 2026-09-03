using DotNetMonoRepoTemplate.Logging;
using ScheduleApi.Jobs;

namespace ScheduleApi.Services;

public sealed class CronSchedulerHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly WebhookProcessorJob _webhookProcessorJob;
    private readonly Logger _logger = new(nameof(CronSchedulerHostedService));

    public CronSchedulerHostedService(WebhookProcessorJob webhookProcessorJob) => _webhookProcessorJob = webhookProcessorJob;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Info("Starting cron jobs");
        await RunJobSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunJobSafelyAsync(stoppingToken);
        }
    }

    private async Task RunJobSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _webhookProcessorJob.ProcessWebhooksAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to run webhook processor job", ex);
        }
    }
}
