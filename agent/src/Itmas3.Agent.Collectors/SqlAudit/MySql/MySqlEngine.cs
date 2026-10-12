using System.Data.Common;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using MySqlConnector;

namespace Itmas3.Agent.Collectors.SqlAudit.MySql;

/// <summary>
/// MySQL 9 LTS and MariaDB 12 LTS (MySqlConnector). Every grant lives in the <c>mysql</c> schema, so one connection reads
/// the whole instance. TLS: <c>SslMode=Required</c> plus <see cref="SqlTls.ValidationCallback"/>, which MySqlConnector
/// enforces during the handshake, before any credential is sent. VerifyCA/VerifyFull are not used on purpose: with them
/// MySqlConnector postpones a failed validation until after authentication (MariaDB "zero-configuration TLS"), which
/// would expose the password to an impostor. <c>AllowPublicKeyRetrieval</c> stays false (ADR-0015 §5).
/// </summary>
public sealed class MySqlEngine(bool mariaDb) : ISqlEngine
{
    private static readonly HashSet<string> MySqlCatalogTables =
        new(["user", "role_edges", "global_grants", "db", "tables_priv", "columns_priv", "procs_priv"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> MariaDbCatalogTables =
        new(["global_priv", "roles_mapping", "db", "tables_priv", "columns_priv", "procs_priv"], StringComparer.OrdinalIgnoreCase);

    public async Task<SqlCatalog> CollectAsync(SqlConnectionSettings settings, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            await using var connection = await OpenAsync(settings, password, cancellationToken);
            var rows = await ReadAsync(connection, cancellationToken);
            return MySqlMapper.Map(rows, mariaDb, DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (Classify(exception, cancellationToken) is { } kind)
        {
            throw new SqlAuditException(kind, $"MySQL/MariaDB catalog read failed ({Describe(exception)}).", exception);
        }
    }

    public async Task<SqlProbeResult> ProbeAsync(SqlConnectionSettings settings, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            await using var connection = await OpenAsync(settings, password, cancellationToken);
            var rows = await ReadAsync(connection, cancellationToken);
            var excess = await ExcessPrivilegesAsync(connection, cancellationToken);
            return new SqlProbeResult(rows.Instance.Version, rows.Instance.Edition, rows.Accounts.Count, rows.Databases.Count, excess);
        }
        catch (Exception exception) when (Classify(exception, cancellationToken) is { } kind)
        {
            throw new SqlAuditException(kind, $"MySQL/MariaDB probe failed ({Describe(exception)}).", exception);
        }
    }

    /// <summary>Connection string composed only with the builder (ADR-0014 §3): no free-form options.</summary>
    public static MySqlConnectionStringBuilder Builder(SqlConnectionSettings settings, string password)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new MySqlConnectionStringBuilder
        {
            Server = settings.Host,
            Port = (uint)(settings.Port ?? SqlConnectionSettings.DefaultPort(settings.Engine)),
            UserID = settings.User,
            Password = password,
            Database = string.Empty,
            SslMode = MySqlSslMode.Required,
            AllowPublicKeyRetrieval = false,
            ConnectionProtocol = MySqlConnectionProtocol.Sockets,
            ServerRedirectionMode = MySqlServerRedirectionMode.Disabled,
            AllowLoadLocalInfile = false,
            AllowUserVariables = false,
            Pooling = false,
            ConnectionTimeout = (uint)SqlConnectionSettings.ConnectTimeout.TotalSeconds,
            DefaultCommandTimeout = (uint)SqlConnectionSettings.QueryTimeout.TotalSeconds,
            ApplicationName = SqlConnectionSettings.ApplicationName,
            ConvertZeroDateTime = true,
        };
    }

    private async Task<MySqlConnection> OpenAsync(SqlConnectionSettings settings, string password, CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(Builder(settings, password).ConnectionString)
        {
            RemoteCertificateValidationCallback = SqlTls.ValidationCallback(settings.TlsMode, settings.PinnedSha256),
        };
        try
        {
            await connection.OpenAsync(cancellationToken);
            if (connection.ServerVersion.Contains("MariaDB", StringComparison.OrdinalIgnoreCase) != mariaDb)
            {
                throw new SqlAuditException(SqlFailureKind.EngineMismatch, "The server is not the configured engine.");
            }

            await DbRows.ExecuteAsync(connection, MySqlQueries.ReadOnlySession, cancellationToken);
            await DbRows.ExecuteAsync(connection, MySqlQueries.UtcSession, cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task<MySqlRows> ReadAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var instance = (await DbRows.QueryAsync(connection, MySqlQueries.Instance, reader =>
            new MySqlInstanceRow(DbRows.RequiredString(reader, "version"), DbRows.String(reader, "edition")), cancellationToken))[0];
        var databases = await DbRows.QueryAsync(connection, MySqlQueries.Databases, reader => DbRows.RequiredString(reader, "name"), cancellationToken);

        var accounts = mariaDb
            ? await DbRows.QueryAsync(connection, MySqlQueries.MariaDbAccounts, ReadMariaDbAccount, cancellationToken)
            : await DbRows.QueryAsync(connection, MySqlQueries.MySqlAccounts, ReadMySqlAccount, cancellationToken);
        var edges = await DbRows.QueryAsync(connection, mariaDb ? MySqlQueries.MariaDbRoleEdges : MySqlQueries.MySqlRoleEdges, reader => new MySqlRoleEdgeRow(
            DbRows.RequiredString(reader, "member_user"),
            DbRows.RequiredString(reader, "member_host"),
            DbRows.RequiredString(reader, "role_user"),
            DbRows.RequiredString(reader, "role_host")), cancellationToken);
        var dynamic = mariaDb
            ? []
            : await DbRows.QueryAsync(connection, MySqlQueries.MySqlDynamicGrants, reader => new MySqlDynamicGrantRow(
                DbRows.RequiredString(reader, "USER"),
                DbRows.RequiredString(reader, "HOST"),
                DbRows.RequiredString(reader, "PRIV"),
                DbRows.Bool(reader, "WITH_GRANT_OPTION") == true), cancellationToken);

        var databaseColumns = mariaDb ? MySqlQueries.MariaDbDatabasePrivilegeColumns : MySqlQueries.MySqlDatabasePrivilegeColumns;
        var databaseGrants = await DbRows.QueryAsync(connection, mariaDb ? MySqlQueries.MariaDbDatabaseGrants : MySqlQueries.MySqlDatabaseGrants, reader => new MySqlDatabaseGrantRow(
            DbRows.RequiredString(reader, "User"),
            DbRows.RequiredString(reader, "Host"),
            DbRows.RequiredString(reader, "Db"),
            databaseColumns.Where(column => DbRows.Bool(reader, column) == true).ToList()), cancellationToken);
        var tableGrants = await DbRows.QueryAsync(connection, MySqlQueries.TableGrants, reader => new MySqlTableGrantRow(
            DbRows.RequiredString(reader, "User"),
            DbRows.RequiredString(reader, "Host"),
            DbRows.RequiredString(reader, "Db"),
            DbRows.RequiredString(reader, "Table_name"),
            DbRows.String(reader, "Grantor"),
            DbRows.RequiredString(reader, "Table_priv")), cancellationToken);
        var columnGrants = await DbRows.QueryAsync(connection, MySqlQueries.ColumnGrants, reader => new MySqlColumnGrantRow(
            DbRows.RequiredString(reader, "User"),
            DbRows.RequiredString(reader, "Host"),
            DbRows.RequiredString(reader, "Db"),
            DbRows.RequiredString(reader, "Table_name"),
            DbRows.RequiredString(reader, "Column_name"),
            DbRows.RequiredString(reader, "Column_priv")), cancellationToken);
        var routineGrants = await DbRows.QueryAsync(connection, MySqlQueries.RoutineGrants, reader => new MySqlRoutineGrantRow(
            DbRows.RequiredString(reader, "User"),
            DbRows.RequiredString(reader, "Host"),
            DbRows.RequiredString(reader, "Db"),
            DbRows.RequiredString(reader, "Routine_name"),
            DbRows.RequiredString(reader, "Routine_type"),
            DbRows.String(reader, "Grantor"),
            DbRows.RequiredString(reader, "Proc_priv")), cancellationToken);

        return new MySqlRows(instance, databases, accounts, edges, dynamic, databaseGrants, tableGrants, columnGrants, routineGrants);
    }

    private static MySqlAccountRow ReadMySqlAccount(DbDataReader reader) => new(
        DbRows.RequiredString(reader, "User"),
        DbRows.RequiredString(reader, "Host"),
        MySqlQueries.MySqlUserPrivilegeColumns.Where(column => DbRows.Bool(reader, column) == true).ToList(),
        null,
        false,
        DbRows.Bool(reader, "account_locked"),
        DbRows.Bool(reader, "password_expired"),
        DbRows.DateTime(reader, "password_last_changed"),
        DbRows.Long(reader, "password_lifetime"));

    private static MySqlAccountRow ReadMariaDbAccount(DbDataReader reader)
    {
        var lastChanged = DbRows.Long(reader, "password_last_changed");
        return new MySqlAccountRow(
            DbRows.RequiredString(reader, "User"),
            DbRows.RequiredString(reader, "Host"),
            [],
            DbRows.ULong(reader, "access") ?? 0,
            DbRows.Bool(reader, "is_role") == true,
            DbRows.Bool(reader, "account_locked"),
            lastChanged == 0,
            lastChanged is > 0 ? DateTimeOffset.FromUnixTimeSeconds(lastChanged.Value).UtcDateTime : null,
            DbRows.Long(reader, "password_lifetime"));
    }

    /// <summary>SPEC-0005 §10.3: SHOW DATABASES globally and SELECT on the grant tables only; no roles.</summary>
    private async Task<IReadOnlyList<string>> ExcessPrivilegesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var excess = new List<string>();
        var global = await DbRows.QueryAsync(connection, MySqlQueries.OwnGlobalPrivileges, reader => DbRows.RequiredString(reader, "privilege"), cancellationToken);
        excess.AddRange(global
            .Where(privilege => privilege is not ("USAGE" or "SHOW DATABASES"))
            .Select(privilege => $"privilegio global {privilege}"));

        var schemas = await DbRows.QueryAsync(connection, MySqlQueries.OwnSchemaPrivileges, reader => DbRows.RequiredString(reader, "privilege"), cancellationToken);
        if (schemas.Count > 0)
        {
            excess.Add($"{schemas.Count} privilegios sobre bases de datos completas (solo se necesita SELECT sobre tablas concretas de 'mysql')");
        }

        var allowed = mariaDb ? MariaDbCatalogTables : MySqlCatalogTables;
        var tables = await DbRows.QueryAsync(connection, MySqlQueries.OwnTablePrivileges, reader => (
            Schema: DbRows.RequiredString(reader, "schema_name"),
            Table: DbRows.RequiredString(reader, "table_name"),
            Privilege: DbRows.RequiredString(reader, "privilege")), cancellationToken);
        excess.AddRange(tables
            .Where(grant => grant.Privilege != "SELECT" || !string.Equals(grant.Schema, "mysql", StringComparison.OrdinalIgnoreCase) || !allowed.Contains(grant.Table))
            .Select(grant => $"{grant.Privilege} sobre la tabla {grant.Schema}.{grant.Table}"));

        var columns = await DbRows.QueryAsync(connection, MySqlQueries.OwnColumnPrivileges, reader => (
            Schema: DbRows.RequiredString(reader, "schema_name"),
            Table: DbRows.RequiredString(reader, "table_name"),
            Privilege: DbRows.RequiredString(reader, "privilege")), cancellationToken);
        excess.AddRange(columns
            .Where(grant => grant.Privilege != "SELECT" || !string.Equals(grant.Schema, "mysql", StringComparison.OrdinalIgnoreCase) || !allowed.Contains(grant.Table))
            .Select(grant => $"{grant.Privilege} sobre columnas de {grant.Schema}.{grant.Table}")
            .Distinct(StringComparer.Ordinal));

        var roles = (await DbRows.QueryAsync(connection, MySqlQueries.OwnRoles, reader => DbRows.Long(reader, "roles") ?? 0, cancellationToken))[0];
        if (roles > 0)
        {
            excess.Add($"{roles} roles concedidos a la cuenta (no necesita ninguno)");
        }

        return excess;
    }

    public static SqlFailureKind? Classify(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (SqlFailures.ClassifyCommon(exception, cancellationToken) is { } common)
        {
            return common;
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException mysql)
            {
                return mysql.ErrorCode switch
                {
                    MySqlErrorCode.AccessDenied => SqlFailureKind.Authentication,
                    MySqlErrorCode.DatabaseAccessDenied or MySqlErrorCode.TableAccessDenied or MySqlErrorCode.ColumnAccessDenied
                        or MySqlErrorCode.SpecifiedAccessDeniedError or MySqlErrorCode.PrecedureAccessDenied => SqlFailureKind.Permission,
                    MySqlErrorCode.CommandTimeoutExpired => SqlFailureKind.Timeout,
                    MySqlErrorCode.UnableToConnectToHost when mysql.Message.Contains("SSL", StringComparison.OrdinalIgnoreCase) => SqlFailureKind.TlsNotSupported,
                    MySqlErrorCode.UnableToConnectToHost => SqlFailureKind.Connection,
                    _ => SqlFailureKind.Other,
                };
            }
        }

        return exception is DbException or InvalidOperationException ? SqlFailureKind.Other : null;
    }

    /// <summary>Exception type and error code only: engine messages may contain names (§14.1).</summary>
    private static string Describe(Exception exception) =>
        exception is MySqlException mysql ? $"{nameof(MySqlException)} {(int)mysql.ErrorCode}" : exception.GetType().Name;
}
