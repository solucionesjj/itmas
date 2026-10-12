namespace Itmas3.Domain.SqlAudit;

/// <summary>
/// Risk catalog per engine (SPEC-0005 §8.6, RF-14). It lives in the server so it can change without touching the agent
/// (RF-15): raise <see cref="Version"/> whenever a rule changes and the stored risks are recalculated on deployment.
/// <list type="bullet">
/// <item>A <c>DENY</c> carries no risk (<see cref="RiskLevel.Denied"/>).</item>
/// <item>Anything not listed is <see cref="RiskLevel.High"/>, so nothing is left unreviewed (RN-9).</item>
/// <item>A non-critical permission granted <c>WITH GRANT OPTION</c> is at least High.</item>
/// <item>An account is General unless it is a well-known all-powerful account (<c>sa</c>, <c>root</c>) or a role of the
/// catalog; its permissions and roles are separate elements and raise the account risk through them. A MySQL/MariaDB
/// anonymous account (<c>''@host</c>) is High: anyone can connect with it.</item>
/// </list>
/// </summary>
public static class SqlRiskCatalog
{
    public const int Version = 1;

    private static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;

    // SQL Server
    private static readonly HashSet<string> SqlServerCriticalRoles = new(Names)
    {
        "sysadmin", "securityadmin", "serveradmin", "db_owner", "db_securityadmin", "db_accessadmin",
    };

    private static readonly HashSet<string> SqlServerHighRoles = new(Names) { "db_datawriter", "db_ddladmin", "dbcreator" };

    private static readonly HashSet<string> SqlServerGeneralRoles = new(Names) { "db_datareader", "public" };

    private static readonly HashSet<string> SqlServerCriticalServerPermissions = new(Names)
    {
        "CONTROL SERVER", "ALTER ANY LOGIN", "ALTER ANY SERVER ROLE", "IMPERSONATE ANY LOGIN",
    };

    private static readonly HashSet<string> SqlServerCriticalDatabasePermissions = new(Names)
    {
        "CONTROL", "ALTER ANY USER", "ALTER ANY ROLE", "IMPERSONATE", "IMPERSONATE ANY USER", "OWNERSHIP",
    };

    // CONNECT SQL and VIEW ANY DATABASE are granted to every login by default: same level as CONNECT.
    private static readonly HashSet<string> SqlServerGeneralPermissions = new(Names)
    {
        "SELECT", "CONNECT", "CONNECT SQL", "VIEW DEFINITION", "VIEW ANY DATABASE", "SHOWPLAN",
    };

    // PostgreSQL
    private static readonly HashSet<string> PostgreSqlCriticalRoles = new(Names)
    {
        "pg_execute_server_program", "pg_read_server_files", "pg_write_server_files",
    };

    private static readonly HashSet<string> PostgreSqlHighRoles = new(Names) { "pg_write_all_data", "pg_read_all_data" };

    private static readonly HashSet<string> PostgreSqlCriticalServerPermissions = new(Names) { "SUPERUSER", "CREATEROLE", "BYPASSRLS", "REPLICATION" };

    private static readonly HashSet<string> PostgreSqlCriticalDatabasePermissions = new(Names) { "OWNERSHIP", "ALL" };

    private static readonly HashSet<string> PostgreSqlGeneralPermissions = new(Names) { "SELECT", "CONNECT", "USAGE", "TEMPORARY", "TEMP", "LOGIN" };

    // MySQL and MariaDB
    private static readonly HashSet<string> MySqlCriticalGlobalPermissions = new(Names)
    {
        "ALL PRIVILEGES", "ALL", "SUPER", "GRANT OPTION", "CREATE USER", "FILE", "SHUTDOWN", "SYSTEM_USER",
    };

    private static readonly HashSet<string> MySqlCriticalDatabasePermissions = new(Names) { "ALL PRIVILEGES", "ALL" };

    private static readonly HashSet<string> MySqlGeneralPermissions = new(Names) { "SELECT", "SHOW VIEW", "USAGE", "SHOW DATABASES" };

    /// <summary>Risk of the account itself (not of its roles or permissions).</summary>
    public static RiskLevel ForAccount(SqlEngine engine, string name, string accountType)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(accountType);
        if ((engine == SqlEngine.SqlServer && Names.Equals(name, "sa") && accountType == "sql-login")
            || (engine is SqlEngine.MySql or SqlEngine.MariaDb && Names.Equals(name, "root")))
        {
            return RiskLevel.Critical;
        }

        if (engine is SqlEngine.MySql or SqlEngine.MariaDb && name.Length == 0)
        {
            return RiskLevel.High;
        }

        return IsRole(accountType) && ListedRole(engine, name) is { } roleRisk ? roleRisk : RiskLevel.General;
    }

    /// <summary>Risk of being a member of the role <paramref name="roleName"/>.</summary>
    public static RiskLevel ForMembership(SqlEngine engine, string roleName)
    {
        ArgumentNullException.ThrowIfNull(roleName);
        return ListedRole(engine, roleName) ?? RiskLevel.High;
    }

    public static RiskLevel ForPermission(SqlEngine engine, SqlSecurableClass securableClass, string permission, SqlPermissionState state)
    {
        ArgumentNullException.ThrowIfNull(permission);
        if (state == SqlPermissionState.Deny)
        {
            return RiskLevel.Denied;
        }

        var risk = engine switch
        {
            SqlEngine.SqlServer => SqlServerPermission(securableClass, permission),
            SqlEngine.PostgreSql => PostgreSqlPermission(securableClass, permission),
            _ => MySqlPermission(securableClass, permission),
        };

        return state == SqlPermissionState.GrantWithGrantOption && risk < RiskLevel.High ? RiskLevel.High : risk;
    }

    public static bool IsRole(string accountType) =>
        accountType is "server-role" or "database-role" or "application-role" or "mysql-role";

    private static RiskLevel? ListedRole(SqlEngine engine, string roleName) => engine switch
    {
        SqlEngine.SqlServer when SqlServerCriticalRoles.Contains(roleName) => RiskLevel.Critical,
        SqlEngine.SqlServer when SqlServerHighRoles.Contains(roleName) => RiskLevel.High,
        SqlEngine.SqlServer when SqlServerGeneralRoles.Contains(roleName) => RiskLevel.General,
        SqlEngine.PostgreSql when PostgreSqlCriticalRoles.Contains(roleName) => RiskLevel.Critical,
        SqlEngine.PostgreSql when PostgreSqlHighRoles.Contains(roleName) => RiskLevel.High,
        _ => null,
    };

    private static RiskLevel SqlServerPermission(SqlSecurableClass securableClass, string permission) => securableClass switch
    {
        SqlSecurableClass.Server when SqlServerCriticalServerPermissions.Contains(permission) => RiskLevel.Critical,
        SqlSecurableClass.Database when SqlServerCriticalDatabasePermissions.Contains(permission) => RiskLevel.Critical,
        _ when SqlServerGeneralPermissions.Contains(permission) => RiskLevel.General,
        _ => RiskLevel.High,
    };

    private static RiskLevel PostgreSqlPermission(SqlSecurableClass securableClass, string permission) => securableClass switch
    {
        SqlSecurableClass.Server when PostgreSqlCriticalServerPermissions.Contains(permission) => RiskLevel.Critical,
        SqlSecurableClass.Database when PostgreSqlCriticalDatabasePermissions.Contains(permission) => RiskLevel.Critical,
        _ when PostgreSqlGeneralPermissions.Contains(permission) => RiskLevel.General,
        _ => RiskLevel.High,
    };

    private static RiskLevel MySqlPermission(SqlSecurableClass securableClass, string permission) => securableClass switch
    {
        SqlSecurableClass.Server when MySqlCriticalGlobalPermissions.Contains(permission) => RiskLevel.Critical,
        SqlSecurableClass.Database when MySqlCriticalDatabasePermissions.Contains(permission) => RiskLevel.Critical,
        _ when MySqlGeneralPermissions.Contains(permission) => RiskLevel.General,
        _ => RiskLevel.High,
    };
}

/// <summary>
/// Account risk (RF-14, P-24): own risk = the account and its current roles and permissions; inherited risk = the
/// effective risk of every role it is a member of, directly or through other roles. Cycles are tolerated.
/// </summary>
public static class SqlRiskPropagation
{
    public static IReadOnlyDictionary<Guid, (RiskLevel Own, RiskLevel Inherited)> Calculate(
        IReadOnlyDictionary<Guid, RiskLevel> accountRisks, IEnumerable<SqlRiskElement> currentElements)
    {
        ArgumentNullException.ThrowIfNull(accountRisks);
        ArgumentNullException.ThrowIfNull(currentElements);

        var own = new Dictionary<Guid, RiskLevel>(accountRisks);
        var roles = new Dictionary<Guid, List<Guid>>();
        foreach (var element in currentElements)
        {
            if (!own.TryGetValue(element.AccountId, out var current))
            {
                continue;
            }

            if (element.Risk > current)
            {
                own[element.AccountId] = element.Risk;
            }

            if (element.RoleAccountId is { } roleId && accountRisks.ContainsKey(roleId))
            {
                if (!roles.TryGetValue(element.AccountId, out var memberOf))
                {
                    roles[element.AccountId] = memberOf = [];
                }

                memberOf.Add(roleId);
            }
        }

        var result = new Dictionary<Guid, (RiskLevel, RiskLevel)>(own.Count);
        foreach (var accountId in own.Keys)
        {
            var inherited = RiskLevel.General;
            var visited = new HashSet<Guid> { accountId };
            var pending = new Stack<Guid>(roles.GetValueOrDefault(accountId) ?? []);
            while (pending.Count > 0 && inherited < RiskLevel.Critical)
            {
                var roleId = pending.Pop();
                if (!visited.Add(roleId))
                {
                    continue;
                }

                if (own[roleId] > inherited)
                {
                    inherited = own[roleId];
                }

                foreach (var parent in roles.GetValueOrDefault(roleId) ?? [])
                {
                    pending.Push(parent);
                }
            }

            result[accountId] = (own[accountId], inherited);
        }

        return result;
    }
}

/// <summary>A current element as seen by the risk propagation.</summary>
public readonly record struct SqlRiskElement(Guid AccountId, RiskLevel Risk, Guid? RoleAccountId);
