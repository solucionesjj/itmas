using System.Globalization;
using Itmas3.Agent.Collectors.SqlAudit.Model;

namespace Itmas3.Agent.Collectors.SqlAudit.PostgreSql;

/// <summary>
/// Pure mapping of the PostgreSQL catalogs to the engine-neutral catalog (SPEC-0005 RF-4, RF-5).
/// <list type="bullet">
/// <item>Every role is a server account (<c>pg-role</c>); PUBLIC is a pseudo-role added only when something is granted to it.</item>
/// <item>Role attributes are server permissions; ACL entries are permissions of their object, one per privilege, or a
/// single <c>ALL</c> when the grantee holds every privilege of that object type with the same grantor and grant option.</item>
/// <item>The owner's own implicit entries are represented by <c>OWNERSHIP</c> instead. Ownership is reported for every
/// database and for schemas and objects whose owner is not the default (initdb objects, or the owner of the schema).</item>
/// <item>Entries granted to PUBLIC on initdb objects that are still as initdb left them are omitted and counted (RF-5).</item>
/// </list>
/// </summary>
public static class PostgreSqlMapper
{
    private const long PublicOid = 0;
    private const string All = "ALL";

    private static readonly string[] RelationPrivileges = ["SELECT", "INSERT", "UPDATE", "DELETE", "TRUNCATE", "REFERENCES", "TRIGGER", "MAINTAIN"];

    /// <summary>Privileges that exist for each object type (PostgreSQL 18), used to collapse a full set into ALL.</summary>
    private static readonly Dictionary<string, string[]> PrivilegesByType = new(StringComparer.Ordinal)
    {
        [SqlObjectTypes.Table] = RelationPrivileges,
        [SqlObjectTypes.View] = RelationPrivileges,
        [SqlObjectTypes.MaterializedView] = RelationPrivileges,
        [SqlObjectTypes.ForeignTable] = RelationPrivileges,
        [SqlObjectTypes.Sequence] = ["USAGE", "SELECT", "UPDATE"],
        [SqlObjectTypes.Schema] = ["USAGE", "CREATE"],
        [SqlObjectTypes.Database] = ["CREATE", "TEMPORARY", "CONNECT"],
        [SqlObjectTypes.Parameter] = ["SET", "ALTER SYSTEM"],
        [SqlObjectTypes.LargeObject] = ["SELECT", "UPDATE"],
    };

    private static readonly string[] ColumnPrivileges = ["SELECT", "INSERT", "UPDATE", "REFERENCES"];

    public static SqlCatalog Map(PostgreSqlRows rows, DateTimeOffset collectedAt)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var accounts = rows.Roles.Select(role => Account(role, collectedAt)).ToList();
        var memberships = rows.Memberships.Select(row => new CatalogMembership(Key(row.MemberOid), Key(row.RoleOid))).ToList();
        var serverPermissions = rows.Roles.SelectMany(RoleAttributes).ToList();

        var usesPublic = false;
        var serverAcls = rows.ClusterAcls.Where(row => row.Class == SqlSecurableClasses.Server).ToList();
        serverPermissions.AddRange(Permissions(serverAcls, null, ref usesPublic, out var serverOmitted));

        var sections = new List<CatalogSection>();
        var unavailable = rows.Unavailable.Select(item => item.Section).ToHashSet(StringComparer.Ordinal);
        foreach (var database in rows.Databases)
        {
            if (unavailable.Contains(SqlSections.Database(database.Name)))
            {
                continue;
            }

            var acls = rows.ClusterAcls.Where(row => row.Class == SqlSecurableClasses.Database && row.Database == database.Name)
                .Concat(rows.DatabaseCatalogs.FirstOrDefault(catalog => catalog.Database == database.Name)?.Acls ?? []);
            var permissions = Permissions(acls, database.Name, ref usesPublic, out var omitted);
            sections.Add(new CatalogSection(SqlSections.Database(database.Name), [], [], permissions, omitted));
        }

        if (usesPublic)
        {
            accounts.Add(new CatalogAccount(Key(PublicOid), SqlAccountLevels.Server, null, "PUBLIC", null, SqlAccountTypes.PgRole,
                true, false, null, null, null, null, null, false, null));
        }

        sections.Insert(0, new CatalogSection(SqlSections.Server, accounts, memberships, serverPermissions, serverOmitted));
        return new SqlCatalog(
            new SqlInstanceInfo(rows.Instance.Version, null, SqlAuthenticationModes.Password),
            rows.Databases.Select(database => new CatalogDatabase(database.Name, SqlDatabaseStates.Online, Key(database.OwnerOid))).ToList(),
            sections,
            rows.Unavailable);
    }

    public static string Key(long oid) => "pg:" + oid.ToString(CultureInfo.InvariantCulture);

    private static CatalogAccount Account(PostgreSqlRoleRow role, DateTimeOffset collectedAt)
    {
        DateTimeOffset? validUntil = role.ValidUntil is { } until ? new DateTimeOffset(DateTime.SpecifyKind(until, DateTimeKind.Utc)) : null;
        var password = role.CanLogin
            ? new SqlPasswordInfo(null, validUntil is not null, validUntil < collectedAt, validUntil)
            : null;
        return new CatalogAccount(Key(role.Oid), SqlAccountLevels.Server, null, role.Name, null, SqlAccountTypes.PgRole,
            Enabled: true, Locked: false, CreatedAt: null, ModifiedAt: null, DefaultDatabase: null, DefaultSchema: null,
            LoginKey: null, IsOrphaned: false, Password: password);
    }

    /// <summary>SUPERUSER, CREATEROLE, CREATEDB, REPLICATION, BYPASSRLS and LOGIN as server permissions (contract §1).</summary>
    private static IEnumerable<CatalogPermission> RoleAttributes(PostgreSqlRoleRow role)
    {
        var attributes = new (bool Held, string Name)[]
        {
            (role.Superuser, "SUPERUSER"), (role.CreateRole, "CREATEROLE"), (role.CreateDb, "CREATEDB"),
            (role.Replication, "REPLICATION"), (role.BypassRls, "BYPASSRLS"), (role.CanLogin, "LOGIN"),
        };
        return attributes.Where(attribute => attribute.Held)
            .Select(attribute => new CatalogPermission(Key(role.Oid), Securables.Server(), attribute.Name, SqlPermissionStates.Grant, null));
    }

    private static List<CatalogPermission> Permissions(IEnumerable<PostgreSqlAclRow> rows, string? database, ref bool usesPublic, out int omitted)
    {
        omitted = 0;
        var permissions = new List<CatalogPermission>();
        foreach (var objectRows in rows.GroupBy(row => (row.Class, row.Database, row.Schema, row.Name, row.Arguments, row.Column, row.ObjectType)))
        {
            var first = objectRows.First();
            var securable = Securable(first, database ?? first.Database);
            if (securable is null)
            {
                continue;
            }

            if (ReportsOwnership(first))
            {
                permissions.Add(new CatalogPermission(Key(first.OwnerOid), securable, SqlPermissionNames.Ownership, SqlPermissionStates.Grant, null));
            }

            var entries = new List<PostgreSqlAclRow>();
            foreach (var row in objectRows)
            {
                if (row.Grantee is not { } grantee || row.Privilege is null)
                {
                    continue;
                }

                var ownSelfEntry = first.Class != SqlSecurableClasses.Server || first.ObjectType != SqlObjectTypes.Parameter;
                if (ownSelfEntry && grantee == row.OwnerOid && row.Grantor == row.OwnerOid)
                {
                    continue; // The owner's implicit privileges: represented by OWNERSHIP.
                }

                if (row.IsBuiltin && grantee == PublicOid)
                {
                    omitted++;
                    continue;
                }

                entries.Add(row);
            }

            foreach (var group in entries.GroupBy(row => (Grantee: row.Grantee!.Value, row.Grantor, row.IsGrantable)))
            {
                usesPublic |= group.Key.Grantee == PublicOid;
                var state = group.Key.IsGrantable ? SqlPermissionStates.GrantWithGrantOption : SqlPermissionStates.Grant;
                var grantor = group.Key.Grantor is { } grantorOid ? Key(grantorOid) : null;
                var privileges = group.Select(row => row.Privilege!).Distinct(StringComparer.Ordinal).ToList();
                var available = first.Class == SqlSecurableClasses.Column ? ColumnPrivileges : PrivilegesByType.GetValueOrDefault(securable.ObjectType ?? first.Class switch
                {
                    SqlSecurableClasses.Database => SqlObjectTypes.Database,
                    SqlSecurableClasses.Schema => SqlObjectTypes.Schema,
                    _ => string.Empty,
                });
                if (available is { Length: > 1 } && available.All(privileges.Contains))
                {
                    privileges = [All, .. privileges.Where(privilege => !available.Contains(privilege))];
                }

                permissions.AddRange(privileges.Select(privilege => new CatalogPermission(Key(group.Key.Grantee), securable, privilege, state, grantor)));
            }
        }

        return permissions;
    }

    /// <summary>Databases always; otherwise only non-initdb objects whose owner differs from the owner of their schema.</summary>
    private static bool ReportsOwnership(PostgreSqlAclRow row) => row.Class switch
    {
        SqlSecurableClasses.Database => true,
        SqlSecurableClasses.Column or SqlSecurableClasses.DefaultPrivileges => false,
        SqlSecurableClasses.Server => row.ObjectType == SqlObjectTypes.Tablespace && !row.IsSystem,
        _ => !row.IsSystem && row.OwnerOid != row.ParentOwnerOid,
    };

    private static SqlSecurableInfo? Securable(PostgreSqlAclRow row, string? database)
    {
        // Functions and procedures carry their signature so overloads are different securables (contract §1).
        var name = row.Arguments is null || row.Name is null ? row.Name : $"{row.Name}({row.Arguments})";
        return row.Class switch
        {
            SqlSecurableClasses.Server when name is not null && row.ObjectType is not null => Securables.ServerObject(name, row.ObjectType),
            SqlSecurableClasses.Database when database is not null => Securables.Database(database),
            SqlSecurableClasses.Schema when database is not null && row.Schema is not null => Securables.Schema(database, row.Schema),
            SqlSecurableClasses.Object when database is not null && name is not null && row.ObjectType is not null =>
                Securables.Object(database, row.Schema, name, row.ObjectType),
            SqlSecurableClasses.Column when database is not null && name is not null && row.ObjectType is not null && row.Column is not null =>
                Securables.Column(database, row.Schema, name, row.ObjectType, row.Column),
            SqlSecurableClasses.DefaultPrivileges when database is not null && row.ObjectType is not null =>
                Securables.DefaultPrivileges(database, row.Schema, row.ObjectType),
            _ => null,
        };
    }
}
