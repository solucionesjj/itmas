using System.Data.Common;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using Npgsql;

namespace Itmas3.Agent.Collectors.SqlAudit.PostgreSql;

/// <summary>
/// PostgreSQL 18 (Npgsql). Roles, memberships and database ACLs are read once from the initial database; every database
/// that accepts connections is then read through its own connection, whose database is set in the builder (never USE).
/// TLS: <c>SslMode=Require</c> plus <see cref="SqlTls.ValidationCallback"/> installed through the data source, so all three
/// modes are validated by the agent itself during the handshake (and Npgsql never falls back to ~/.postgresql/root.crt).
/// GSS encryption is disabled and the session is read-only (<c>default_transaction_read_only</c>).
/// </summary>
public sealed class PostgreSqlEngine : ISqlEngine
{
    public async Task<SqlCatalog> CollectAsync(SqlConnectionSettings settings, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        PostgreSqlRows cluster;
        try
        {
            await using var dataSource = DataSource(settings, password, settings.Database ?? SqlConnectionSettings.DefaultPostgreSqlDatabase);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            cluster = await ReadClusterAsync(connection, cancellationToken);
        }
        catch (Exception exception) when (Classify(exception, cancellationToken) is { } kind)
        {
            throw new SqlAuditException(kind, $"PostgreSQL catalog read failed ({Describe(exception)}).", exception);
        }

        var catalogs = new List<PostgreSqlDatabaseCatalog>();
        var unavailable = new List<SqlUnavailableSection>();
        foreach (var database in cluster.Databases.Where(database => database.AllowConnections))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // Connection budget exhausted (RNF-1): the rest is reported as not read, never as empty.
                unavailable.Add(new SqlUnavailableSection(SqlSections.Database(database.Name), SqlUnavailableReasons.Timeout));
                continue;
            }

            try
            {
                await using var dataSource = DataSource(settings, password, database.Name);
                await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
                var acls = new List<PostgreSqlAclRow>();
                foreach (var query in PostgreSqlQueries.DatabaseAcls)
                {
                    acls.AddRange(await DbRows.QueryAsync(connection, query, ReadAcl, cancellationToken));
                }

                catalogs.Add(new PostgreSqlDatabaseCatalog(database.Name, acls));
            }
            catch (Exception exception) when (Classify(exception, CancellationToken.None) is { } kind)
            {
                unavailable.Add(new SqlUnavailableSection(SqlSections.Database(database.Name), SqlFailures.Reason(kind)));
            }
        }

        return PostgreSqlMapper.Map(cluster with { DatabaseCatalogs = catalogs, Unavailable = unavailable }, DateTimeOffset.UtcNow);
    }

    public async Task<SqlProbeResult> ProbeAsync(SqlConnectionSettings settings, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            await using var dataSource = DataSource(settings, password, settings.Database ?? SqlConnectionSettings.DefaultPostgreSqlDatabase);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var cluster = await ReadClusterAsync(connection, cancellationToken);

            // One database read proves the per-database catalog queries work with this account.
            foreach (var query in PostgreSqlQueries.DatabaseAcls)
            {
                await DbRows.QueryAsync(connection, query, ReadAcl, cancellationToken);
            }

            var own = (await DbRows.QueryAsync(connection, PostgreSqlQueries.OwnPrivileges, reader => (
                Superuser: DbRows.Bool(reader, "superuser") == true,
                CreateRole: DbRows.Bool(reader, "create_role") == true,
                CreateDb: DbRows.Bool(reader, "create_db") == true,
                Replication: DbRows.Bool(reader, "replication") == true,
                BypassRls: DbRows.Bool(reader, "bypass_rls") == true,
                Roles: DbRows.Long(reader, "role_count") ?? 0,
                OwnedDatabases: DbRows.Long(reader, "owned_databases") ?? 0), cancellationToken))[0];
            var excess = new List<string>();
            excess.AddRange(new (bool Held, string Text)[]
            {
                (own.Superuser, "atributo SUPERUSER"), (own.CreateRole, "atributo CREATEROLE"), (own.CreateDb, "atributo CREATEDB"),
                (own.Replication, "atributo REPLICATION"), (own.BypassRls, "atributo BYPASSRLS"),
                (own.Roles > 0, $"miembro de {own.Roles} roles (no necesita ninguno)"),
                (own.OwnedDatabases > 0, $"propietaria de {own.OwnedDatabases} bases de datos"),
            }.Where(item => item.Held).Select(item => item.Text));

            return new SqlProbeResult(cluster.Instance.Version, null, cluster.Roles.Count, cluster.Databases.Count, excess);
        }
        catch (Exception exception) when (Classify(exception, cancellationToken) is { } kind)
        {
            throw new SqlAuditException(kind, $"PostgreSQL probe failed ({Describe(exception)}).", exception);
        }
    }

    /// <summary>Connection string composed only with the builder (ADR-0014 §3): no free-form options.</summary>
    public static NpgsqlConnectionStringBuilder Builder(SqlConnectionSettings settings, string password, string database)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new NpgsqlConnectionStringBuilder
        {
            Host = settings.Host,
            Port = settings.Port ?? SqlConnectionSettings.DefaultPort(settings.Engine),
            Database = database,
            Username = settings.User,
            Password = password,
            SslMode = SslMode.Require,
            GssEncryptionMode = GssEncryptionMode.Disable,
            Timeout = (int)SqlConnectionSettings.ConnectTimeout.TotalSeconds,
            CommandTimeout = (int)SqlConnectionSettings.QueryTimeout.TotalSeconds,
            ApplicationName = SqlConnectionSettings.ApplicationName,
            Options = "-c default_transaction_read_only=on",
            Pooling = false,
            IncludeErrorDetail = false,
            Multiplexing = false,
        };
    }

    private static NpgsqlDataSource DataSource(SqlConnectionSettings settings, string password, string database)
    {
        var builder = new NpgsqlDataSourceBuilder(Builder(settings, password, database).ConnectionString);
        var validation = SqlTls.ValidationCallback(settings.TlsMode, settings.PinnedSha256);
        builder.UseSslClientAuthenticationOptionsCallback(options => options.RemoteCertificateValidationCallback = validation);
        return builder.Build();
    }

    private static async Task<PostgreSqlRows> ReadClusterAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var instance = (await DbRows.QueryAsync(connection, PostgreSqlQueries.Instance, reader =>
            new PostgreSqlInstanceRow(DbRows.RequiredString(reader, "version"), DbRows.Int(reader, "version_number") ?? 0), cancellationToken))[0];
        var roles = await DbRows.QueryAsync(connection, PostgreSqlQueries.Roles, reader => new PostgreSqlRoleRow(
            DbRows.Long(reader, "oid") ?? 0,
            DbRows.RequiredString(reader, "name"),
            DbRows.Bool(reader, "superuser") == true,
            DbRows.Bool(reader, "create_role") == true,
            DbRows.Bool(reader, "create_db") == true,
            DbRows.Bool(reader, "can_login") == true,
            DbRows.Bool(reader, "replication") == true,
            DbRows.Bool(reader, "bypass_rls") == true,
            DbRows.DateTime(reader, "valid_until")), cancellationToken);
        var memberships = await DbRows.QueryAsync(connection, PostgreSqlQueries.Memberships, reader =>
            new PostgreSqlMembershipRow(DbRows.Long(reader, "role_oid") ?? 0, DbRows.Long(reader, "member_oid") ?? 0), cancellationToken);
        var databases = await DbRows.QueryAsync(connection, PostgreSqlQueries.Databases, reader => new PostgreSqlDatabaseRow(
            DbRows.RequiredString(reader, "name"),
            DbRows.Long(reader, "owner_oid") ?? 0,
            DbRows.Bool(reader, "allow_connections") == true), cancellationToken);
        var acls = await DbRows.QueryAsync(connection, PostgreSqlQueries.ClusterAcls, ReadAcl, cancellationToken);
        if (instance.VersionNumber >= 150000)
        {
            acls.AddRange(await DbRows.QueryAsync(connection, PostgreSqlQueries.ParameterAcls, ReadAcl, cancellationToken));
        }

        return new PostgreSqlRows(instance, roles, memberships, databases, acls, [], []);
    }

    private static PostgreSqlAclRow ReadAcl(DbDataReader reader) => new(
        DbRows.RequiredString(reader, "class"),
        DbRows.String(reader, "database_name"),
        DbRows.String(reader, "schema_name"),
        DbRows.String(reader, "name"),
        DbRows.String(reader, "arguments"),
        DbRows.String(reader, "column_name"),
        DbRows.String(reader, "object_type"),
        DbRows.Long(reader, "owner_oid") ?? 0,
        DbRows.Long(reader, "parent_owner_oid"),
        DbRows.Long(reader, "grantee"),
        DbRows.Long(reader, "grantor"),
        DbRows.String(reader, "privilege"),
        DbRows.Bool(reader, "is_grantable") == true,
        DbRows.Bool(reader, "is_system") == true,
        DbRows.Bool(reader, "is_builtin") == true);

    public static SqlFailureKind? Classify(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres.SqlState switch
                {
                    PostgresErrorCodes.InvalidPassword or PostgresErrorCodes.InvalidAuthorizationSpecification => SqlFailureKind.Authentication,
                    PostgresErrorCodes.InsufficientPrivilege => SqlFailureKind.Permission,
                    PostgresErrorCodes.CannotConnectNow or PostgresErrorCodes.ObjectNotInPrerequisiteState => SqlFailureKind.Offline,
                    PostgresErrorCodes.QueryCanceled => SqlFailureKind.Timeout,
                    PostgresErrorCodes.TooManyConnections => SqlFailureKind.Connection,
                    _ => SqlFailureKind.Other,
                };
            }
        }

        if (SqlFailures.ClassifyCommon(exception, cancellationToken) is { } common)
        {
            return common;
        }

        if (exception is NpgsqlException npgsql && npgsql.Message.Contains("SSL", StringComparison.Ordinal))
        {
            // "SSL connection requested. No SSL enabled connection from this host is configured."
            return SqlFailureKind.TlsNotSupported;
        }

        return exception is DbException or InvalidOperationException ? SqlFailureKind.Other : null;
    }

    /// <summary>Exception type and SQLSTATE only: engine messages may contain names (§14.1).</summary>
    private static string Describe(Exception exception) =>
        exception is PostgresException postgres ? $"{nameof(PostgresException)} {postgres.SqlState}" : exception.GetType().Name;
}
