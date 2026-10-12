namespace Itmas3.Agent.Collectors.SqlAudit.MySql;

// Raw rows of the MySQL 9 / MariaDB 12 catalog queries (MySqlQueries). Plain records so MySqlMapper can be tested with
// fixtures (tests/Itmas3.Agent.Tests/fixtures/sqlaudit/). Authentication columns are never part of them.

public sealed record MySqlRows(
    MySqlInstanceRow Instance,
    IReadOnlyList<string> Databases,
    IReadOnlyList<MySqlAccountRow> Accounts,
    IReadOnlyList<MySqlRoleEdgeRow> RoleEdges,
    IReadOnlyList<MySqlDynamicGrantRow> DynamicGrants,
    IReadOnlyList<MySqlDatabaseGrantRow> DatabaseGrants,
    IReadOnlyList<MySqlTableGrantRow> TableGrants,
    IReadOnlyList<MySqlColumnGrantRow> ColumnGrants,
    IReadOnlyList<MySqlRoutineGrantRow> RoutineGrants);

public sealed record MySqlInstanceRow(string Version, string? Edition);

/// <param name="GlobalPrivileges">MySQL: names of the <c>*_priv</c> columns of mysql.user set to 'Y'.</param>
/// <param name="Access">MariaDB: privilege bit mask of mysql.global_priv ($.access).</param>
/// <param name="IsRole">MariaDB: $.is_role. MySQL roles are detected from mysql.role_edges.</param>
/// <param name="PasswordExpired">MySQL password_expired; MariaDB password_last_changed = 0.</param>
/// <param name="PasswordLifetimeDays">Null: server default; 0: never expires.</param>
public sealed record MySqlAccountRow(
    string User,
    string Host,
    IReadOnlyList<string> GlobalPrivileges,
    ulong? Access,
    bool IsRole,
    bool? Locked,
    bool? PasswordExpired,
    DateTime? PasswordLastChanged,
    long? PasswordLifetimeDays);

public sealed record MySqlRoleEdgeRow(string MemberUser, string MemberHost, string RoleUser, string RoleHost);

public sealed record MySqlDynamicGrantRow(string User, string Host, string Privilege, bool WithGrantOption);

/// <param name="Privileges">Names of the <c>*_priv</c> columns of mysql.db set to 'Y'.</param>
public sealed record MySqlDatabaseGrantRow(string User, string Host, string Database, IReadOnlyList<string> Privileges);

/// <param name="Privileges">SET value of tables_priv.Table_priv, e.g. "Select,Insert,Grant".</param>
public sealed record MySqlTableGrantRow(string User, string Host, string Database, string Table, string? Grantor, string Privileges);

public sealed record MySqlColumnGrantRow(string User, string Host, string Database, string Table, string Column, string Privileges);

public sealed record MySqlRoutineGrantRow(string User, string Host, string Database, string Routine, string RoutineType, string? Grantor, string Privileges);
