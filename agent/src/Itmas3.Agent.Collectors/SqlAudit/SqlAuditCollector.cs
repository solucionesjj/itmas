using Itmas3.Agent.Collectors.SqlAudit.Model;
using Microsoft.Extensions.Logging;

namespace Itmas3.Agent.Collectors.SqlAudit;

public interface ISqlAuditCollector
{
    /// <summary>One snapshot of one connection, with a new submissionId (RF-8). Never throws for engine failures.</summary>
    Task<SqlAuditSnapshot> CollectAsync(SqlConnectionSettings settings, string password, string agentVersion, CancellationToken cancellationToken);
}

/// <summary>
/// Collects one connection within its 5-minute budget (RNF-1). An instance that cannot be read still produces a snapshot,
/// with the instance marked unavailable and the reason (RF-7); nothing is ever filled with assumed values. Logs carry the
/// alias, the failure kind and driver error codes only: never passwords, account, database or object names (§14.1).
/// </summary>
public sealed partial class SqlAuditCollector(Func<string, ISqlEngine> engines, TimeProvider timeProvider, ILogger<SqlAuditCollector> logger)
    : ISqlAuditCollector
{
    public async Task<SqlAuditSnapshot> CollectAsync(
        SqlConnectionSettings settings, string password, string agentVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var collectedAt = timeProvider.GetUtcNow();
        var submissionId = Guid.NewGuid();
        var connection = settings.ToContract();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(SqlConnectionSettings.ConnectionBudget);

        try
        {
            var catalog = await engines(settings.Engine).CollectAsync(settings, password, budget.Token);
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = SqlSnapshotAssembler.Assemble(connection, catalog, submissionId, collectedAt, agentVersion);
            LogCollected(logger, settings.Alias, snapshot.Accounts.Count, snapshot.Permissions.Count, snapshot.Unavailable.Count);
            return snapshot;
        }
        catch (SqlAuditException exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogInstanceUnavailable(logger, settings.Alias, exception.Kind, exception.Message);
            return SqlSnapshotAssembler.Unavailable(connection, submissionId, collectedAt, agentVersion, SqlFailures.Reason(exception.Kind));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogInstanceUnavailable(logger, settings.Alias, SqlFailureKind.Timeout, "Connection budget exhausted.");
            return SqlSnapshotAssembler.Unavailable(connection, submissionId, collectedAt, agentVersion, SqlUnavailableReasons.Timeout);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SQL audit of connection {Alias} collected: {Accounts} accounts, {Permissions} permissions, {Unavailable} unavailable sections")]
    private static partial void LogCollected(ILogger logger, string alias, int accounts, int permissions, int unavailable);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SQL audit of connection {Alias} could not read the instance ({Kind}): {Detail}")]
    private static partial void LogInstanceUnavailable(ILogger logger, string alias, SqlFailureKind kind, string detail);
}
