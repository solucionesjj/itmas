using System.Globalization;
using Itmas3.Agent.Collectors.SqlAudit.Model;

namespace Itmas3.Agent.Collectors.SqlAudit.SqlServer;

/// <summary>
/// Pure mapping of the SQL Server catalog to the engine-neutral catalog (SPEC-0005 RF-4, RF-5): logins and server roles
/// (server section), users, roles and application roles of each database (its section), role memberships, permissions of
/// every class with their state (GRANT, GRANT WITH GRANT OPTION, DENY) and grantor, and non-default ownership as the
/// OWNERSHIP permission. Database users are linked to their login by SID; a SQL user whose login no longer exists is
/// orphaned. Grants to public on objects shipped with SQL Server are counted, not sent.
/// </summary>
public static class SqlServerMapper
{
    private const int DboPrincipalId = 1;

    public static SqlCatalog Map(SqlServerRows rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var offset = TimeSpan.FromMinutes(rows.Instance.TimeZoneOffsetMinutes);
        var loginsBySid = rows.ServerPrincipals
            .Where(principal => principal.Sid is not null && principal.Type != "R")
            .GroupBy(principal => principal.Sid!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => ServerKey(group.First().PrincipalId), StringComparer.Ordinal);

        var serverAccounts = rows.ServerPrincipals.Select(principal => ServerAccount(principal, offset)).ToList();
        var serverMemberships = rows.ServerRoleMembers
            .Select(member => new CatalogMembership(ServerKey(member.MemberId), ServerKey(member.RoleId)))
            .ToList();
        var serverPermissions = rows.ServerPermissions
            .Select(permission => Permission(permission, null, ServerKey))
            .OfType<CatalogPermission>()
            .ToList();
        var sections = new List<CatalogSection>
        {
            new(SqlSections.Server, serverAccounts, serverMemberships, serverPermissions, rows.ServerPermissions.Count(permission => permission.IsBuiltinPublic)),
        };

        foreach (var catalog in rows.DatabaseCatalogs)
        {
            var database = catalog.Database;
            string Key(int id) => DatabaseKey(database, id);

            var connectGranted = catalog.Permissions
                .Where(permission => permission.Class == 0 && permission.Permission == "CONNECT" && permission.State is "G" or "W")
                .Select(permission => permission.GranteeId)
                .ToHashSet();
            var accounts = catalog.Principals
                .Select(principal => DatabaseAccount(database, principal, offset, loginsBySid, connectGranted))
                .ToList();
            var memberships = catalog.RoleMembers.Select(member => new CatalogMembership(Key(member.MemberId), Key(member.RoleId))).ToList();
            var permissions = catalog.Permissions
                .Where(permission => !permission.IsBuiltinPublic)
                .Select(permission => Permission(permission, database, Key))
                .OfType<CatalogPermission>()
                .ToList();

            if (rows.Databases.FirstOrDefault(row => row.Name == database)?.OwnerSid is { } ownerSid && loginsBySid.TryGetValue(ownerSid, out var owner))
            {
                permissions.Add(new CatalogPermission(owner, Securables.Database(database), SqlPermissionNames.Ownership, SqlPermissionStates.Grant, null));
            }

            permissions.AddRange(catalog.Ownerships.Select(ownership => new CatalogPermission(
                Key(ownership.OwnerId),
                ownership.Kind == "schema" || ownership.ObjectName is null
                    ? Securables.Schema(database, ownership.SchemaName)
                    : Securables.Object(database, ownership.SchemaName, ownership.ObjectName, ObjectType(ownership.ObjectType)),
                SqlPermissionNames.Ownership,
                SqlPermissionStates.Grant,
                null)));

            sections.Add(new CatalogSection(
                SqlSections.Database(database),
                accounts,
                memberships,
                permissions,
                catalog.Permissions.Count(permission => permission.IsBuiltinPublic)));
        }

        var databases = rows.Databases
            .Select(database => new CatalogDatabase(
                database.Name,
                DatabaseState(database.State),
                database.OwnerSid is not null && loginsBySid.TryGetValue(database.OwnerSid, out var owner) ? owner : null))
            .ToList();

        return new SqlCatalog(
            new SqlInstanceInfo(
                rows.Instance.Version,
                rows.Instance.Edition,
                rows.Instance.IntegratedSecurityOnly ? SqlAuthenticationModes.Windows : SqlAuthenticationModes.Mixed),
            databases,
            sections,
            rows.Unavailable);
    }

    public static string ServerKey(int principalId) => "s:" + principalId.ToString(CultureInfo.InvariantCulture);

    public static string DatabaseKey(string database, int principalId) =>
        $"d:{database}:{principalId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>sys.databases.state_desc → contract state.</summary>
    public static string DatabaseState(string state) => state.ToUpperInvariant() switch
    {
        "ONLINE" => SqlDatabaseStates.Online,
        "OFFLINE" or "OFFLINE_SECONDARY" => SqlDatabaseStates.Offline,
        "RESTORING" or "COPYING" => SqlDatabaseStates.Restoring,
        "RECOVERING" or "RECOVERY_PENDING" => SqlDatabaseStates.Recovering,
        "SUSPECT" => SqlDatabaseStates.Suspect,
        "EMERGENCY" => SqlDatabaseStates.Emergency,
        _ => SqlDatabaseStates.Unknown,
    };

    private static CatalogAccount ServerAccount(SqlServerPrincipalRow principal, TimeSpan offset)
    {
        var type = principal.Type switch
        {
            "S" => SqlAccountTypes.SqlLogin,
            "U" => SqlAccountTypes.WindowsLogin,
            "G" => SqlAccountTypes.WindowsGroup,
            "R" => SqlAccountTypes.ServerRole,
            "E" or "X" => SqlAccountTypes.EntraLogin,
            "C" => SqlAccountTypes.Certificate,
            _ => SqlAccountTypes.Other,
        };
        var password = principal.Type == "S"
            ? new SqlPasswordInfo(principal.IsPolicyChecked, principal.IsExpirationChecked, principal.IsExpired, null)
            : null;
        return new CatalogAccount(
            ServerKey(principal.PrincipalId),
            SqlAccountLevels.Server,
            null,
            principal.Name,
            null,
            type,
            Enabled: !principal.IsDisabled,
            Locked: principal.IsLocked == true,
            CreatedAt: Date(principal.CreateDate, offset),
            ModifiedAt: Date(principal.ModifyDate, offset),
            DefaultDatabase: principal.Type == "R" ? null : principal.DefaultDatabase,
            DefaultSchema: null,
            LoginKey: null,
            IsOrphaned: false,
            Password: password);
    }

    private static CatalogAccount DatabaseAccount(
        string database, SqlServerPrincipalRow principal, TimeSpan offset, Dictionary<string, string> loginsBySid, HashSet<int> connectGranted)
    {
        var isUser = principal.Type is not ("R" or "A");
        var login = isUser && principal.Sid is not null ? loginsBySid.GetValueOrDefault(principal.Sid) : null;
        var type = principal.Type switch
        {
            "R" => SqlAccountTypes.DatabaseRole,
            "A" => SqlAccountTypes.ApplicationRole,
            "C" => SqlAccountTypes.Certificate,
            "K" => SqlAccountTypes.Other,
            "S" when principal.AuthenticationType == 0 => SqlAccountTypes.UserWithoutLogin,
            _ => SqlAccountTypes.DatabaseUser,
        };
        return new CatalogAccount(
            DatabaseKey(database, principal.PrincipalId),
            SqlAccountLevels.Database,
            database,
            principal.Name,
            null,
            type,
            // A user can enter the database only while it holds CONNECT (REVOKE CONNECT disables it, e.g. guest).
            Enabled: !isUser || principal.PrincipalId == DboPrincipalId || connectGranted.Contains(principal.PrincipalId),
            Locked: false,
            CreatedAt: Date(principal.CreateDate, offset),
            ModifiedAt: Date(principal.ModifyDate, offset),
            DefaultDatabase: null,
            DefaultSchema: principal.Type == "R" ? null : principal.DefaultSchema,
            LoginKey: login,
            // A SQL user mapped to a login (authentication type INSTANCE) whose login no longer exists.
            IsOrphaned: principal.Type == "S" && principal.AuthenticationType == 1 && login is null,
            Password: null);
    }

    private static CatalogPermission? Permission(SqlServerPermissionRow row, string? database, Func<int, string> key)
    {
        var state = row.State switch
        {
            "G" => SqlPermissionStates.Grant,
            "W" => SqlPermissionStates.GrantWithGrantOption,
            "D" => SqlPermissionStates.Deny,
            _ => null, // R: column-level revoke of a table grant; not a grant, deny or ownership.
        };
        var securable = Securable(row, database);
        return state is null || securable is null
            ? null
            : new CatalogPermission(key(row.GranteeId), securable, row.Permission, state, key(row.GrantorId));
    }

    private static SqlSecurableInfo? Securable(SqlServerPermissionRow row, string? database)
    {
        if (database is null)
        {
            return row.Class switch
            {
                100 => Securables.Server(),
                101 when row.ObjectName is not null => Securables.ServerObject(row.ObjectName, row.ObjectType == "R" ? SqlObjectTypes.ServerRole : SqlObjectTypes.Login),
                105 when row.ObjectName is not null => Securables.ServerObject(row.ObjectName, SqlObjectTypes.Endpoint),
                108 when row.ObjectName is not null => Securables.ServerObject(row.ObjectName, SqlObjectTypes.AvailabilityGroup),
                _ => null,
            };
        }

        return row.Class switch
        {
            0 => Securables.Database(database),
            1 when row.ObjectName is not null && row.ColumnName is not null =>
                Securables.Column(database, row.SchemaName, row.ObjectName, ObjectType(row.ObjectType), row.ColumnName),
            1 when row.ObjectName is not null => Securables.Object(database, row.SchemaName, row.ObjectName, ObjectType(row.ObjectType)),
            3 when row.SchemaName is not null => Securables.Schema(database, row.SchemaName),
            4 when row.ObjectName is not null => Securables.Object(database, null, row.ObjectName, row.ObjectType switch
            {
                "R" => SqlObjectTypes.DatabaseRole,
                "A" => SqlObjectTypes.ApplicationRole,
                _ => SqlObjectTypes.User,
            }),
            5 or 6 or 10 or 15 or 16 or 17 or 18 or 19 or 23 or 24 or 25 or 26 when row.ObjectName is not null =>
                Securables.Object(database, row.SchemaName, row.ObjectName, ObjectType(row.ObjectType)),
            _ => null,
        };
    }

    /// <summary>sys.objects.type (or the pseudo codes of the permissions query) → contract object type.</summary>
    public static string ObjectType(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "U" or "S" or "IT" or "ET" => SqlObjectTypes.Table,
        "V" => SqlObjectTypes.View,
        "P" or "PC" or "X" or "RF" => SqlObjectTypes.Procedure,
        "FN" or "IF" or "TF" or "FS" or "FT" => SqlObjectTypes.Function,
        "AF" => SqlObjectTypes.Aggregate,
        "SO" => SqlObjectTypes.Sequence,
        "SN" => SqlObjectTypes.Synonym,
        "TT" or "TYPE" => SqlObjectTypes.Type,
        "SQ" or "SB" => SqlObjectTypes.ServiceBroker,
        "ASM" => SqlObjectTypes.Assembly,
        "XSC" => SqlObjectTypes.XmlSchemaCollection,
        "FTC" => SqlObjectTypes.FullTextCatalog,
        "SK" => SqlObjectTypes.SymmetricKey,
        "AK" => SqlObjectTypes.AsymmetricKey,
        "CERT" => SqlObjectTypes.Certificate,
        _ => SqlObjectTypes.Other,
    };

    /// <summary>Catalog dates are local server times: the offset of the server clock turns them into instants.</summary>
    private static DateTimeOffset? Date(DateTime? value, TimeSpan offset) =>
        value is { } date ? new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), offset).ToUniversalTime() : null;
}
