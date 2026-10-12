using System.Collections.Concurrent;
using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using Microsoft.Extensions.Logging;

namespace Itmas3.Agent.Tests.SqlAudit;

/// <summary>Engine without a database: records every probe and collection and returns what the test configures.</summary>
internal sealed class FakeSqlEngine : ISqlEngine
{
    public List<(SqlConnectionSettings Settings, string Password)> Probes { get; } = [];

    public List<(SqlConnectionSettings Settings, string Password)> Collections { get; } = [];

    public Exception? ProbeFailure { get; set; }

    public Exception? CollectFailure { get; set; }

    public IReadOnlyList<string> ExcessPrivileges { get; set; } = [];

    /// <summary>Checked during the probe: e.g. that a pinned SQL Server certificate is staged where SqlClient reads it.</summary>
    public Action<SqlConnectionSettings>? OnProbe { get; set; }

    public Task<SqlCatalog> CollectAsync(SqlConnectionSettings settings, string password, CancellationToken cancellationToken)
    {
        Collections.Add((settings, password));
        return CollectFailure is null ? Task.FromResult(Catalog()) : Task.FromException<SqlCatalog>(CollectFailure);
    }

    public Task<SqlProbeResult> ProbeAsync(SqlConnectionSettings settings, string password, CancellationToken cancellationToken)
    {
        Probes.Add((settings, password));
        OnProbe?.Invoke(settings);
        return ProbeFailure is null
            ? Task.FromResult(new SqlProbeResult("16.0.4175.1", "Developer Edition (64-bit)", 12, 4, ExcessPrivileges))
            : Task.FromException<SqlProbeResult>(ProbeFailure);
    }

    public static SqlCatalog Catalog() => new(
        new SqlInstanceInfo("16.0.4175.1", null, SqlAuthenticationModes.Mixed),
        [new CatalogDatabase("Ventas_Secretas", SqlDatabaseStates.Online, "s:1")],
        [
            new CatalogSection(SqlSections.Server,
                [new CatalogAccount("s:1", SqlAccountLevels.Server, null, "cuenta_confidencial", null, SqlAccountTypes.SqlLogin, true, false, null, null, null, null, null, false, null)],
                [],
                [new CatalogPermission("s:1", Securables.Server(), "CONNECT SQL", SqlPermissionStates.Grant, null)],
                0),
        ],
        []);
}

/// <summary>Captures formatted log messages to prove what is (not) logged.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Messages.Enqueue(formatter(state, exception) + (exception is null ? string.Empty : " " + exception));
    }
}
