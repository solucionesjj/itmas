using System.Data.Common;
using System.Globalization;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using Microsoft.Data.SqlClient;

namespace Itmas3.Agent.Collectors.SqlAudit.SqlServer;

/// <summary>
/// SQL Server 2019, 2022 and 2025 (Microsoft.Data.SqlClient, managed networking on every OS). The server catalog is read
/// from master; then each online database through its own connection with that database as Initial Catalog (never USE).
/// TLS is always <c>Encrypt=Mandatory</c> (works with 2019 and later; TDS 8 Strict is 2022+ only):
/// <list type="bullet">
/// <item><c>verify</c>: <c>TrustServerCertificate=false</c> → OS trust store and host name.</item>
/// <item><c>pinned</c>: <c>ServerCertificate</c> = DER copy of the pinned certificate in the protected store; SqlClient 7.1
/// accepts only a byte-for-byte match, even when the OS would trust another certificate.</item>
/// <item><c>unverified</c>: <c>TrustServerCertificate=true</c> → still encrypted, certificate not validated (ADR-0015).</item>
/// </list>
/// </summary>
public sealed class SqlServerEngine : ISqlEngine
{
    private const string MasterDatabase = "master";

    /// <summary>Server permissions the agent login may hold (§10.3); VIEW ANY DATABASE and CONNECT SQL come with every login.</summary>
    private static readonly HashSet<string> MinimumServerPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "CONNECT SQL", "CONNECT ANY DATABASE", "VIEW ANY DATABASE", "VIEW ANY DEFINITION", "VIEW ANY SECURITY DEFINITION",
        "VIEW ANY PERFORMANCE DEFINITION",
    };

    public async Task<SqlCatalog> CollectAsync(SqlConnectionSettings settings, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        SqlServerRows server;
        try
        {
            await using var connection = await OpenAsync(settings, password, MasterDatabase, cancellationToken);
            server = await ReadServerAsync(connection, cancellationToken);
        }
        catch (Exception exception) when (Classify(exception, cancellationToken) is { } kind)
        {
            throw new SqlAuditException(kind, $"SQL Server catalog read failed ({Describe(exception)}).", exception);
        }

        var catalogs = new List<SqlServerDatabaseCatalog>();
        var unavailable = new List<SqlUnavailableSection>();
        foreach (var database in server.Databases)
        {
            if (SqlServerMapper.DatabaseState(database.State) != SqlDatabaseStates.Online)
            {
                unavailable.Add(new SqlUnavailableSection(SqlSections.Database(database.Name), SqlUnavailableReasons.Offline));
                continue;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                // Connection budget exhausted (RNF-1): the rest is reported as not read, never as empty.
                unavailable.Add(new SqlUnavailableSection(SqlSections.Database(database.Name), SqlUnavailableReasons.Timeout));
                continue;
            }

            try
            {
                await using var connection = await OpenAsync(settings, password, database.Name, cancellationToken);
                catalogs.Add(await ReadDatabaseAsync(connection, database.Name, cancellationToken));
            }
            catch (Exception exception) when (Classify(exception, CancellationToken.None) is { } kind)
            {
                unavailable.Add(new SqlUnavailableSection(SqlSections.Database(database.Name), SqlFailures.Reason(kind)));
            }
        }

        return SqlServerMapper.Map(server with { DatabaseCatalogs = catalogs, Unavailable = unavailable });
    }

    public async Task<SqlProbeResult> ProbeAsync(SqlConnectionSettings settings, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            await using var connection = await OpenAsync(settings, password, MasterDatabase, cancellationToken);
            var server = await ReadServerAsync(connection, cancellationToken);
            await ReadDatabaseAsync(connection, MasterDatabase, cancellationToken);
            var permissions = await DbRows.QueryAsync(connection, SqlServerQueries.OwnServerPermissions, reader => DbRows.RequiredString(reader, "permission_name"), cancellationToken);
            var excess = permissions
                .Where(permission => !MinimumServerPermissions.Contains(permission))
                .Select(permission => $"permiso de servidor {permission}")
                .ToList();
            return new SqlProbeResult(server.Instance.Version, server.Instance.Edition, server.ServerPrincipals.Count, server.Databases.Count, excess);
        }
        catch (Exception exception) when (Classify(exception, cancellationToken) is { } kind)
        {
            throw new SqlAuditException(kind, $"SQL Server probe failed ({Describe(exception)}).", exception);
        }
    }

    /// <summary>Connection string composed only with the builder (ADR-0014 §3): no free-form options.</summary>
    public static SqlConnectionStringBuilder Builder(SqlConnectionSettings settings, string password, string database)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.TlsMode == SqlTlsModes.Pinned && string.IsNullOrEmpty(settings.PinnedCertificatePath))
        {
            throw new SqlAuditException(SqlFailureKind.Certificate, "The pinned certificate file is missing.");
        }

        // Host and instance are validated (DNS name or IP; instance pattern), so they cannot inject options.
        var port = settings.Port?.ToString(CultureInfo.InvariantCulture);
        var server = settings.InstanceName is null ? $"tcp:{settings.Host},{port}"
            : port is null ? $@"tcp:{settings.Host}\{settings.InstanceName}"
            : $@"tcp:{settings.Host}\{settings.InstanceName},{port}";
        return new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            UserID = settings.User,
            Password = password,
            IntegratedSecurity = false,
            Authentication = SqlAuthenticationMethod.SqlPassword,
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = settings.TlsMode == SqlTlsModes.Unverified,
            ServerCertificate = settings.TlsMode == SqlTlsModes.Pinned ? settings.PinnedCertificatePath : string.Empty,
            ApplicationName = SqlConnectionSettings.ApplicationName,
            ApplicationIntent = ApplicationIntent.ReadOnly,
            ConnectTimeout = (int)SqlConnectionSettings.ConnectTimeout.TotalSeconds,
            CommandTimeout = (int)SqlConnectionSettings.QueryTimeout.TotalSeconds,
            Pooling = false,
            PersistSecurityInfo = false,
            MultipleActiveResultSets = false,
        };
    }

    private static async Task<SqlConnection> OpenAsync(SqlConnectionSettings settings, string password, string database, CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(Builder(settings, password, database).ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await DbRows.ExecuteAsync(connection, SqlServerQueries.Session, cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<SqlServerRows> ReadServerAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var instance = (await DbRows.QueryAsync(connection, SqlServerQueries.Instance, reader => new SqlServerInstanceRow(
            DbRows.RequiredString(reader, "version"),
            DbRows.String(reader, "edition"),
            DbRows.Bool(reader, "integrated_only") == true,
            DbRows.Int(reader, "tz_offset") ?? 0), cancellationToken))[0];
        var principals = await DbRows.QueryAsync(connection, SqlServerQueries.ServerPrincipals, ReadPrincipal, cancellationToken);
        var members = await DbRows.QueryAsync(connection, SqlServerQueries.ServerRoleMembers, ReadMember, cancellationToken);
        var permissions = await DbRows.QueryAsync(connection, SqlServerQueries.ServerPermissions, ReadPermission, cancellationToken);
        var databases = await DbRows.QueryAsync(connection, SqlServerQueries.Databases, reader => new SqlServerDatabaseRow(
            DbRows.RequiredString(reader, "name"),
            DbRows.RequiredString(reader, "state"),
            DbRows.Hex(reader, "owner_sid")), cancellationToken);
        return new SqlServerRows(instance, principals, members, permissions, databases, [], []);
    }

    private static async Task<SqlServerDatabaseCatalog> ReadDatabaseAsync(DbConnection connection, string database, CancellationToken cancellationToken)
    {
        var principals = await DbRows.QueryAsync(connection, SqlServerQueries.DatabasePrincipals, ReadPrincipal, cancellationToken);
        var members = await DbRows.QueryAsync(connection, SqlServerQueries.DatabaseRoleMembers, ReadMember, cancellationToken);
        var permissions = await DbRows.QueryAsync(connection, SqlServerQueries.DatabasePermissions, ReadPermission, cancellationToken);
        var ownerships = await DbRows.QueryAsync(connection, SqlServerQueries.Ownerships, reader => new SqlServerOwnershipRow(
            DbRows.RequiredString(reader, "kind"),
            DbRows.RequiredString(reader, "schema_name"),
            DbRows.String(reader, "object_name"),
            DbRows.String(reader, "object_type"),
            DbRows.Int(reader, "owner_id") ?? 0), cancellationToken);
        return new SqlServerDatabaseCatalog(database, principals, members, permissions, ownerships);
    }

    private static SqlServerPrincipalRow ReadPrincipal(DbDataReader reader) => new(
        DbRows.Int(reader, "principal_id") ?? 0,
        DbRows.RequiredString(reader, "name"),
        DbRows.RequiredString(reader, "type").Trim(),
        DbRows.Hex(reader, "sid"),
        DbRows.Bool(reader, "is_disabled") == true,
        DbRows.DateTime(reader, "create_date"),
        DbRows.DateTime(reader, "modify_date"),
        DbRows.String(reader, "default_database_name"),
        DbRows.String(reader, "default_schema_name"),
        DbRows.Int(reader, "authentication_type"),
        DbRows.Bool(reader, "is_policy_checked"),
        DbRows.Bool(reader, "is_expiration_checked"),
        DbRows.Bool(reader, "is_expired"),
        DbRows.Bool(reader, "is_locked"));

    private static SqlServerRoleMemberRow ReadMember(DbDataReader reader) =>
        new(DbRows.Int(reader, "role_id") ?? 0, DbRows.Int(reader, "member_id") ?? 0);

    private static SqlServerPermissionRow ReadPermission(DbDataReader reader) => new(
        DbRows.Int(reader, "class") ?? -1,
        DbRows.Int(reader, "grantee_id") ?? 0,
        DbRows.Int(reader, "grantor_id") ?? 0,
        DbRows.RequiredString(reader, "permission_name"),
        DbRows.RequiredString(reader, "state").Trim(),
        DbRows.String(reader, "schema_name"),
        DbRows.String(reader, "object_name"),
        DbRows.String(reader, "object_type")?.Trim(),
        DbRows.String(reader, "column_name"),
        DbRows.Bool(reader, "is_builtin_public") == true);

    public static SqlFailureKind? Classify(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (exception is SqlException sql)
        {
            var numbers = sql.Errors.Cast<SqlError>().Select(error => error.Number).ToHashSet();
            if (numbers.Overlaps([942, 952, 955, 976, 978, 983]))
            {
                return SqlFailureKind.Offline; // offline, in transition, AG replica not readable
            }

            if (numbers.Overlaps([4060, 916, 229, 230, 262, 297, 300, 15247]))
            {
                return SqlFailureKind.Permission;
            }

            if (numbers.Overlaps([18456, 18486, 18487, 18488, 18470]))
            {
                return SqlFailureKind.Authentication;
            }

            if (numbers.Overlaps([-2, 1222]))
            {
                return SqlFailureKind.Timeout;
            }
        }

        if (SqlFailures.ClassifyCommon(exception, cancellationToken) is { } common)
        {
            return common;
        }

        if (exception is SqlException network && network.Class >= 20)
        {
            // Pre-login failures (unreachable host, unknown instance): SqlClient reports them as class 20 errors.
            return network.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase) ? SqlFailureKind.Certificate : SqlFailureKind.Connection;
        }

        return exception is DbException or InvalidOperationException ? SqlFailureKind.Other : null;
    }

    /// <summary>Exception type and error numbers only: engine messages may contain names (§14.1).</summary>
    private static string Describe(Exception exception) =>
        exception is SqlException sql
            ? $"{nameof(SqlException)} {string.Join(',', sql.Errors.Cast<SqlError>().Select(error => error.Number.ToString(CultureInfo.InvariantCulture)))}"
            : exception.GetType().Name;
}
