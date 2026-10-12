using Itmas3.Agent.Api;
using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.Configuration;
using Itmas3.Agent.Scheduling;
using Itmas3.Agent.Service;
using Microsoft.Extensions.Logging;

namespace Itmas3.Agent.SqlAudit;

/// <summary>
/// Daily SQL audit (SPEC-0005 RF-1, RF-8, P-12): runs right after the inventory and sends one snapshot per configured
/// connection. A connection is collected when the inventory runs and its last successful send is older than
/// <see cref="MinimumInterval"/> (so inventory retries do not resend it), or when its own retry is due. Each collected
/// snapshot gets a new submissionId; the short HTTP retries of that same snapshot reuse it. A failed send is retried with
/// the inventory's backoff (1, 2, 4… 60 min), re-collecting each time (SPEC-0002 RF-8).
/// Nothing here can affect the inventory: every failure is caught and logged, and an agent without connections never
/// opens an engine connection (CA-2).
/// </summary>
public sealed partial class SqlAuditCycle(
    ISqlConnectionStore connections,
    ISqlAuditCollector collector,
    IItmasApiClient apiClient,
    IAgentStore store,
    TimeProvider timeProvider,
    ILogger<SqlAuditCycle> logger,
    IReadOnlyList<TimeSpan>? sendRetryDelays = null)
{
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromHours(20);

    /// <summary>Waits between the HTTP attempts of the same snapshot (3 attempts in total).</summary>
    public static readonly IReadOnlyList<TimeSpan> DefaultSendRetryDelays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20)];

    private readonly IReadOnlyList<TimeSpan> _sendRetryDelays = sendRetryDelays ?? DefaultSendRetryDelays;

    /// <summary>Earliest pending retry of a configured connection; null when there is none.</summary>
    public DateTimeOffset? NextRetryAt()
    {
        try
        {
            var configured = connections.List().Select(connection => connection.Id).ToHashSet();
            return connections.LoadResults()
                .Where(pair => configured.Contains(pair.Key) && pair.Value.NextRetryAt is not null)
                .Select(pair => pair.Value.NextRetryAt)
                .Min();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            LogStoreUnreadable(logger, exception.GetType().Name);
            return null;
        }
    }

    /// <param name="inventoryState">State after the inventory: a rejected device credential stops the SQL audit too.</param>
    /// <param name="inventoryRan">True when this wake-up ran the daily inventory.</param>
    public async Task RunAsync(AgentState inventoryState, bool inventoryRan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inventoryState);
        try
        {
            await RunCoreAsync(inventoryState, inventoryRan, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // The SQL audit must never break the agent or the inventory (SPEC-0005 RF-1).
            LogCycleFailed(logger, exception.GetType().Name);
        }
    }

    private async Task RunCoreAsync(AgentState inventoryState, bool inventoryRan, CancellationToken cancellationToken)
    {
        var configured = connections.List();
        if (configured.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var results = connections.LoadResults();
        var due = configured
            .Where(connection => IsDue(results.GetValueOrDefault(connection.Id) ?? new SqlConnectionResult(), inventoryRan, now))
            .ToList();
        if (due.Count == 0)
        {
            return;
        }

        var configuration = store.LoadConfiguration();
        var apiKey = store.LoadApiKey();
        var credentialRejected = inventoryState.CredentialRejectedAt is { } rejected && (inventoryState.LastSuccessAt is null || rejected > inventoryState.LastSuccessAt);
        if (configuration is null || apiKey is null || credentialRejected || !AgentConfiguration.IsValidServerUrl(configuration.ServerUrl))
        {
            // The inventory reports and retries the credential problem; the SQL audit waits for the next daily run.
            foreach (var connection in due)
            {
                Save(connection, (results.GetValueOrDefault(connection.Id) ?? new SqlConnectionResult()) with { NextRetryAt = null });
            }

            return;
        }

        var server = AgentConfiguration.BaseUri(configuration.ServerUrl);
        foreach (var connection in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var previous = results.GetValueOrDefault(connection.Id) ?? new SqlConnectionResult();
            try
            {
                if (!await RunConnectionAsync(connection, previous, server, apiKey, cancellationToken))
                {
                    return; // Device credential rejected: stop here, the inventory handles it.
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // An unexpected failure of one connection affects neither the others nor the inventory.
                var failures = previous.ConsecutiveFailures + 1;
                LogConnectionFailed(logger, connection.Alias, exception.GetType().Name);
                Save(connection, previous with
                {
                    LastAttemptAt = timeProvider.GetUtcNow(),
                    LastOutcome = SqlOutcomes.Failed,
                    ConsecutiveFailures = failures,
                    NextRetryAt = timeProvider.GetUtcNow() + SchedulePolicy.Backoff(failures),
                    LastError = $"Error inesperado del agente ({exception.GetType().Name}).",
                });
            }
        }
    }

    /// <returns>False when the API rejected the device credential.</returns>
    private async Task<bool> RunConnectionAsync(
        SqlConnectionDefinition connection, SqlConnectionResult previous, Uri server, string apiKey, CancellationToken cancellationToken)
    {
        var attemptAt = timeProvider.GetUtcNow();
        if (connections.LoadPassword(connection) is not { Length: > 0 } password)
        {
            LogCredentialMissing(logger, connection.Alias);
            Save(connection, previous with
            {
                LastAttemptAt = attemptAt,
                LastOutcome = SqlOutcomes.Failed,
                NextRetryAt = null,
                LastError = "Falta la contraseña de la conexión. Vuelva a configurarla con 'itmas-agent sql update'.",
            });
            return true;
        }

        var snapshot = await collector.CollectAsync(connections.Settings(connection), password, AgentRuntime.Version, cancellationToken);
        var (outcome, detail) = await SendAsync(server, apiKey, snapshot, cancellationToken);
        var now = timeProvider.GetUtcNow();
        switch (outcome)
        {
            case SubmitOutcome.Accepted:
                var unreachable = snapshot.Instance is null;
                LogSent(logger, connection.Alias, snapshot.Unavailable.Count);
                Save(connection, previous with
                {
                    LastAttemptAt = attemptAt,
                    LastSuccessAt = now,
                    LastOutcome = unreachable ? SqlOutcomes.Unreachable : snapshot.Unavailable.Count > 0 ? SqlOutcomes.Partial : SqlOutcomes.Complete,
                    UnavailableSections = snapshot.Unavailable.Count,
                    ConsecutiveFailures = 0,
                    NextRetryAt = null,
                    LastError = unreachable ? $"No se pudo leer el motor ({snapshot.Unavailable[0].Reason}); se envió la instantánea con el motivo." : null,
                });
                return true;
            case SubmitOutcome.CredentialRejected:
                LogCredentialRejected(logger, connection.Alias);
                Save(connection, previous with
                {
                    LastAttemptAt = attemptAt,
                    LastOutcome = SqlOutcomes.Failed,
                    NextRetryAt = null,
                    LastError = "La API rechazó la credencial del dispositivo (401).",
                });
                return false;
            default:
                var failures = previous.ConsecutiveFailures + 1;
                var retryAt = now + SchedulePolicy.Backoff(failures);
                LogSendFailed(logger, connection.Alias, detail, retryAt);
                Save(connection, previous with
                {
                    LastAttemptAt = attemptAt,
                    LastOutcome = SqlOutcomes.Failed,
                    ConsecutiveFailures = failures,
                    NextRetryAt = retryAt,
                    LastError = detail == "network" ? "No se pudo conectar con la API de ITMas." : $"La API respondió {detail}.",
                });
                return true;
        }
    }

    /// <summary>Sends one snapshot; transient failures are retried with the same snapshot, hence the same submissionId.</summary>
    private async Task<(SubmitOutcome Outcome, string Detail)> SendAsync(Uri server, string apiKey, SqlAuditSnapshot snapshot, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            (SubmitOutcome Outcome, string Detail) result;
            try
            {
                result = await apiClient.SubmitSqlAuditAsync(server, apiKey, snapshot, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                result = (SubmitOutcome.TransientFailure, "network");
            }

            if (result.Outcome != SubmitOutcome.TransientFailure || attempt >= _sendRetryDelays.Count)
            {
                return result;
            }

            await Task.Delay(_sendRetryDelays[attempt], timeProvider, cancellationToken);
        }
    }

    private static bool IsDue(SqlConnectionResult result, bool inventoryRan, DateTimeOffset now) =>
        (inventoryRan && (result.LastSuccessAt is null || now - result.LastSuccessAt >= MinimumInterval))
        || result.NextRetryAt <= now;

    private void Save(SqlConnectionDefinition connection, SqlConnectionResult result)
    {
        try
        {
            connections.SaveResult(connection.Id, result);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogStoreUnreadable(logger, exception.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SQL audit snapshot of connection {Alias} sent ({Unavailable} unavailable sections)")]
    private static partial void LogSent(ILogger logger, string alias, int unavailable);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SQL audit snapshot of connection {Alias} not sent ({Detail}); next attempt at {RetryAt}")]
    private static partial void LogSendFailed(ILogger logger, string alias, string detail, DateTimeOffset retryAt);

    [LoggerMessage(Level = LogLevel.Error, Message = "The API rejected the device credential while sending the SQL audit of connection {Alias}")]
    private static partial void LogCredentialRejected(ILogger logger, string alias);

    [LoggerMessage(Level = LogLevel.Error, Message = "The stored password of SQL connection {Alias} is missing; run 'itmas-agent sql update'")]
    private static partial void LogCredentialMissing(ILogger logger, string alias);

    [LoggerMessage(Level = LogLevel.Error, Message = "The SQL connection store could not be read or written ({ExceptionType})")]
    private static partial void LogStoreUnreadable(ILogger logger, string exceptionType);

    [LoggerMessage(Level = LogLevel.Error, Message = "SQL audit of connection {Alias} failed unexpectedly ({ExceptionType}); the inventory is not affected")]
    private static partial void LogConnectionFailed(ILogger logger, string alias, string exceptionType);

    [LoggerMessage(Level = LogLevel.Error, Message = "The SQL audit cycle failed ({ExceptionType}); the inventory is not affected")]
    private static partial void LogCycleFailed(ILogger logger, string exceptionType);
}
