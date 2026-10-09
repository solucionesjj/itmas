using Itmas3.Application.Inventory.PurgeHistory;

namespace Itmas3.Api.Inventory;

/// <summary>Periodically removes inventory history older than the retention period (SPEC-0002 RF-17).</summary>
internal sealed partial class InventoryHistoryPurgeService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<InventoryHistoryPurgeService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, timeProvider, stoppingToken);
            using var timer = new PeriodicTimer(Interval, timeProvider);
            do
            {
                await PurgeAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var deleted = await scope.ServiceProvider.GetRequiredService<PurgeInventoryHistoryHandler>().HandleAsync(cancellationToken);
            if (deleted > 0)
            {
                LogPurged(logger, deleted);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPurgeFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {Count} expired inventory reports")]
    private static partial void LogPurged(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Inventory history purge failed; it will be retried on the next run")]
    private static partial void LogPurgeFailed(ILogger logger, Exception exception);
}
