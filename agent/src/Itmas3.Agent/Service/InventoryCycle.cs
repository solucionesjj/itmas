using Itmas3.Agent.Api;
using Itmas3.Agent.Collectors;
using Itmas3.Agent.Configuration;
using Itmas3.Agent.Scheduling;
using Microsoft.Extensions.Logging;

namespace Itmas3.Agent.Service;

/// <summary>One scheduled run: collect, send and compute the next run (persisted in the state).</summary>
public sealed partial class InventoryCycle(
    InventoryCollector collector,
    IItmasApiClient apiClient,
    IAgentStore store,
    TimeProvider timeProvider,
    Random random,
    ILogger<InventoryCycle> logger)
{
    public async Task<AgentState> RunAsync(AgentState state, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var configuration = store.LoadConfiguration();
        var apiKey = store.LoadApiKey();
        if (configuration is null || apiKey is null || !AgentConfiguration.IsValidServerUrl(configuration.ServerUrl))
        {
            LogNotEnrolled(logger);
            return Save(SchedulePolicy.AfterCredentialRejected(state, now) with { LastError = "El agente no está enrolado. Ejecute 'itmas-agent install'." });
        }

        try
        {
            var report = await collector.CollectAsync(AgentRuntime.Version, cancellationToken);
            var (outcome, detail) = await apiClient.SubmitAsync(AgentConfiguration.BaseUri(configuration.ServerUrl), apiKey, report, cancellationToken);
            now = timeProvider.GetUtcNow();
            switch (outcome)
            {
                case SubmitOutcome.Accepted:
                    LogSent(logger, report.Unavailable.Count);
                    return Save(SchedulePolicy.AfterSuccess(state, now, SchedulePolicy.RandomJitter(random)));
                case SubmitOutcome.CredentialRejected:
                    LogCredentialRejected(logger);
                    return Save(SchedulePolicy.AfterCredentialRejected(state, now));
                default:
                    var failed = SchedulePolicy.AfterFailure(state, now, $"La API respondió {detail}.");
                    LogSendFailed(logger, detail, failed.NextRunAt);
                    return Save(failed);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            var failed = SchedulePolicy.AfterFailure(state, timeProvider.GetUtcNow(), "No se pudo conectar con la API de ITMas.");
            LogSendFailed(logger, exception.GetType().Name, failed.NextRunAt);
            return Save(failed);
        }
    }

    private AgentState Save(AgentState state)
    {
        store.SaveState(state);
        return state;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Inventory sent ({Unavailable} unavailable sections)")]
    private static partial void LogSent(ILogger logger, int unavailable);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Inventory not sent ({Detail}); next attempt at {NextRunAt}")]
    private static partial void LogSendFailed(ILogger logger, string detail, DateTimeOffset? nextRunAt);

    [LoggerMessage(Level = LogLevel.Error, Message = "The API rejected the device credential; sending is suspended and retried daily. Reinstall the agent to enroll again.")]
    private static partial void LogCredentialRejected(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "The agent is not enrolled; run 'itmas-agent install'")]
    private static partial void LogNotEnrolled(ILogger logger);
}
