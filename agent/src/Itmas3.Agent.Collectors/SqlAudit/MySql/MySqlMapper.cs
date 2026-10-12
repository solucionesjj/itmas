using Itmas3.Agent.Collectors.SqlAudit.Model;

namespace Itmas3.Agent.Collectors.SqlAudit.MySql;

/// <summary>
/// Pure mapping of MySQL / MariaDB grant tables to the engine-neutral catalog. Accounts are <c>user@host</c>; global and
/// dynamic privileges are server permissions; database, table, column and routine privileges belong to the section of
/// their database (which may be a pattern such as <c>app\_%</c>). The level's GRANT OPTION is reported as its own
/// permission, and a level holding every static privilege is reported as <c>ALL PRIVILEGES</c>, as SHOW GRANTS does.
/// </summary>
public static class MySqlMapper
{
    private const string GrantOption = "GRANT OPTION";
    private const string AllPrivileges = "ALL PRIVILEGES";

    private static readonly Dictionary<string, string> ColumnPrivileges = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Select_priv"] = "SELECT",
        ["Insert_priv"] = "INSERT",
        ["Update_priv"] = "UPDATE",
        ["Delete_priv"] = "DELETE",
        ["Create_priv"] = "CREATE",
        ["Drop_priv"] = "DROP",
        ["Reload_priv"] = "RELOAD",
        ["Shutdown_priv"] = "SHUTDOWN",
        ["Process_priv"] = "PROCESS",
        ["File_priv"] = "FILE",
        ["Grant_priv"] = GrantOption,
        ["References_priv"] = "REFERENCES",
        ["Index_priv"] = "INDEX",
        ["Alter_priv"] = "ALTER",
        ["Show_db_priv"] = "SHOW DATABASES",
        ["Super_priv"] = "SUPER",
        ["Create_tmp_table_priv"] = "CREATE TEMPORARY TABLES",
        ["Lock_tables_priv"] = "LOCK TABLES",
        ["Execute_priv"] = "EXECUTE",
        ["Repl_slave_priv"] = "REPLICATION SLAVE",
        ["Repl_client_priv"] = "REPLICATION CLIENT",
        ["Create_view_priv"] = "CREATE VIEW",
        ["Show_view_priv"] = "SHOW VIEW",
        ["Create_routine_priv"] = "CREATE ROUTINE",
        ["Alter_routine_priv"] = "ALTER ROUTINE",
        ["Create_user_priv"] = "CREATE USER",
        ["Event_priv"] = "EVENT",
        ["Trigger_priv"] = "TRIGGER",
        ["Create_tablespace_priv"] = "CREATE TABLESPACE",
        ["Create_role_priv"] = "CREATE ROLE",
        ["Drop_role_priv"] = "DROP ROLE",
        ["Delete_history_priv"] = "DELETE HISTORY",
        ["Show_create_routine_priv"] = "SHOW CREATE ROUTINE",
    };

    /// <summary>MariaDB 12 privilege bits of mysql.global_priv ($.access), verified against mariadb:12.3.</summary>
    private static readonly string[] MariaDbAccessBits =
    [
        "SELECT", "INSERT", "UPDATE", "DELETE", "CREATE", "DROP", "RELOAD", "SHUTDOWN", "PROCESS", "FILE", GrantOption,
        "REFERENCES", "INDEX", "ALTER", "SHOW DATABASES", "SUPER", "CREATE TEMPORARY TABLES", "LOCK TABLES", "EXECUTE",
        "REPLICATION SLAVE", "BINLOG MONITOR", "CREATE VIEW", "SHOW VIEW", "CREATE ROUTINE", "ALTER ROUTINE", "CREATE USER",
        "EVENT", "TRIGGER", "CREATE TABLESPACE", "DELETE HISTORY", "SET USER", "FEDERATED ADMIN", "CONNECTION ADMIN",
        "READ_ONLY ADMIN", "REPLICATION SLAVE ADMIN", "REPLICATION MASTER ADMIN", "BINLOG ADMIN", "BINLOG REPLAY",
        "SLAVE MONITOR", "SHOW CREATE ROUTINE",
    ];

    private static readonly Dictionary<string, string> SetPrivileges = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Select"] = "SELECT",
        ["Insert"] = "INSERT",
        ["Update"] = "UPDATE",
        ["Delete"] = "DELETE",
        ["Create"] = "CREATE",
        ["Drop"] = "DROP",
        ["Grant"] = GrantOption,
        ["References"] = "REFERENCES",
        ["Index"] = "INDEX",
        ["Alter"] = "ALTER",
        ["Create View"] = "CREATE VIEW",
        ["Show view"] = "SHOW VIEW",
        ["Trigger"] = "TRIGGER",
        ["Delete versioning rows"] = "DELETE HISTORY",
        ["Execute"] = "EXECUTE",
        ["Alter Routine"] = "ALTER ROUTINE",
        ["Show Create Routine"] = "SHOW CREATE ROUTINE",
    };

    private static readonly string[] MySqlTableAll =
        ["SELECT", "INSERT", "UPDATE", "DELETE", "CREATE", "DROP", "REFERENCES", "INDEX", "ALTER", "CREATE VIEW", "SHOW VIEW", "TRIGGER"];

    private static readonly string[] MariaDbTableAll = [.. MySqlTableAll, "DELETE HISTORY"];

    public static SqlCatalog Map(MySqlRows rows, bool mariaDb, DateTimeOffset collectedAt)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var roleKeys = new HashSet<string>(
            rows.RoleEdges.Select(edge => Key(edge.RoleUser, edge.RoleHost)).Concat(rows.Accounts.Where(account => account.IsRole).Select(account => Key(account.User, account.Host))),
            StringComparer.Ordinal);

        var accounts = rows.Accounts.Select(row => Account(row, roleKeys.Contains(Key(row.User, row.Host)), collectedAt)).ToList();
        var grantors = new GrantorResolver(rows.Accounts);
        var memberships = rows.RoleEdges
            .Select(edge => new CatalogMembership(Key(edge.MemberUser, edge.MemberHost), Key(edge.RoleUser, edge.RoleHost)))
            .ToList();

        var serverPermissions = new List<CatalogPermission>();
        foreach (var account in rows.Accounts)
        {
            var key = Key(account.User, account.Host);
            var privileges = mariaDb ? DecodeAccess(account.Access ?? 0) : account.GlobalPrivileges.Select(Privilege).OfType<string>().ToList();
            var all = mariaDb
                ? MariaDbAccessBits.Where(name => name != GrantOption)
                : MySqlQueries.MySqlUserPrivilegeColumns.Select(Privilege).OfType<string>().Where(name => name != GrantOption);
            serverPermissions.AddRange(Level(privileges, all.ToList()).Select(name => new CatalogPermission(key, Securables.Server(), name, SqlPermissionStates.Grant, null)));
        }

        serverPermissions.AddRange(rows.DynamicGrants.Select(grant => new CatalogPermission(
            Key(grant.User, grant.Host),
            Securables.Server(),
            grant.Privilege,
            grant.WithGrantOption ? SqlPermissionStates.GrantWithGrantOption : SqlPermissionStates.Grant,
            null)));

        var sections = new List<CatalogSection> { new(SqlSections.Server, accounts, memberships, serverPermissions, 0) };
        var databasePermissions = new Dictionary<string, List<CatalogPermission>>(StringComparer.Ordinal);
        List<CatalogPermission> For(string database) =>
            databasePermissions.TryGetValue(database, out var list) ? list : databasePermissions[database] = [];

        var databaseAll = (mariaDb ? MySqlQueries.MariaDbDatabasePrivilegeColumns : MySqlQueries.MySqlDatabasePrivilegeColumns)
            .Select(Privilege).OfType<string>().Where(name => name != GrantOption).ToList();
        foreach (var grant in rows.DatabaseGrants)
        {
            var privileges = grant.Privileges.Select(Privilege).OfType<string>().ToList();
            For(grant.Database).AddRange(Level(privileges, databaseAll).Select(name =>
                new CatalogPermission(Key(grant.User, grant.Host), Securables.Database(grant.Database), name, SqlPermissionStates.Grant, null)));
        }

        foreach (var grant in rows.TableGrants)
        {
            var securable = Securables.Object(grant.Database, null, grant.Table, SqlObjectTypes.Table);
            For(grant.Database).AddRange(Level(SetValues(grant.Privileges), mariaDb ? MariaDbTableAll : MySqlTableAll).Select(name =>
                new CatalogPermission(Key(grant.User, grant.Host), securable, name, SqlPermissionStates.Grant, grantors.Resolve(grant.Grantor))));
        }

        foreach (var grant in rows.ColumnGrants)
        {
            var securable = Securables.Column(grant.Database, null, grant.Table, SqlObjectTypes.Table, grant.Column);
            For(grant.Database).AddRange(SetValues(grant.Privileges).Select(name =>
                new CatalogPermission(Key(grant.User, grant.Host), securable, name, SqlPermissionStates.Grant, null)));
        }

        foreach (var grant in rows.RoutineGrants)
        {
            var objectType = grant.RoutineType.ToUpperInvariant() switch
            {
                "FUNCTION" => SqlObjectTypes.Function,
                "PROCEDURE" => SqlObjectTypes.Procedure,
                _ => SqlObjectTypes.Other,
            };
            var securable = Securables.Object(grant.Database, null, grant.Routine, objectType);
            For(grant.Database).AddRange(SetValues(grant.Privileges).Select(name =>
                new CatalogPermission(Key(grant.User, grant.Host), securable, name, SqlPermissionStates.Grant, grantors.Resolve(grant.Grantor))));
        }

        sections.AddRange(databasePermissions
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new CatalogSection(SqlSections.Database(pair.Key), [], [], pair.Value, 0)));

        return new SqlCatalog(
            new SqlInstanceInfo(rows.Instance.Version, rows.Instance.Edition, SqlAuthenticationModes.Password),
            rows.Databases.Select(name => new CatalogDatabase(name, SqlDatabaseStates.Online, null)).ToList(),
            sections,
            []);
    }

    /// <summary>Account key: exactly <c>user@host</c>, which is also how tables_priv and procs_priv store the grantor.</summary>
    public static string Key(string user, string host) => $"my:{user}@{host}";

    /// <summary>
    /// The grantor column stores <c>user@host</c> where MySQL writes the account (e.g. root@%) but MariaDB writes the
    /// client host (e.g. root@127.0.0.1). It is resolved to the account with that user whose host is that literal or, if
    /// none, the most specific host pattern matching it (as the server matches accounts); otherwise it stays unknown.
    /// </summary>
    private sealed class GrantorResolver(IReadOnlyList<MySqlAccountRow> accounts)
    {
        private readonly ILookup<string, string> _hostsByUser = accounts.ToLookup(account => account.User, account => account.Host, StringComparer.Ordinal);

        public string? Resolve(string? grantor)
        {
            var at = grantor?.LastIndexOf('@') ?? -1;
            if (grantor is null || at < 0)
            {
                return null;
            }

            var (user, host) = (grantor[..at], grantor[(at + 1)..]);
            var hosts = _hostsByUser[user].ToList();
            var match = hosts.FirstOrDefault(candidate => string.Equals(candidate, host, StringComparison.OrdinalIgnoreCase))
                ?? hosts.Where(candidate => Matches(candidate, host))
                    .OrderBy(candidate => candidate.Count(character => character is '%' or '_'))
                    .ThenByDescending(candidate => candidate.Length)
                    .FirstOrDefault();
            return match is null ? null : Key(user, match);
        }

        private static bool Matches(string pattern, string host)
        {
            var expression = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("%", ".*", StringComparison.Ordinal).Replace("_", ".", StringComparison.Ordinal) + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(host, expression, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
    }

    private static CatalogAccount Account(MySqlAccountRow row, bool isRole, DateTimeOffset collectedAt)
    {
        SqlPasswordInfo? password = null;
        if (!isRole)
        {
            DateTimeOffset? validUntil = row.PasswordLifetimeDays is > 0 && row.PasswordLastChanged is { } changed
                ? new DateTimeOffset(DateTime.SpecifyKind(changed, DateTimeKind.Utc)).AddDays(row.PasswordLifetimeDays.Value)
                : null;
            password = new SqlPasswordInfo(
                PolicyEnforced: null,
                ExpirationEnforced: row.PasswordLifetimeDays is null ? null : row.PasswordLifetimeDays > 0,
                Expired: row.PasswordExpired == true || validUntil < collectedAt,
                ValidUntil: validUntil);
        }

        return new CatalogAccount(
            Key(row.User, row.Host),
            SqlAccountLevels.Server,
            null,
            row.User,
            row.Host,
            isRole ? SqlAccountTypes.MySqlRole : SqlAccountTypes.MySqlAccount,
            Enabled: true,
            Locked: row.Locked == true,
            CreatedAt: null,
            ModifiedAt: null,
            DefaultDatabase: null,
            DefaultSchema: null,
            LoginKey: null,
            IsOrphaned: false,
            Password: password);
    }

    private static string? Privilege(string column) => ColumnPrivileges.GetValueOrDefault(column);

    private static List<string> DecodeAccess(ulong access) =>
        MariaDbAccessBits.Where((_, bit) => (access & (1UL << bit)) != 0).ToList();

    private static List<string> SetValues(string set) =>
        set.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => SetPrivileges.GetValueOrDefault(value) ?? value.ToUpperInvariant())
            .ToList();

    /// <summary>The privileges of one level: ALL PRIVILEGES when every static privilege is held, plus GRANT OPTION.</summary>
    private static List<string> Level(IReadOnlyCollection<string> held, IReadOnlyCollection<string> all)
    {
        var set = new HashSet<string>(held, StringComparer.Ordinal);
        var result = all.Count > 0 && all.All(set.Contains)
            ? [AllPrivileges, .. set.Where(name => !all.Contains(name) && name != GrantOption)]
            : set.Where(name => name != GrantOption).ToList();
        if (set.Contains(GrantOption))
        {
            result.Add(GrantOption);
        }

        return result;
    }
}
