namespace Itmas3.Agent.Collectors.SqlAudit.MySql;

/// <summary>
/// Fixed, read-only catalog queries for MySQL 9 LTS and MariaDB 12 LTS (SPEC-0005 §10.2). Nothing is ever concatenated
/// into them. Authentication columns are never selected: in MySQL only the non-authentication columns of mysql.user; in
/// MariaDB only the access, role, lock and password-age keys of mysql.global_priv.Priv (never the whole JSON value).
/// </summary>
internal static class MySqlQueries
{
    public const string ReadOnlySession = "SET SESSION TRANSACTION READ ONLY";

    /// <summary>Timestamps (password_last_changed) are returned in UTC.</summary>
    public const string UtcSession = "SET time_zone = '+00:00'";

    public const string Instance = "SELECT VERSION() AS version, @@version_comment AS edition";

    public const string Databases = "SELECT SCHEMA_NAME AS name FROM information_schema.SCHEMATA ORDER BY SCHEMA_NAME";

    /// <summary>Static global privilege columns of MySQL 9 mysql.user (Grant_priv becomes GRANT OPTION).</summary>
    public static readonly string[] MySqlUserPrivilegeColumns =
    [
        "Select_priv", "Insert_priv", "Update_priv", "Delete_priv", "Create_priv", "Drop_priv", "Reload_priv", "Shutdown_priv",
        "Process_priv", "File_priv", "Grant_priv", "References_priv", "Index_priv", "Alter_priv", "Show_db_priv", "Super_priv",
        "Create_tmp_table_priv", "Lock_tables_priv", "Execute_priv", "Repl_slave_priv", "Repl_client_priv", "Create_view_priv",
        "Show_view_priv", "Create_routine_priv", "Alter_routine_priv", "Create_user_priv", "Event_priv", "Trigger_priv",
        "Create_tablespace_priv", "Create_role_priv", "Drop_role_priv",
    ];

    public const string MySqlAccounts =
        "SELECT User, Host, Select_priv, Insert_priv, Update_priv, Delete_priv, Create_priv, Drop_priv, Reload_priv, Shutdown_priv, "
        + "Process_priv, File_priv, Grant_priv, References_priv, Index_priv, Alter_priv, Show_db_priv, Super_priv, "
        + "Create_tmp_table_priv, Lock_tables_priv, Execute_priv, Repl_slave_priv, Repl_client_priv, Create_view_priv, "
        + "Show_view_priv, Create_routine_priv, Alter_routine_priv, Create_user_priv, Event_priv, Trigger_priv, "
        + "Create_tablespace_priv, Create_role_priv, Drop_role_priv, "
        + "account_locked, password_expired, password_last_changed, password_lifetime "
        + "FROM mysql.user";

    public const string MariaDbAccounts =
        "SELECT User, Host, "
        + "CAST(JSON_VALUE(Priv, '$.access') AS UNSIGNED) AS access, "
        + "JSON_VALUE(Priv, '$.is_role') AS is_role, "
        + "JSON_VALUE(Priv, '$.account_locked') AS account_locked, "
        + "CAST(JSON_VALUE(Priv, '$.password_last_changed') AS SIGNED) AS password_last_changed, "
        + "CAST(JSON_VALUE(Priv, '$.password_lifetime') AS SIGNED) AS password_lifetime "
        + "FROM mysql.global_priv";

    public const string MySqlRoleEdges = "SELECT TO_USER AS member_user, TO_HOST AS member_host, FROM_USER AS role_user, FROM_HOST AS role_host FROM mysql.role_edges";

    public const string MariaDbRoleEdges = "SELECT User AS member_user, Host AS member_host, Role AS role_user, '' AS role_host FROM mysql.roles_mapping";

    public const string MySqlDynamicGrants = "SELECT USER, HOST, PRIV, WITH_GRANT_OPTION FROM mysql.global_grants";

    public static readonly string[] MySqlDatabasePrivilegeColumns =
    [
        "Select_priv", "Insert_priv", "Update_priv", "Delete_priv", "Create_priv", "Drop_priv", "Grant_priv", "References_priv",
        "Index_priv", "Alter_priv", "Create_tmp_table_priv", "Lock_tables_priv", "Create_view_priv", "Show_view_priv",
        "Create_routine_priv", "Alter_routine_priv", "Execute_priv", "Event_priv", "Trigger_priv",
    ];

    public static readonly string[] MariaDbDatabasePrivilegeColumns = [.. MySqlDatabasePrivilegeColumns, "Delete_history_priv", "Show_create_routine_priv"];

    public const string MySqlDatabaseGrants =
        "SELECT User, Host, Db, Select_priv, Insert_priv, Update_priv, Delete_priv, Create_priv, Drop_priv, Grant_priv, "
        + "References_priv, Index_priv, Alter_priv, Create_tmp_table_priv, Lock_tables_priv, Create_view_priv, Show_view_priv, "
        + "Create_routine_priv, Alter_routine_priv, Execute_priv, Event_priv, Trigger_priv "
        + "FROM mysql.db";

    public const string MariaDbDatabaseGrants =
        "SELECT User, Host, Db, Select_priv, Insert_priv, Update_priv, Delete_priv, Create_priv, Drop_priv, Grant_priv, "
        + "References_priv, Index_priv, Alter_priv, Create_tmp_table_priv, Lock_tables_priv, Create_view_priv, Show_view_priv, "
        + "Create_routine_priv, Alter_routine_priv, Execute_priv, Event_priv, Trigger_priv, Delete_history_priv, Show_create_routine_priv "
        + "FROM mysql.db";

    public const string TableGrants = "SELECT User, Host, Db, Table_name, Grantor, Table_priv FROM mysql.tables_priv";

    public const string ColumnGrants = "SELECT User, Host, Db, Table_name, Column_name, Column_priv FROM mysql.columns_priv";

    public const string RoutineGrants = "SELECT User, Host, Db, Routine_name, CAST(Routine_type AS CHAR) AS Routine_type, Grantor, Proc_priv FROM mysql.procs_priv";

    // ---- sql test: privileges of the agent account itself (information_schema shows only its own rows) -----------------

    public const string OwnGlobalPrivileges =
        "SELECT PRIVILEGE_TYPE AS privilege FROM information_schema.USER_PRIVILEGES "
        + "WHERE GRANTEE = CONCAT('''', SUBSTRING_INDEX(CURRENT_USER(), '@', 1), '''@''', SUBSTRING_INDEX(CURRENT_USER(), '@', -1), '''')";

    public const string OwnSchemaPrivileges =
        "SELECT TABLE_SCHEMA AS schema_name, PRIVILEGE_TYPE AS privilege FROM information_schema.SCHEMA_PRIVILEGES "
        + "WHERE GRANTEE = CONCAT('''', SUBSTRING_INDEX(CURRENT_USER(), '@', 1), '''@''', SUBSTRING_INDEX(CURRENT_USER(), '@', -1), '''')";

    public const string OwnTablePrivileges =
        "SELECT TABLE_SCHEMA AS schema_name, TABLE_NAME AS table_name, PRIVILEGE_TYPE AS privilege FROM information_schema.TABLE_PRIVILEGES "
        + "WHERE GRANTEE = CONCAT('''', SUBSTRING_INDEX(CURRENT_USER(), '@', 1), '''@''', SUBSTRING_INDEX(CURRENT_USER(), '@', -1), '''')";

    public const string OwnColumnPrivileges =
        "SELECT TABLE_SCHEMA AS schema_name, TABLE_NAME AS table_name, PRIVILEGE_TYPE AS privilege FROM information_schema.COLUMN_PRIVILEGES "
        + "WHERE GRANTEE = CONCAT('''', SUBSTRING_INDEX(CURRENT_USER(), '@', 1), '''@''', SUBSTRING_INDEX(CURRENT_USER(), '@', -1), '''')";

    public const string OwnRoles = "SELECT COUNT(*) AS roles FROM information_schema.APPLICABLE_ROLES";
}
