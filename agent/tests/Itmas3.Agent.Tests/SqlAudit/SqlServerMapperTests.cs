using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.Collectors.SqlAudit.SqlServer;

namespace Itmas3.Agent.Tests.SqlAudit;

/// <summary>SQL Server catalog rows → snapshot (SPEC-0005 RF-4, RF-5, RF-7, CA-5, CA-6, CA-17), with fixtures.</summary>
public sealed class SqlServerMapperTests
{
    private static readonly SqlAuditSnapshot Snapshot =
        SqlFixtures.Snapshot(SqlEngines.SqlServer, SqlServerMapper.Map(SqlFixtures.Rows<SqlServerRows>("sqlserver.rows.json")));

    [Fact]
    public void Instance_and_databases_are_reported_with_their_state_and_owner()
    {
        Assert.Equal(new SqlInstanceInfo("16.0.4175.1", "Developer Edition (64-bit)", SqlAuthenticationModes.Mixed), Snapshot.Instance);
        Assert.Equal(["Archivo", "master", "Ventas']; --"], Snapshot.Databases.Select(database => database.Name));
        Assert.Equal(SqlDatabaseStates.Offline, Snapshot.Databases[0].State);
        Assert.Null(Snapshot.Databases[0].OwnerRef);
        Assert.Equal("sa", Snapshot.NameOf(Snapshot.Databases[2].OwnerRef));
    }

    [Fact]
    public void Logins_carry_their_attributes_and_dates_in_utc()
    {
        var sa = Snapshot.Account("sa");
        Assert.Equal((SqlAccountLevels.Server, SqlAccountTypes.SqlLogin, false), (sa.Level, sa.Type, sa.Enabled));
        Assert.Equal(new SqlPasswordInfo(true, false, false, null), sa.Password);
        Assert.Equal(new DateTimeOffset(2003, 4, 8, 14, 10, 35, 460, TimeSpan.Zero), sa.CreatedAt);

        var locked = Snapshot.Account("<script>alert(\"x\")</script>");
        Assert.True(locked.Locked);
        Assert.True(locked.Password!.Expired);

        Assert.Equal(SqlAccountTypes.WindowsGroup, Snapshot.Account("DOMINIO\\dba-equipo").Type);
        Assert.Null(Snapshot.Account("DOMINIO\\dba-equipo").Password);
        Assert.Equal(SqlAccountTypes.EntraLogin, Snapshot.Account("ext@example.org").Type);
        Assert.Equal(SqlAccountTypes.ServerRole, Snapshot.Account("sysadmin").Type);
        Assert.Null(Snapshot.Account("sysadmin").DefaultDatabase);
    }

    [Fact]
    public void Database_users_are_linked_to_logins_and_orphans_are_flagged()
    {
        const string Ventas = "Ventas']; --";
        var user = Snapshot.Account("app_ventas", Ventas);
        Assert.Equal((SqlAccountTypes.DatabaseUser, "dbo", false), (user.Type, user.DefaultSchema, user.IsOrphaned));
        Assert.Equal("app_ventas", Snapshot.NameOf(user.LoginRef));
        Assert.Equal(SqlAccountLevels.Server, Snapshot.Accounts.Single(account => account.Ref == user.LoginRef).Level);

        var orphan = Snapshot.Account("antiguo_reportes", Ventas);
        Assert.True(orphan.IsOrphaned);
        Assert.Null(orphan.LoginRef);

        var withoutLogin = Snapshot.Account("sin\"login", Ventas);
        Assert.Equal(SqlAccountTypes.UserWithoutLogin, withoutLogin.Type);
        Assert.False(withoutLogin.IsOrphaned);
        Assert.False(withoutLogin.Enabled); // no CONNECT

        Assert.False(Snapshot.Account("guest", Ventas).Enabled);
        Assert.True(Snapshot.Account("dbo", Ventas).Enabled);
        Assert.Equal(SqlAccountTypes.ApplicationRole, Snapshot.Account("app_role", Ventas).Type);
        Assert.Equal(SqlAccountTypes.DatabaseRole, Snapshot.Account("rol;lectura", Ventas).Type);
    }

    [Fact]
    public void Role_memberships_include_server_and_database_roles_without_duplicates()
    {
        Assert.Equal(["sysadmin"], Snapshot.RolesOf(Snapshot.Account("DOMINIO\\dba-equipo")));
        Assert.Equal(["rol;lectura", "app_role"], Snapshot.RolesOf(Snapshot.Account("app_ventas", "Ventas']; --")));
        Assert.Equal(["db_owner"], Snapshot.RolesOf(Snapshot.Account("antiguo_reportes", "Ventas']; --")));
    }

    [Fact]
    public void Permissions_of_every_level_keep_state_and_grantor()
    {
        var login = Snapshot.PermissionsOf(Snapshot.Account("app_ventas"));
        Assert.Contains(login, permission => permission is { Permission: "CONNECT SQL", State: SqlPermissionStates.Grant, Securable.Class: SqlSecurableClasses.Server } && Snapshot.NameOf(permission.GrantorRef) == "sa");
        Assert.Contains(login, permission => permission is { Permission: "VIEW SERVER STATE", State: SqlPermissionStates.GrantWithGrantOption });
        Assert.Contains(login, permission => permission is { Permission: "IMPERSONATE", Securable: { Class: SqlSecurableClasses.Server, Name: "sa", ObjectType: SqlObjectTypes.Login } });
        Assert.DoesNotContain(login, permission => permission.Permission == "ALTER"); // availability group without a resolvable name

        var publicRole = Snapshot.PermissionsOf(Snapshot.Account("public"));
        Assert.Contains(publicRole, permission => permission.Securable is { Name: "TSQL Default TCP", ObjectType: SqlObjectTypes.Endpoint });

        var denied = Snapshot.PermissionsOf(Snapshot.Account("<script>alert(\"x\")</script>"));
        Assert.Equal(["VIEW ANY DATABASE"], denied.Select(permission => permission.Permission)); // R (revoke) rows are not grants
        Assert.Equal(SqlPermissionStates.Deny, denied[0].State);

        var user = Snapshot.PermissionsOf(Snapshot.Account("app_ventas", "Ventas']; --"));
        Assert.Contains(user, permission => permission is { Permission: "CONNECT", Securable.Class: SqlSecurableClasses.Database });
        Assert.Contains(user, permission => permission is { Permission: "DELETE", Securable: { Class: SqlSecurableClasses.Object, Schema: "dbo", Name: "Facturas", ObjectType: SqlObjectTypes.Table } });
        Assert.Contains(user, permission => permission is { Permission: "UPDATE", State: SqlPermissionStates.GrantWithGrantOption, Securable.Name: "Clientes" });
        Assert.Contains(user, permission => permission is { Permission: "SELECT", State: SqlPermissionStates.Deny, Securable: { Class: SqlSecurableClasses.Column, Name: "Clientes", Column: "Email" } });
        Assert.DoesNotContain(user, permission => permission.Permission == "CONTROL"); // unknown securable class: dropped, not invented

        var role = Snapshot.PermissionsOf(Snapshot.Account("rol;lectura", "Ventas']; --"));
        Assert.Contains(role, permission => permission is { Permission: "SELECT", Securable: { Class: SqlSecurableClasses.Schema, Schema: "ventas;esquema" } });
        Assert.Contains(role, permission => permission is { Permission: "EXECUTE", Securable: { Name: "usp_Resumen'Ventas", ObjectType: SqlObjectTypes.Procedure } });

        Assert.Contains(Snapshot.PermissionsOf(Snapshot.Account("antiguo_reportes", "Ventas']; --")),
            permission => permission is { Permission: "IMPERSONATE", Securable: { Name: "app_ventas", ObjectType: SqlObjectTypes.User, Schema: null } });
    }

    [Fact]
    public void RF05_builtin_public_grants_are_counted_not_sent_but_explicit_ones_are()
    {
        Assert.Equal(3, Snapshot.Omitted.BuiltinPublicPermissions);
        var publicUser = Snapshot.PermissionsOf(Snapshot.Account("public", "Ventas']; --"));
        Assert.DoesNotContain(publicUser, permission => permission.Securable.Name == "tables");
        Assert.Contains(publicUser, permission => permission is { Permission: "SELECT", State: SqlPermissionStates.Deny, Securable.Name: "sql_logins" });
        Assert.Contains(publicUser, permission => permission is { Permission: "REFERENCES", Securable: { Name: "TipoMoneda", ObjectType: SqlObjectTypes.Type } });
    }

    [Fact]
    public void Ownership_is_sent_as_OWNERSHIP_without_grantor()
    {
        var ownerships = Snapshot.Permissions.Where(permission => permission.Permission == SqlPermissionNames.Ownership).ToList();
        Assert.All(ownerships, permission => Assert.Equal((SqlPermissionStates.Grant, (string?)null), (permission.State, permission.GrantorRef)));
        Assert.Contains(ownerships, permission => permission.Securable is { Class: SqlSecurableClasses.Database, Database: "Ventas']; --" } && Snapshot.NameOf(permission.GranteeRef) == "sa");
        Assert.Contains(ownerships, permission => permission.Securable is { Class: SqlSecurableClasses.Schema, Schema: "ventas;esquema" } && Snapshot.NameOf(permission.GranteeRef) == "app_ventas");
        Assert.Contains(ownerships, permission => permission.Securable is { Class: SqlSecurableClasses.Object, Name: "Tabla<script>" } && Snapshot.NameOf(permission.GranteeRef) == "antiguo_reportes");
    }

    [Fact]
    public void RF07_offline_databases_are_unavailable_and_have_no_elements()
    {
        Assert.Equal([new SqlUnavailableSection("database:Archivo", SqlUnavailableReasons.Offline)], Snapshot.Unavailable);
        Assert.DoesNotContain(Snapshot.Accounts, account => account.Database == "Archivo");
    }

    [Theory]
    [InlineData("ONLINE", SqlDatabaseStates.Online)]
    [InlineData("RECOVERY_PENDING", SqlDatabaseStates.Recovering)]
    [InlineData("OFFLINE_SECONDARY", SqlDatabaseStates.Offline)]
    [InlineData("COPYING", SqlDatabaseStates.Restoring)]
    [InlineData("SOMETHING_NEW", SqlDatabaseStates.Unknown)]
    public void Database_states_map_to_the_closed_values(string state, string expected) =>
        Assert.Equal(expected, SqlServerMapper.DatabaseState(state));

    [Theory]
    [InlineData("U", SqlObjectTypes.Table)]
    [InlineData("V ", SqlObjectTypes.View)]
    [InlineData("PC", SqlObjectTypes.Procedure)]
    [InlineData("TF", SqlObjectTypes.Function)]
    [InlineData("SO", SqlObjectTypes.Sequence)]
    [InlineData("SN", SqlObjectTypes.Synonym)]
    [InlineData("XSC", SqlObjectTypes.XmlSchemaCollection)]
    [InlineData("ZZ", SqlObjectTypes.Other)]
    [InlineData(null, SqlObjectTypes.Other)]
    public void Object_types_map_to_the_closed_values(string? code, string expected) =>
        Assert.Equal(expected, SqlServerMapper.ObjectType(code));
}
