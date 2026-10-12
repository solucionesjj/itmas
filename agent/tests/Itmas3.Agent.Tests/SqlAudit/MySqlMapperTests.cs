using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.Collectors.SqlAudit.MySql;

namespace Itmas3.Agent.Tests.SqlAudit;

/// <summary>MySQL 9 and MariaDB 12 grant tables → snapshot (SPEC-0005 RF-4, CA-5, CA-17), with fixtures.</summary>
public sealed class MySqlMapperTests
{
    private const string Ventas = "ventas`; DROP DATABASE x;--";

    private static readonly SqlAuditSnapshot MySql = SqlFixtures.Snapshot(
        SqlEngines.MySql, MySqlMapper.Map(SqlFixtures.Rows<MySqlRows>("mysql.rows.json"), mariaDb: false, SqlFixtures.CollectedAt));

    private static readonly SqlAuditSnapshot MariaDb = SqlFixtures.Snapshot(
        SqlEngines.MariaDb, MySqlMapper.Map(SqlFixtures.Rows<MySqlRows>("mariadb.rows.json"), mariaDb: true, SqlFixtures.CollectedAt));

    [Fact]
    public void Accounts_are_user_at_host_and_roles_come_from_the_role_graph()
    {
        Assert.Equal(new SqlInstanceInfo("9.7.2", "MySQL Community Server - GPL", SqlAuthenticationModes.Password), MySql.Instance);
        var app = MySql.Account("app", host: "10.0.%");
        Assert.Equal((SqlAccountTypes.MySqlAccount, false, false), (app.Type, app.Locked, app.IsOrphaned));
        Assert.Equal(new SqlPasswordInfo(null, true, false, new DateTimeOffset(2026, 11, 30, 0, 0, 0, TimeSpan.Zero)), app.Password);

        var role = MySql.Account("rol_lectura", host: "%");
        Assert.Equal(SqlAccountTypes.MySqlRole, role.Type);
        Assert.Null(role.Password);
        Assert.Equal(["rol_lectura"], MySql.RolesOf(app));

        var tricky = MySql.Account("o'brien\"<script>", host: "%");
        Assert.True(tricky.Locked);
        Assert.Equal(new SqlPasswordInfo(null, false, true, null), tricky.Password);
        Assert.Null(MySql.Account("root", host: "localhost").Password!.ExpirationEnforced); // server default lifetime
    }

    [Fact]
    public void Global_privileges_collapse_to_ALL_PRIVILEGES_with_a_separate_GRANT_OPTION()
    {
        var root = MySql.PermissionsOf(MySql.Account("root", host: "localhost"));
        Assert.Contains(root, permission => permission is { Permission: "ALL PRIVILEGES", Securable.Class: SqlSecurableClasses.Server });
        Assert.Contains(root, permission => permission is { Permission: "GRANT OPTION", Securable.Class: SqlSecurableClasses.Server });
        Assert.Contains(root, permission => permission is { Permission: "SYSTEM_USER", State: SqlPermissionStates.GrantWithGrantOption });
        Assert.DoesNotContain(root, permission => permission.Permission == "SUPER");

        var app = MySql.PermissionsOf(MySql.Account("app", host: "10.0.%"));
        Assert.Contains(app, permission => permission is { Permission: "PROCESS", Securable.Class: SqlSecurableClasses.Server });
        Assert.Contains(app, permission => permission is { Permission: "BACKUP_ADMIN", State: SqlPermissionStates.Grant });
        Assert.Contains(MySql.PermissionsOf(MySql.Account("itmas_agent", host: "%")), permission => permission.Permission == "SHOW DATABASES");
    }

    [Fact]
    public void Database_table_column_and_routine_privileges_belong_to_their_database()
    {
        var app = MySql.PermissionsOf(MySql.Account("app", host: "10.0.%"));
        Assert.Contains(app, permission => permission is { Permission: "ALL PRIVILEGES", Securable: { Class: SqlSecurableClasses.Database, Database: Ventas } });
        Assert.Contains(app, permission => permission is { Permission: "GRANT OPTION", Securable.Class: SqlSecurableClasses.Database });
        Assert.Contains(app, permission => permission is { Permission: "ALL PRIVILEGES", Securable: { Class: SqlSecurableClasses.Object, Name: "todo", ObjectType: SqlObjectTypes.Table }, GrantorRef: null });
        Assert.Contains(app, permission => permission is { Permission: "ALTER ROUTINE", Securable: { Name: "calcular", ObjectType: SqlObjectTypes.Function, Schema: null } });
        Assert.Contains(app, permission => permission is { Permission: "EXECUTE", Securable: { Name: "lib_js", ObjectType: SqlObjectTypes.Other } });

        var tricky = MySql.PermissionsOf(MySql.Account("o'brien\"<script>", host: "%"));
        Assert.Equal(["SELECT", "INSERT", "GRANT OPTION"], tricky.Where(permission => permission.Securable.Name == "facturas]").Select(permission => permission.Permission));
        Assert.All(tricky.Where(permission => permission.Securable.Name == "facturas]"), permission => Assert.Equal("root", MySql.NameOf(permission.GrantorRef)));
        Assert.Contains(tricky, permission => permission is { Permission: "UPDATE", Securable: { Class: SqlSecurableClasses.Column, Name: "clientes", Column: "email;" } });

        // A pattern grant (app\_%) is kept as the database of the securable.
        Assert.Contains(MySql.PermissionsOf(MySql.Account("rol_lectura", host: "%")), permission => permission is { Permission: "SELECT", Securable.Database: "app\\_%" });
    }

    [Fact]
    public void MariaDb_privileges_are_decoded_from_the_access_mask_and_roles_have_no_host()
    {
        var root = MariaDb.PermissionsOf(MariaDb.Account("root", host: "localhost"));
        Assert.Equal(["ALL PRIVILEGES", "GRANT OPTION"], root.Where(permission => permission.Securable.Class == SqlSecurableClasses.Server).Select(permission => permission.Permission));

        var app = MariaDb.Account("app", host: "%");
        Assert.True(app.Locked);
        Assert.Equal(["EXECUTE", "CONNECTION ADMIN"], MariaDb.PermissionsOf(app).Where(permission => permission.Securable.Class == SqlSecurableClasses.Server).Select(permission => permission.Permission));
        Assert.Contains(MariaDb.PermissionsOf(app), permission => permission is { Permission: "DELETE HISTORY", Securable.Name: "historial" });
        Assert.Contains(MariaDb.PermissionsOf(app), permission => permission is { Permission: "SHOW CREATE ROUTINE", Securable: { Name: "pkg", ObjectType: SqlObjectTypes.Other } });

        var role = Assert.Single(MariaDb.Accounts, account => account.Name == "rol<b>");
        Assert.Equal((SqlAccountTypes.MySqlRole, (string?)null), (role.Type, role.Host));
        Assert.Equal(["rol<b>"], MariaDb.RolesOf(app));
        Assert.Contains(MariaDb.PermissionsOf(role), permission => permission is { Permission: "ALL PRIVILEGES", Securable: { Class: SqlSecurableClasses.Database, Database: "ventas'db" } });
        Assert.True(MariaDb.Account("caducada", host: "%").Password!.Expired);
    }

    [Fact]
    public void MariaDb_grantors_recorded_with_the_client_host_resolve_to_the_matching_account()
    {
        var grant = Assert.Single(MariaDb.PermissionsOf(MariaDb.Account("caducada", host: "%")), permission => permission.Securable.Name == "historial");
        Assert.Equal("app", MariaDb.NameOf(grant.GrantorRef)); // app@10.1.2.3 → app@%
        Assert.All(MariaDb.PermissionsOf(MariaDb.Account("app", host: "%")).Where(permission => permission.Securable.Name == "historial"),
            permission => Assert.Equal("root", MariaDb.NameOf(permission.GrantorRef)));
    }

    [Fact]
    public void Nothing_is_omitted_and_no_section_is_unavailable_when_everything_was_read()
    {
        Assert.Equal(0, MySql.Omitted.BuiltinPublicPermissions);
        Assert.Empty(MySql.Unavailable);
        Assert.Contains(MySql.Databases, database => database is { Name: Ventas, State: SqlDatabaseStates.Online, OwnerRef: null });
    }
}
