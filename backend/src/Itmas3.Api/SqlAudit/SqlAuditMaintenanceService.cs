using Itmas3.Application.SqlAudit.Maintenance;

namespace Itmas3.Api.SqlAudit;

/// <summary>
/// Background maintenance of the SQL access audit: after start-up it recalculates the risks evaluated with an older
/// catalog (RF-15), and periodically purges the history older than the retention period (P-14).
/// </summary>
internal sealed partial class SqlAuditMaintenanceService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SqlAuditMaintenanceService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, timeProvider, stoppingToken);
            await RunAsync<RecalculateSqlRiskHandler>(
                (handler, ct) => handler.HandleAsync(ct), count => LogRiskRecalculated(logger, count), stoppingToken);

            using var timer = new PeriodicTimer(Interval, timeProvider);
            do
            {
                await RunAsync<PurgeSqlAuditHistoryHandler>(
                    (handler, ct) => handler.HandleAsync(ct), count => LogPurged(logger, count), stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task RunAsync<THandler>(Func<THandler, CancellationToken, Task<int>> run, Action<int> report, CancellationToken cancellationToken)
        where THandler : notnull
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var count = await run(scope.ServiceProvider.GetRequiredService<THandler>(), cancellationToken);
            if (count > 0)
            {
                report(count);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogMaintenanceFailed(logger, typeof(THandler).Name, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Recalculated the SQL audit risk of {Count} instances with the current catalog")]
    private static partial void LogRiskRecalculated(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {Count} expired SQL audit rows")]
    private static partial void LogPurged(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "SQL audit maintenance {Task} failed; it will be retried on the next run")]
    private static partial void LogMaintenanceFailed(ILogger logger, string task, Exception exception);
}
