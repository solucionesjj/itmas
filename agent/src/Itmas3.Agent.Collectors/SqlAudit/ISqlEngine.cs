using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.Collectors.SqlAudit.MySql;
using Itmas3.Agent.Collectors.SqlAudit.PostgreSql;
using Itmas3.Agent.Collectors.SqlAudit.SqlServer;

namespace Itmas3.Agent.Collectors.SqlAudit;

/// <summary>
/// I/O side of one engine: opens encrypted connections (<see cref="SqlTls"/>), runs the fixed catalog queries and hands the
/// raw rows to its pure mapper. Failures that prevent reading the instance are thrown as <see cref="SqlAuditException"/>;
/// databases that cannot be read are reported in <see cref="SqlCatalog.Unavailable"/> (RF-7).
/// </summary>
public interface ISqlEngine
{
    Task<SqlCatalog> CollectAsync(SqlConnectionSettings settings, string password, CancellationToken cancellationToken);

    /// <summary>
    /// <c>sql add</c>, <c>sql update</c> and <c>sql test</c>: connection, certificate and server-level catalog read, plus the
    /// privileges the account has beyond the minimum of SPEC-0005 §10.3.
    /// </summary>
    Task<SqlProbeResult> ProbeAsync(SqlConnectionSettings settings, string password, CancellationToken cancellationToken);
}

/// <param name="ExcessPrivileges">Privileges beyond the minimum (fixed descriptions, in Spanish, for the administrator).</param>
public sealed record SqlProbeResult(string Version, string? Edition, int Accounts, int Databases, IReadOnlyList<string> ExcessPrivileges);

public static class SqlEngineFactory
{
    public static ISqlEngine Create(string engine) => engine switch
    {
        SqlEngines.SqlServer => new SqlServerEngine(),
        SqlEngines.PostgreSql => new PostgreSqlEngine(),
        SqlEngines.MySql => new MySqlEngine(mariaDb: false),
        SqlEngines.MariaDb => new MySqlEngine(mariaDb: true),
        _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unsupported engine."),
    };
}
