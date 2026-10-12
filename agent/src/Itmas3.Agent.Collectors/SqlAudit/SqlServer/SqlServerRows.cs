using Itmas3.Agent.Collectors.SqlAudit.Model;

namespace Itmas3.Agent.Collectors.SqlAudit.SqlServer;

// Raw rows of the SQL Server 2019/2022/2025 catalog queries (SqlServerQueries). Plain records so SqlServerMapper can be
// tested with fixtures. sys.sql_logins.password_hash and LOGINPROPERTY(…, 'PasswordHash') are never read; SIDs are only
// used, as hexadecimal, to join database users with their logins and are not sent.

public sealed record SqlServerRows(
    SqlServerInstanceRow Instance,
    IReadOnlyList<SqlServerPrincipalRow> ServerPrincipals,
    IReadOnlyList<SqlServerRoleMemberRow> ServerRoleMembers,
    IReadOnlyList<SqlServerPermissionRow> ServerPermissions,
    IReadOnlyList<SqlServerDatabaseRow> Databases,
    IReadOnlyList<SqlServerDatabaseCatalog> DatabaseCatalogs,
    IReadOnlyList<SqlUnavailableSection> Unavailable);

/// <param name="TimeZoneOffsetMinutes">Offset of the server clock: catalog dates are local server times.</param>
public sealed record SqlServerInstanceRow(string Version, string? Edition, bool IntegratedSecurityOnly, int TimeZoneOffsetMinutes);

/// <param name="Type">sys.*_principals.type: S, U, G, R, C, K, E, X (server); S, U, G, R, A, C, K, E, X (database).</param>
/// <param name="AuthenticationType">sys.database_principals.authentication_type: 0 none, 1 instance, 2 database, 3 Windows, 4 external.</param>
public sealed record SqlServerPrincipalRow(
    int PrincipalId,
    string Name,
    string Type,
    string? Sid,
    bool IsDisabled,
    DateTime? CreateDate,
    DateTime? ModifyDate,
    string? DefaultDatabase,
    string? DefaultSchema,
    int? AuthenticationType,
    bool? IsPolicyChecked,
    bool? IsExpirationChecked,
    bool? IsExpired,
    bool? IsLocked);

public sealed record SqlServerRoleMemberRow(int RoleId, int MemberId);

/// <summary>
/// A row of sys.server_permissions or sys.database_permissions with its securable already resolved by the query:
/// <see cref="ObjectName"/>/<see cref="SchemaName"/>/<see cref="ColumnName"/> for its class and the sys.objects or
/// sys.*_principals type code in <see cref="ObjectType"/>.
/// </summary>
/// <param name="Class">sys.*_permissions.class (0 database, 1 object/column, 3 schema, 4 principal, 100 server, 101 login…).</param>
/// <param name="State">G grant, W grant with grant option, D deny, R revoke.</param>
/// <param name="IsBuiltinPublic">Granted to public on an object shipped with SQL Server (is_ms_shipped, RF-5).</param>
public sealed record SqlServerPermissionRow(
    int Class,
    int GranteeId,
    int GrantorId,
    string Permission,
    string State,
    string? SchemaName,
    string? ObjectName,
    string? ObjectType,
    string? ColumnName,
    bool IsBuiltinPublic);

/// <param name="State">sys.databases.state_desc.</param>
public sealed record SqlServerDatabaseRow(string Name, string State, string? OwnerSid);

/// <param name="Kind">"schema" or "object".</param>
public sealed record SqlServerOwnershipRow(string Kind, string SchemaName, string? ObjectName, string? ObjectType, int OwnerId);

public sealed record SqlServerDatabaseCatalog(
    string Database,
    IReadOnlyList<SqlServerPrincipalRow> Principals,
    IReadOnlyList<SqlServerRoleMemberRow> RoleMembers,
    IReadOnlyList<SqlServerPermissionRow> Permissions,
    IReadOnlyList<SqlServerOwnershipRow> Ownerships);
