using Itmas3.Agent.Collectors.SqlAudit.Model;

namespace Itmas3.Agent.Collectors.SqlAudit.PostgreSql;

// Raw rows of the PostgreSQL 18 catalog queries (PostgreSqlQueries). Plain records so PostgreSqlMapper can be tested with
// fixtures. Password columns (rolpassword, pg_authid) are never read.

public sealed record PostgreSqlRows(
    PostgreSqlInstanceRow Instance,
    IReadOnlyList<PostgreSqlRoleRow> Roles,
    IReadOnlyList<PostgreSqlMembershipRow> Memberships,
    IReadOnlyList<PostgreSqlDatabaseRow> Databases,
    IReadOnlyList<PostgreSqlAclRow> ClusterAcls,
    IReadOnlyList<PostgreSqlDatabaseCatalog> DatabaseCatalogs,
    IReadOnlyList<SqlUnavailableSection> Unavailable);

public sealed record PostgreSqlInstanceRow(string Version, int VersionNumber);

/// <param name="ValidUntil">VALID UNTIL when finite; null for no limit or 'infinity'.</param>
public sealed record PostgreSqlRoleRow(
    long Oid,
    string Name,
    bool Superuser,
    bool CreateRole,
    bool CreateDb,
    bool CanLogin,
    bool Replication,
    bool BypassRls,
    DateTime? ValidUntil);

public sealed record PostgreSqlMembershipRow(long RoleOid, long MemberOid);

public sealed record PostgreSqlDatabaseRow(string Name, long OwnerOid, bool AllowConnections);

/// <summary>
/// One exploded ACL entry (aclexplode), or one object without entries (<see cref="Grantee"/> null) so its owner is still
/// known. <see cref="Class"/> and <see cref="ObjectType"/> are already the contract values.
/// </summary>
/// <param name="Database">Filled by the engine for per-database rows; the database name for class "database".</param>
/// <param name="ParentOwnerOid">Owner of the object's schema: ownership equal to it is the default and is not reported.</param>
/// <param name="Grantee">0 is PUBLIC.</param>
/// <param name="IsSystem">Object created by initdb (OID below 16384): its ownership is the engine default.</param>
/// <param name="IsBuiltin">Entry of an engine object that is still as initdb left it (NULL ACL or listed in pg_init_privs).</param>
public sealed record PostgreSqlAclRow(
    string Class,
    string? Database,
    string? Schema,
    string? Name,
    string? Arguments,
    string? Column,
    string? ObjectType,
    long OwnerOid,
    long? ParentOwnerOid,
    long? Grantee,
    long? Grantor,
    string? Privilege,
    bool IsGrantable,
    bool IsSystem,
    bool IsBuiltin);

public sealed record PostgreSqlDatabaseCatalog(string Database, IReadOnlyList<PostgreSqlAclRow> Acls);
