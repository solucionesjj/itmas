using Itmas3.Agent.Configuration;
using Itmas3.Agent.Scheduling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Itmas3.Agent.Service;

/// <summary>Service loop (RF-7, RF-8): waits until the next run, runs one cycle and repeats.</summary>
public sealed partial class AgentWorker(
    InventoryCycle cycle,
    IAgentStore store,
    TimeProvider timeProvider,
    ILogger<AgentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var state = store.LoadState();
        var nextRun = SchedulePolicy.NextRunAtStartup(state, timeProvider.GetUtcNow());
        LogStarted(logger, AgentRuntime.Version, nextRun);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = nextRun - timeProvider.GetUtcNow();
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, timeProvider, stoppingToken);
                }

                state = await cycle.RunAsync(state, stoppingToken);
                nextRun = state.NextRunAt ?? timeProvider.GetUtcNow() + SchedulePolicy.Interval;
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
