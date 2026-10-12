namespace Itmas3.Agent.Collectors.SqlAudit.SqlServer;

/// <summary>
/// Fixed, read-only catalog queries for SQL Server 2019, 2022 and 2025 (SPEC-0005 §10.2). Nothing is ever concatenated
/// into them; each database is read through its own connection (Initial Catalog), never with USE. Password hashes are
/// never selected (sys.sql_logins is only joined for the policy flags). The agent login needs CONNECT ANY DATABASE and
/// VIEW ANY DEFINITION (SPEC-0005 §10.3).
/// </summary>
internal static class SqlServerQueries
{
    // Names from different catalog views (e.g. sys.all_objects of the resource database) may have different collations:
    // every CASE/UNION branch is converted to the database default (COLLATE DATABASE_DEFAULT) so they can be combined.

    /// <summary>Lowest priority for the engine's deadlock resolution and a bounded wait for metadata locks.</summary>
    public const string Session = "SET DEADLOCK_PRIORITY LOW; SET LOCK_TIMEOUT 30000;";

    public const string Instance =
        "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS version, "
        + "CAST(SERVERPROPERTY('Edition') AS nvarchar(128)) AS edition, "
        + "CAST(SERVERPROPERTY('IsIntegratedSecurityOnly') AS int) AS integrated_only, "
        + "DATEPART(TZOFFSET, SYSDATETIMEOFFSET()) AS tz_offset";

    public const string ServerPrincipals =
        "SELECT p.principal_id, p.name, p.type, p.sid, p.is_disabled, p.create_date, p.modify_date, p.default_database_name, "
        + "CAST(NULL AS sysname) AS default_schema_name, CAST(NULL AS int) AS authentication_type, "
        + "l.is_policy_checked, l.is_expiration_checked, "
        + "CASE WHEN p.type = 'S' THEN CAST(LOGINPROPERTY(p.name, 'IsExpired') AS int) END AS is_expired, "
        + "CASE WHEN p.type = 'S' THEN CAST(LOGINPROPERTY(p.name, 'IsLocked') AS int) END AS is_locked "
        + "FROM sys.server_principals AS p "
        + "LEFT JOIN sys.sql_logins AS l ON l.principal_id = p.principal_id "
        + "WHERE p.type IN ('S', 'U', 'G', 'R', 'C', 'K', 'E', 'X')";

    public const string ServerRoleMembers =
        "SELECT role_principal_id AS role_id, member_principal_id AS member_id FROM sys.server_role_members";

    /// <summary>Server, login/server role (101), endpoint (105) and availability group (108) permissions.</summary>
    public const string ServerPermissions =
        "SELECT sp.class, sp.grantee_principal_id AS grantee_id, sp.grantor_principal_id AS grantor_id, sp.permission_name, sp.state, "
        + "CAST(NULL AS sysname) AS schema_name, "
        + "CASE sp.class WHEN 101 THEN pr.name COLLATE DATABASE_DEFAULT WHEN 105 THEN ep.name COLLATE DATABASE_DEFAULT WHEN 108 THEN ag.name COLLATE DATABASE_DEFAULT END AS object_name, "
        + "CASE sp.class WHEN 101 THEN pr.type COLLATE DATABASE_DEFAULT WHEN 105 THEN 'EP' WHEN 108 THEN 'AG' END AS object_type, "
        + "CAST(NULL AS sysname) AS column_name, CAST(0 AS bit) AS is_builtin_public "
        + "FROM sys.server_permissions AS sp "
        + "LEFT JOIN sys.server_principals AS pr ON sp.class = 101 AND pr.principal_id = sp.major_id "
        + "LEFT JOIN sys.endpoints AS ep ON sp.class = 105 AND ep.endpoint_id = sp.major_id "
        + "LEFT JOIN sys.availability_replicas AS ar ON sp.class = 108 AND ar.replica_metadata_id = sp.major_id "
        + "LEFT JOIN sys.availability_groups AS ag ON ag.group_id = ar.group_id";

    public const string Databases =
        "SELECT d.name, d.state_desc AS state, d.owner_sid FROM sys.databases AS d ORDER BY d.name";

    // ---- Per database (connection opened on that database) ----------------------------------------------------------

    /// <summary>Users, roles and application roles; the internal INFORMATION_SCHEMA (3) and sys (4) users are skipped.</summary>
    public const string DatabasePrincipals =
        "SELECT dp.principal_id, dp.name, dp.type, dp.sid, CAST(0 AS bit) AS is_disabled, dp.create_date, dp.modify_date, "
        + "CAST(NULL AS sysname) AS default_database_name, dp.default_schema_name, dp.authentication_type, "
        + "CAST(NULL AS bit) AS is_policy_checked, CAST(NULL AS bit) AS is_expiration_checked, "
        + "CAST(NULL AS int) AS is_expired, CAST(NULL AS int) AS is_locked "
        + "FROM sys.database_principals AS dp "
        + "WHERE dp.type IN ('S', 'U', 'G', 'R', 'A', 'C', 'K', 'E', 'X') AND dp.principal_id NOT IN (3, 4)";

    public const string DatabaseRoleMembers =
        "SELECT role_principal_id AS role_id, member_principal_id AS member_id FROM sys.database_role_members";

    /// <summary>
    /// Database, object, column, schema, principal, assembly, type, XML schema collection, Service Broker, full-text
    /// catalog, key and certificate permissions, with the securable resolved. Grants to public on objects shipped with
    /// SQL Server (and on the built-in sys XML schema collection) are flagged so they can be omitted (RF-5).
    /// </summary>
    public const string DatabasePermissions =
        "SELECT p.class, p.grantee_principal_id AS grantee_id, p.grantor_principal_id AS grantor_id, p.permission_name, p.state, "
        + "CASE p.class WHEN 1 THEN os.name COLLATE DATABASE_DEFAULT WHEN 3 THEN s.name COLLATE DATABASE_DEFAULT WHEN 6 THEN ts.name COLLATE DATABASE_DEFAULT WHEN 10 THEN xs.name COLLATE DATABASE_DEFAULT END AS schema_name, "
        + "CASE p.class WHEN 1 THEN o.name COLLATE DATABASE_DEFAULT WHEN 4 THEN dp.name COLLATE DATABASE_DEFAULT WHEN 5 THEN asm.name COLLATE DATABASE_DEFAULT WHEN 6 THEN t.name COLLATE DATABASE_DEFAULT WHEN 10 THEN x.name COLLATE DATABASE_DEFAULT "
        + "WHEN 15 THEN mt.name COLLATE DATABASE_DEFAULT WHEN 16 THEN sc.name COLLATE DATABASE_DEFAULT WHEN 17 THEN sv.name COLLATE DATABASE_DEFAULT WHEN 18 THEN rsb.name COLLATE DATABASE_DEFAULT WHEN 19 THEN rt.name COLLATE DATABASE_DEFAULT "
        + "WHEN 23 THEN fc.name COLLATE DATABASE_DEFAULT WHEN 24 THEN sk.name COLLATE DATABASE_DEFAULT WHEN 25 THEN ce.name COLLATE DATABASE_DEFAULT WHEN 26 THEN ak.name COLLATE DATABASE_DEFAULT END AS object_name, "
        + "CASE p.class WHEN 1 THEN RTRIM(o.type) COLLATE DATABASE_DEFAULT WHEN 4 THEN dp.type COLLATE DATABASE_DEFAULT WHEN 5 THEN 'ASM' WHEN 6 THEN 'TYPE' WHEN 10 THEN 'XSC' "
        + "WHEN 15 THEN 'SB' WHEN 16 THEN 'SB' WHEN 17 THEN 'SB' WHEN 18 THEN 'SB' WHEN 19 THEN 'SB' "
        + "WHEN 23 THEN 'FTC' WHEN 24 THEN 'SK' WHEN 25 THEN 'CERT' WHEN 26 THEN 'AK' END AS object_type, "
        + "CASE WHEN p.class = 1 AND p.minor_id > 0 THEN c.name COLLATE DATABASE_DEFAULT END AS column_name, "
        + "CAST(CASE WHEN p.grantee_principal_id = 0 AND p.state IN ('G', 'W') "
        + "AND ((p.class = 1 AND o.is_ms_shipped = 1) OR (p.class = 10 AND x.xml_collection_id = 1)) THEN 1 ELSE 0 END AS bit) AS is_builtin_public "
        + "FROM sys.database_permissions AS p "
        + "LEFT JOIN sys.all_objects AS o ON p.class = 1 AND o.object_id = p.major_id "
        + "LEFT JOIN sys.schemas AS os ON os.schema_id = o.schema_id "
        + "LEFT JOIN sys.all_columns AS c ON p.class = 1 AND p.minor_id > 0 AND c.object_id = p.major_id AND c.column_id = p.minor_id "
        + "LEFT JOIN sys.schemas AS s ON p.class = 3 AND s.schema_id = p.major_id "
        + "LEFT JOIN sys.database_principals AS dp ON p.class = 4 AND dp.principal_id = p.major_id "
        + "LEFT JOIN sys.assemblies AS asm ON p.class = 5 AND asm.assembly_id = p.major_id "
        + "LEFT JOIN sys.types AS t ON p.class = 6 AND t.user_type_id = p.major_id "
        + "LEFT JOIN sys.schemas AS ts ON ts.schema_id = t.schema_id "
        + "LEFT JOIN sys.xml_schema_collections AS x ON p.class = 10 AND x.xml_collection_id = p.major_id "
        + "LEFT JOIN sys.schemas AS xs ON xs.schema_id = x.schema_id "
        + "LEFT JOIN sys.service_message_types AS mt ON p.class = 15 AND mt.message_type_id = p.major_id "
        + "LEFT JOIN sys.service_contracts AS sc ON p.class = 16 AND sc.service_contract_id = p.major_id "
        + "LEFT JOIN sys.services AS sv ON p.class = 17 AND sv.service_id = p.major_id "
        + "LEFT JOIN sys.remote_service_bindings AS rsb ON p.class = 18 AND rsb.remote_service_binding_id = p.major_id "
        + "LEFT JOIN sys.routes AS rt ON p.class = 19 AND rt.route_id = p.major_id "
        + "LEFT JOIN sys.fulltext_catalogs AS fc ON p.class = 23 AND fc.fulltext_catalog_id = p.major_id "
        + "LEFT JOIN sys.symmetric_keys AS sk ON p.class = 24 AND sk.symmetric_key_id = p.major_id "
        + "LEFT JOIN sys.certificates AS ce ON p.class = 25 AND ce.certificate_id = p.major_id "
        + "LEFT JOIN sys.asymmetric_keys AS ak ON p.class = 26 AND ak.asymmetric_key_id = p.major_id";

    /// <summary>
    /// Owners that are not the default: user schemas and built-in schemas whose owner changed (built-in schemas are owned
    /// by the principal with their own id), and user objects with an explicit owner (NULL means the schema owner).
    /// </summary>
    public const string Ownerships =
        "SELECT 'schema' AS kind, s.name COLLATE DATABASE_DEFAULT AS schema_name, CAST(NULL AS sysname) AS object_name, CAST(NULL AS varchar(2)) AS object_type, "
        + "s.principal_id AS owner_id "
        + "FROM sys.schemas AS s WHERE NOT ((s.schema_id <= 4 OR s.schema_id >= 16384) AND s.principal_id = s.schema_id) "
        + "UNION ALL "
        + "SELECT 'object', s.name COLLATE DATABASE_DEFAULT, o.name COLLATE DATABASE_DEFAULT, RTRIM(o.type) COLLATE DATABASE_DEFAULT, o.principal_id "
        + "FROM sys.objects AS o JOIN sys.schemas AS s ON s.schema_id = o.schema_id "
        + "WHERE o.principal_id IS NOT NULL AND o.is_ms_shipped = 0";

    /// <summary>sql test: effective server permissions of the agent login (SPEC-0005 §10.3).</summary>
    public const string OwnServerPermissions = "SELECT permission_name FROM sys.fn_my_permissions(NULL, 'SERVER')";
}
