using Itmas3.Agent.Configuration;
using Itmas3.Agent.Scheduling;
using Itmas3.Agent.SqlAudit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Itmas3.Agent.Service;

/// <summary>
/// Service loop (RF-7, RF-8): waits until the next inventory or the next SQL audit retry, runs what is due and repeats.
/// The SQL audit (SPEC-0005) runs after the inventory and cannot affect it.
/// </summary>
public sealed partial class AgentWorker(
    InventoryCycle cycle,
    SqlAuditCycle sqlAudit,
    IAgentStore store,
    TimeProvider timeProvider,
    ILogger<AgentWorker> logger) : BackgroundService
{
    /// <summary>Minimum wait before a wake-up that only retries the SQL audit (no busy loop on a stale retry time).</summary>
    private static readonly TimeSpan MinimumRetryWait = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var state = store.LoadState();
        var nextInventory = SchedulePolicy.NextRunAtStartup(state, timeProvider.GetUtcNow());
        LogStarted(logger, AgentRuntime.Version, nextInventory);
        var lastWake = DateTimeOffset.MinValue;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var wake = nextInventory;
                if (sqlAudit.NextRetryAt() is { } retry && retry < wake)
                {
                    wake = retry < lastWake + MinimumRetryWait ? lastWake + MinimumRetryWait : retry;
                    wake = wake < nextInventory ? wake : nextInventory;
                }

                var delay = wake - timeProvider.GetUtcNow();
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, timeProvider, stoppingToken);
                }

                lastWake = timeProvider.GetUtcNow();
                var inventoryDue = lastWake >= nextInventory;
                if (inventoryDue)
                {
                    state = await cycle.RunAsync(state, stoppingToken);
                    nextInventory = state.NextRunAt ?? timeProvider.GetUtcNow() + SchedulePolicy.Interval;
                }

                await sqlAudit.RunAsync(state, inventoryDue, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Service stopping.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ITMas agent {Version} started; next inventory at {NextRun}")]
    private static partial void LogStarted(ILogger logger, string version, DateTimeOffset nextRun);
}
