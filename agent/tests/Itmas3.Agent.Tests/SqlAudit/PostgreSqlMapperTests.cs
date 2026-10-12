using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.Collectors.SqlAudit.PostgreSql;

namespace Itmas3.Agent.Tests.SqlAudit;

/// <summary>PostgreSQL catalog rows → snapshot (SPEC-0005 RF-4, RF-5, RF-7, CA-5, CA-6, CA-17), with fixtures.</summary>
public sealed class PostgreSqlMapperTests
{
    private const string Ventas = "ventas;<db>";

    private static readonly SqlAuditSnapshot Snapshot = SqlFixtures.Snapshot(
        SqlEngines.PostgreSql,
        PostgreSqlMapper.Map(SqlFixtures.Rows<PostgreSqlRows>("postgresql.rows.json"), SqlFixtures.CollectedAt));

    [Fact]
    public void Roles_are_server_accounts_with_their_password_validity()
    {
        Assert.Equal(new SqlInstanceInfo("18.0 (Debian 18.0-1.pgdg13+1)", null, SqlAuthenticationModes.Password), Snapshot.Instance);
        Assert.All(Snapshot.Accounts, account => Assert.Equal((SqlAccountLevels.Server, SqlAccountTypes.PgRole), (account.Level, account.Type)));

        var app = Snapshot.Account("app");
        Assert.Equal(new SqlPasswordInfo(null, true, false, new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)), app.Password);
        Assert.True(Snapshot.Account("O'Brien\"; DROP ROLE app;--").Password!.Expired);
        Assert.Null(Snapshot.Account("lectura").Password); // NOLOGIN: no password
        Assert.Equal(new SqlPasswordInfo(null, false, false, null), Snapshot.Account("itmas_agent").Password);
    }

    [Fact]
    public void Role_attributes_are_server_permissions()
    {
        static IEnumerable<string> Attributes(IEnumerable<SqlPermissionInfo> permissions) =>
            permissions.Where(permission => permission.Securable is { Class: SqlSecurableClasses.Server, Name: null }).Select(permission => permission.Permission);

        Assert.Equal(["SUPERUSER", "CREATEROLE", "CREATEDB", "REPLICATION", "BYPASSRLS", "LOGIN"], Attributes(Snapshot.PermissionsOf(Snapshot.Account("postgres"))));
        Assert.Equal(["CREATEDB", "LOGIN"], Attributes(Snapshot.PermissionsOf(Snapshot.Account("app"))));
        Assert.DoesNotContain(Snapshot.PermissionsOf(Snapshot.Account("lectura")), permission => permission.Securable is { Class: SqlSecurableClasses.Server, Name: null });
    }

    [Fact]
    public void Memberships_are_deduplicated()
    {
        Assert.Equal(["lectura"], Snapshot.RolesOf(Snapshot.Account("app")));
        Assert.Equal(["pg_read_all_data"], Snapshot.RolesOf(Snapshot.Account("O'Brien\"; DROP ROLE app;--")));
    }

    [Fact]
    public void Database_acls_include_public_through_a_pseudo_role_and_never_the_owner_self_entries()
    {
        var publicRole = Snapshot.Account("PUBLIC");
        var publicPermissions = Snapshot.PermissionsOf(publicRole);
        Assert.Contains(publicPermissions, permission => permission is { Permission: "CONNECT", Securable: { Class: SqlSecurableClasses.Database, Database: "postgres" } });
        Assert.Contains(publicPermissions, permission => permission is { Permission: "TEMPORARY", Securable.Database: "template0" });
        Assert.DoesNotContain(Snapshot.PermissionsOf(Snapshot.Account("postgres")), permission => permission is { Permission: "CREATE", Securable.Class: SqlSecurableClasses.Database });

        var obrien = Snapshot.PermissionsOf(Snapshot.Account("O'Brien\"; DROP ROLE app;--"));
        Assert.Contains(obrien, permission => permission is { Permission: "ALL", Securable: { Class: SqlSecurableClasses.Database, Database: Ventas } } && Snapshot.NameOf(permission.GrantorRef) == "app");
    }

    [Fact]
    public void Ownership_is_reported_for_databases_and_non_default_owners_only()
    {
        var ownerships = Snapshot.Permissions.Where(permission => permission.Permission == SqlPermissionNames.Ownership)
            .Select(permission => (Snapshot.NameOf(permission.GranteeRef), permission.Securable.Class, permission.Securable.Database ?? permission.Securable.Name, permission.Securable.Schema, permission.Securable.Name))
            .ToList();
        Assert.Contains(("postgres", SqlSecurableClasses.Database, "postgres", null, null), ownerships);
        Assert.Contains(("app", SqlSecurableClasses.Database, Ventas, null, null), ownerships);
        Assert.Contains(("app", SqlSecurableClasses.Schema, Ventas, "esquema\"raro", null), ownerships);
        Assert.Contains(("O'Brien\"; DROP ROLE app;--", SqlSecurableClasses.Object, Ventas, "esquema\"raro", "tabla'; drop"), ownerships);
        Assert.Contains(("app", SqlSecurableClasses.Server, "ts_datos", null, "ts_datos"), ownerships);
        Assert.DoesNotContain(ownerships, ownership => ownership.Item5 is "facturas" or "contador" or "pg_class" or "pg_default");
        Assert.DoesNotContain(ownerships, ownership => ownership.Item4 is "public" or "pg_catalog");
    }

    [Fact]
    public void Object_column_function_type_and_default_privileges_are_reported()
    {
        var lectura = Snapshot.PermissionsOf(Snapshot.Account("lectura"));
        Assert.Contains(lectura, permission => permission is { Permission: "USAGE", Securable: { Class: SqlSecurableClasses.Schema, Database: Ventas, Schema: "esquema\"raro" } });
        Assert.Contains(lectura, permission => permission is { Permission: "SELECT", State: SqlPermissionStates.Grant, Securable: { Class: SqlSecurableClasses.Object, Name: "facturas", ObjectType: SqlObjectTypes.Table } });
        Assert.Contains(lectura, permission => permission is { Permission: "UPDATE", State: SqlPermissionStates.GrantWithGrantOption, Securable.Name: "facturas" });
        Assert.Contains(lectura, permission => permission is { Permission: "SELECT", Securable: { Class: SqlSecurableClasses.Column, Name: "facturas", Column: "email]" } });
        Assert.Contains(lectura, permission => permission is { Permission: "CREATE", Securable: { Class: SqlSecurableClasses.Server, Name: "ts_datos", ObjectType: SqlObjectTypes.Tablespace } });
        Assert.Contains(lectura, permission => permission is { Permission: "SELECT", Securable: { Class: SqlSecurableClasses.DefaultPrivileges, Schema: "esquema\"raro", ObjectType: SqlObjectTypes.Table } } && Snapshot.NameOf(permission.GrantorRef) == "app");

        var app = Snapshot.PermissionsOf(Snapshot.Account("app"));
        Assert.Contains(app, permission => permission is { Permission: "ALL", Securable.Name: "tabla'; drop" }); // every table privilege, same grantor
        Assert.Contains(app, permission => permission is { Permission: "SET", Securable: { Class: SqlSecurableClasses.Server, Name: "work_mem", ObjectType: SqlObjectTypes.Parameter } });
        Assert.DoesNotContain(app, permission => permission.Securable.Class == SqlSecurableClasses.DefaultPrivileges); // owner self entry
        Assert.DoesNotContain(app, permission => permission.Securable.Name == "calcular(integer)"); // the owner's own EXECUTE: OWNERSHIP covers it

        var publicPermissions = Snapshot.PermissionsOf(Snapshot.Account("PUBLIC"));
        Assert.Contains(publicPermissions, permission => permission is { Permission: "EXECUTE", Securable: { Name: "calcular(integer, text)", ObjectType: SqlObjectTypes.Function } });
        Assert.Contains(publicPermissions, permission => permission is { Permission: "USAGE", Securable: { Name: "estado", ObjectType: SqlObjectTypes.Type } });
        Assert.Contains(publicPermissions, permission => permission is { Permission: "CREATE", Securable: { Class: SqlSecurableClasses.Schema, Schema: "public" } }); // modified built-in ACL
        Assert.Contains(publicPermissions, permission => permission is { Permission: "SELECT", Securable.Name: "pg_authid" }); // not as initdb left it
    }

    [Fact]
    public void RF05_unmodified_builtin_grants_to_public_are_counted_not_sent()
    {
        // pg_catalog USAGE, pg_class SELECT, now() EXECUTE and plpgsql USAGE in postgres; public USAGE in ventas.
        Assert.Equal(5, Snapshot.Omitted.BuiltinPublicPermissions);
        Assert.DoesNotContain(Snapshot.Permissions, permission => permission.Securable.Name is "pg_class" or "now()" or "plpgsql");
        Assert.DoesNotContain(Snapshot.Permissions, permission => permission.Securable.Name == "pg_ls_dir(text)"); // grantee not in pg_roles
    }

    [Fact]
    public void RF07_unreadable_databases_are_unavailable_and_their_database_acls_are_not_sent()
    {
        Assert.Equal([new SqlUnavailableSection("database:bloqueada", SqlUnavailableReasons.PermissionDenied)], Snapshot.Unavailable);
        Assert.Contains(Snapshot.Databases, database => database.Name == "bloqueada");
        Assert.DoesNotContain(Snapshot.Permissions, permission => permission.Securable.Database == "bloqueada");
    }
}
