namespace Itmas3.Agent.Collectors.SqlAudit.PostgreSql;

/// <summary>
/// Fixed, read-only catalog queries for PostgreSQL 18 (SPEC-0005 §10.2). They only read catalogs every role can read
/// (pg_roles without rolpassword, pg_auth_members, pg_database, ACL columns of the per-database catalogs, pg_init_privs),
/// so the agent account needs nothing but LOGIN and CONNECT. ACLs are expanded on the server with aclexplode; a NULL ACL
/// means the built-in default and is expanded with acldefault. Every ACL query returns the same columns
/// (<see cref="PostgreSqlAclRow"/>). Objects created by initdb have OIDs below 16384 (FirstNormalObjectId).
/// </summary>
internal static class PostgreSqlQueries
{
    public const string Instance =
        "SELECT pg_catalog.current_setting('server_version') AS version, pg_catalog.current_setting('server_version_num')::int AS version_number";

    public const string Roles =
        "SELECT r.oid::bigint AS oid, r.rolname::text AS name, r.rolsuper AS superuser, r.rolcreaterole AS create_role, "
        + "r.rolcreatedb AS create_db, r.rolcanlogin AS can_login, r.rolreplication AS replication, r.rolbypassrls AS bypass_rls, "
        + "CASE WHEN r.rolvaliduntil IS NOT NULL AND pg_catalog.isfinite(r.rolvaliduntil) THEN r.rolvaliduntil END AS valid_until "
        + "FROM pg_catalog.pg_roles AS r";

    public const string Memberships =
        "SELECT m.roleid::bigint AS role_oid, m.member::bigint AS member_oid FROM pg_catalog.pg_auth_members AS m";

    public const string Databases =
        "SELECT d.datname::text AS name, d.datdba::bigint AS owner_oid, d.datallowconn AS allow_connections "
        + "FROM pg_catalog.pg_database AS d ORDER BY d.datname";

    private const string Exploded =
        "a.grantee::bigint AS grantee, a.grantor::bigint AS grantor, a.privilege_type::text AS privilege, COALESCE(a.is_grantable, false) AS is_grantable";

    private const string ExcludedSchemas = "n.nspname !~ '^pg_(toast|temp_|toast_temp_)'";

    /// <summary>Databases (class database) and tablespaces (class server), shared catalogs.</summary>
    public const string ClusterAcls =
        "SELECT 'database' AS class, d.datname::text AS database_name, NULL::text AS schema_name, NULL::text AS name, NULL::text AS arguments, "
        + "NULL::text AS column_name, NULL::text AS object_type, d.datdba::bigint AS owner_oid, NULL::bigint AS parent_owner_oid, " + Exploded + ", "
        + "false AS is_system, false AS is_builtin "
        + "FROM pg_catalog.pg_database AS d "
        + "LEFT JOIN LATERAL pg_catalog.aclexplode(COALESCE(d.datacl, pg_catalog.acldefault('d', d.datdba))) AS a ON true "
        + "UNION ALL "
        + "SELECT 'server', NULL, NULL, t.spcname::text, NULL, NULL, 'tablespace', t.spcowner::bigint, NULL, " + Exploded + ", "
        + "t.oid < 16384, false "
        + "FROM pg_catalog.pg_tablespace AS t "
        + "LEFT JOIN LATERAL pg_catalog.aclexplode(COALESCE(t.spcacl, pg_catalog.acldefault('t', t.spcowner))) AS a ON true";

    /// <summary>GRANT SET / ALTER SYSTEM on configuration parameters (PostgreSQL 15 and later).</summary>
    public const string ParameterAcls =
        "SELECT 'server' AS class, NULL::text AS database_name, NULL::text AS schema_name, p.parname::text AS name, NULL::text AS arguments, "
        + "NULL::text AS column_name, 'parameter' AS object_type, 0::bigint AS owner_oid, NULL::bigint AS parent_owner_oid, " + Exploded + ", "
        + "false AS is_system, false AS is_builtin "
        + "FROM pg_catalog.pg_parameter_acl AS p CROSS JOIN LATERAL pg_catalog.aclexplode(p.paracl) AS a";

    /// <summary>
    /// An entry of an initdb object is built-in when the ACL is the default (NULL), when initdb recorded it in
    /// pg_init_privs, or when initdb recorded nothing for that object (information_schema is granted after the
    /// pg_init_privs snapshot, so its grants have no record to compare with).
    /// </summary>
    private static string Builtin(string catalog, string oid, string acl, string subId = "0") =>
        $"({oid} < 16384 AND ({acl} IS NULL "
        + $"OR NOT EXISTS (SELECT 1 FROM pg_catalog.pg_init_privs AS ip0 WHERE ip0.classoid = 'pg_catalog.{catalog}'::regclass AND ip0.objoid = {oid} AND ip0.objsubid = {subId}) "
        + "OR EXISTS (SELECT 1 FROM pg_catalog.pg_init_privs AS ip "
        + "CROSS JOIN LATERAL pg_catalog.aclexplode(ip.initprivs) AS ia "
        + $"WHERE ip.classoid = 'pg_catalog.{catalog}'::regclass AND ip.objoid = {oid} AND ip.objsubid = {subId} "
        + "AND ia.grantee = a.grantee AND ia.grantor = a.grantor AND ia.privilege_type = a.privilege_type AND ia.is_grantable = a.is_grantable)))";

    public static readonly string SchemaAcls =
        "SELECT 'schema' AS class, NULL::text AS database_name, n.nspname::text AS schema_name, NULL::text AS name, NULL::text AS arguments, "
        + "NULL::text AS column_name, NULL::text AS object_type, n.nspowner::bigint AS owner_oid, NULL::bigint AS parent_owner_oid, " + Exploded + ", "
        + "n.oid < 16384 AS is_system, " + Builtin("pg_namespace", "n.oid", "n.nspacl") + " AS is_builtin "
        + "FROM pg_catalog.pg_namespace AS n "
        + "LEFT JOIN LATERAL pg_catalog.aclexplode(COALESCE(n.nspacl, pg_catalog.acldefault('n', n.nspowner))) AS a ON true "
        + "WHERE " + ExcludedSchemas;

    private const string RelationType =
        "CASE c.relkind WHEN 'r' THEN 'table' WHEN 'p' THEN 'table' WHEN 'v' THEN 'view' WHEN 'm' THEN 'materialized-view' "
        + "WHEN 'f' THEN 'foreign-table' WHEN 'S' THEN 'sequence' ELSE 'other' END";

    public static readonly string RelationAcls =
        "SELECT 'object' AS class, NULL::text AS database_name, n.nspname::text AS schema_name, c.relname::text AS name, NULL::text AS arguments, "
        + "NULL::text AS column_name, " + RelationType + " AS object_type, c.relowner::bigint AS owner_oid, n.nspowner::bigint AS parent_owner_oid, " + Exploded + ", "
        + "c.oid < 16384 AS is_system, " + Builtin("pg_class", "c.oid", "c.relacl") + " AS is_builtin "
        + "FROM pg_catalog.pg_class AS c JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace "
        + "LEFT JOIN LATERAL pg_catalog.aclexplode(COALESCE(c.relacl, pg_catalog.acldefault(CASE WHEN c.relkind = 'S' THEN 's' ELSE 'r' END::\"char\", c.relowner))) AS a ON true "
        + "WHERE c.relkind IN ('r', 'p', 'v', 'm', 'f', 'S') AND " + ExcludedSchemas;

    public static readonly string ColumnAcls =
        "SELECT 'column' AS class, NULL::text AS database_name, n.nspname::text AS schema_name, c.relname::text AS name, NULL::text AS arguments, "
        + "att.attname::text AS column_name, " + RelationType + " AS object_type, c.relowner::bigint AS owner_oid, NULL::bigint AS parent_owner_oid, " + Exploded + ", "
        + "c.oid < 16384 AS is_system, " + Builtin("pg_class", "c.oid", "att.attacl", "att.attnum") + " AS is_builtin "
        + "FROM pg_catalog.pg_attribute AS att "
        + "JOIN pg_catalog.pg_class AS c ON c.oid = att.attrelid JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace "
        + "CROSS JOIN LATERAL pg_catalog.aclexplode(att.attacl) AS a "
        + "WHERE att.attacl IS NOT NULL AND att.attnum > 0 AND NOT att.attisdropped AND c.relkind IN ('r', 'p', 'v', 'm', 'f') AND " + ExcludedSchemas;

    public static readonly string RoutineAcls =
        "SELECT 'object' AS class, NULL::text AS database_name, n.nspname::text AS schema_name, p.proname::text AS name, "
        + "pg_catalog.pg_get_function_identity_arguments(p.oid) AS arguments, NULL::text AS column_name, "
        + "CASE p.prokind WHEN 'p' THEN 'procedure' WHEN 'a' THEN 'aggregate' ELSE 'function' END AS object_type, "
        + "p.proowner::bigint AS owner_oid, n.nspowner::bigint AS parent_owner_oid, " + Exploded + ", "
        + "p.oid < 16384 AS is_system, " + Builtin("pg_proc", "p.oid", "p.proacl") + " AS is_builtin "
        + "FROM pg_catalog.pg_proc AS p JOIN pg_catalog.pg_namespace AS n ON n.oid = p.pronamespace "
        + "LEFT JOIN LATERAL pg_catalog.aclexplode(COALESCE(p.proacl, pg_catalog.acldefault('f', p.proowner))) AS a ON true "
        + "WHERE " + ExcludedSchemas;

    /// <summary>Base, composite, enum, range and domain types; not array types nor the row types of tables.</summary>
    public static readonly string TypeAcls =
        "SELECT 'object' AS class, NULL::text AS database_name, n.nspname::text AS schema_name, t.typname::text AS name, NULL::text AS arguments, "
        + "NULL::text AS column_name, CASE t.typtype WHEN 'd' THEN 'domain' ELSE 'type' END AS object_type, "
        + "t.typowner::bigint AS owner_oid, n.nspowner::bigint AS parent_owner_oid, " + Exploded + ", "
        + "t.oid < 16384 AS is_system, " + Builtin("pg_type", "t.oid", "t.typacl") + " AS is_builtin "
        + "FROM pg_catalog.pg_type AS t JOIN pg_catalog.pg_namespace AS n ON n.oid = t.typnamespace "
        + "LEFT JOIN LATERAL pg_catalog.aclexplode(COALESCE(t.typacl, pg_catalog.acldefault('T', t.typowner))) AS a ON true "
        + "WHERE (t.typrelid = 0 OR EXISTS (SELECT 1 FROM pg_catalog.pg_class AS rc WHERE rc.oid = t.typrelid AND rc.relkind = 'c')) "
        + "AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_type AS el WHERE el.oid = t.typelem AND el.typarray = t.oid) AND " + ExcludedSchemas;

    /// <summary>Procedural languages, foreign-data wrappers and foreign servers (objects without a schema).</summary>
    public static readonly string OtherObjectAcls =
        "SELECT 'object' AS class, NULL::text AS database_name, NULL::text AS schema_name, l.lanname::text AS name, NULL::text AS arguments, "
        + "NULL::text AS column_name, 'language' AS object_type, l.lanowner::bigint AS owner_oid, NULL::bigint AS parent_owner_oid, " + Exploded + ", "
        + "l.oid < 16384 AS is_system, " + Builtin("pg_language", "l.oid", "l.lanacl") + " AS is_builtin "
        + "FROM pg_catalog.pg_language AS l "
        + "LEFT JOIN LATERAL pg_catalog.aclexplode(COALESCE(l.lanacl, pg_catalog.acldefault('l', l.lanowner))) AS a ON true "
        + "UNION ALL "
        + "SELECT 'object', NULL, NULL, w.fdwname::text, NULL, NULL, 'foreign-data-wrapper', w.fdwowner::bigint, NULL, " + Exploded + ", "
        + "w.oid < 16384, false "
        + "FROM pg_catalog.pg_foreign_data_wrapper AS w "
        + "LEFT JOIN LATERAL pg_catalog.aclexplode(COALESCE(w.fdwacl, pg_catalog.acldefault('F', w.fdwowner))) AS a ON true "
        + "UNION ALL "
        + "SELECT 'object', NULL, NULL, s.srvname::text, NULL, NULL, 'foreign-server', s.srvowner::bigint, NULL, " + Exploded + ", "
        + "s.oid < 16384, false "
        + "FROM pg_catalog.pg_foreign_server AS s "
        + "LEFT JOIN LATERAL pg_catalog.aclexplode(COALESCE(s.srvacl, pg_catalog.acldefault('S', s.srvowner))) AS a ON true";

    /// <summary>ALTER DEFAULT PRIVILEGES: the grantor of each entry is the role whose future objects receive it.</summary>
    public const string DefaultAcls =
        "SELECT 'default-privileges' AS class, NULL::text AS database_name, n.nspname::text AS schema_name, NULL::text AS name, NULL::text AS arguments, "
        + "NULL::text AS column_name, "
        + "CASE d.defaclobjtype WHEN 'r' THEN 'table' WHEN 'S' THEN 'sequence' WHEN 'f' THEN 'function' WHEN 'T' THEN 'type' "
        + "WHEN 'n' THEN 'schema' WHEN 'L' THEN 'large-object' ELSE 'other' END AS object_type, "
        + "d.defaclrole::bigint AS owner_oid, NULL::bigint AS parent_owner_oid, " + Exploded + ", "
        + "false AS is_system, false AS is_builtin "
        + "FROM pg_catalog.pg_default_acl AS d LEFT JOIN pg_catalog.pg_namespace AS n ON n.oid = d.defaclnamespace "
        + "CROSS JOIN LATERAL pg_catalog.aclexplode(d.defaclacl) AS a";

    /// <summary>Per-database queries, in order (all fixed text; the database is chosen by the connection, never by USE).</summary>
    public static IReadOnlyList<string> DatabaseAcls { get; } = [SchemaAcls, RelationAcls, ColumnAcls, RoutineAcls, TypeAcls, OtherObjectAcls, DefaultAcls];

    /// <summary>sql test: attributes, memberships and owned databases of the agent account (SPEC-0005 §10.3).</summary>
    public const string OwnPrivileges =
        "SELECT r.rolsuper AS superuser, r.rolcreaterole AS create_role, r.rolcreatedb AS create_db, r.rolreplication AS replication, "
        + "r.rolbypassrls AS bypass_rls, "
        + "(SELECT count(*) FROM pg_catalog.pg_auth_members AS m WHERE m.member = r.oid) AS role_count, "
        + "(SELECT count(*) FROM pg_catalog.pg_database AS d WHERE d.datdba = r.oid) AS owned_databases "
        + "FROM pg_catalog.pg_roles AS r WHERE r.rolname = current_user";
}
