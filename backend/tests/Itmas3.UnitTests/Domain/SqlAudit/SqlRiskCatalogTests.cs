using Itmas3.Domain.SqlAudit;

namespace Itmas3.UnitTests.Domain.SqlAudit;

/// <summary>SPEC-0005 CA-10: every example of the risk catalog (§8.6), DENY, unknown elements and inheritance.</summary>
public sealed class SqlRiskCatalogTests
{
    [Theory]
    // SQL Server
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Server, "CONTROL SERVER", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Server, "ALTER ANY LOGIN", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Server, "ALTER ANY SERVER ROLE", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Server, "IMPERSONATE ANY LOGIN", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Database, "CONTROL", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Database, "ALTER ANY USER", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Database, "ALTER ANY ROLE", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Database, "IMPERSONATE", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Database, "OWNERSHIP", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Object, "INSERT", RiskLevel.High)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Object, "UPDATE", RiskLevel.High)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Object, "DELETE", RiskLevel.High)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Object, "EXECUTE", RiskLevel.High)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Object, "ALTER", RiskLevel.High)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Database, "CREATE TABLE", RiskLevel.High)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Object, "TAKE OWNERSHIP", RiskLevel.High)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Schema, "CONTROL", RiskLevel.High)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Object, "REFERENCES", RiskLevel.High)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Schema, "OWNERSHIP", RiskLevel.High)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Object, "SELECT", RiskLevel.General)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Database, "CONNECT", RiskLevel.General)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Server, "CONNECT SQL", RiskLevel.General)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Object, "VIEW DEFINITION", RiskLevel.General)]
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Database, "SHOWPLAN", RiskLevel.General)]
    // PostgreSQL
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Server, "SUPERUSER", RiskLevel.Critical)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Server, "CREATEROLE", RiskLevel.Critical)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Server, "BYPASSRLS", RiskLevel.Critical)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Server, "REPLICATION", RiskLevel.Critical)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Database, "OWNERSHIP", RiskLevel.Critical)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Database, "ALL", RiskLevel.Critical)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Object, "TRUNCATE", RiskLevel.High)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Object, "TRIGGER", RiskLevel.High)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Server, "CREATEDB", RiskLevel.High)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Schema, "CREATE", RiskLevel.High)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Object, "OWNERSHIP", RiskLevel.High)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.DefaultPrivileges, "INSERT", RiskLevel.High)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Object, "SELECT", RiskLevel.General)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Database, "CONNECT", RiskLevel.General)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Schema, "USAGE", RiskLevel.General)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Database, "TEMPORARY", RiskLevel.General)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Server, "LOGIN", RiskLevel.General)]
    // MySQL and MariaDB
    [InlineData(SqlEngine.MySql, SqlSecurableClass.Server, "ALL PRIVILEGES", RiskLevel.Critical)]
    [InlineData(SqlEngine.MySql, SqlSecurableClass.Server, "SUPER", RiskLevel.Critical)]
    [InlineData(SqlEngine.MySql, SqlSecurableClass.Server, "GRANT OPTION", RiskLevel.Critical)]
    [InlineData(SqlEngine.MySql, SqlSecurableClass.Server, "CREATE USER", RiskLevel.Critical)]
    [InlineData(SqlEngine.MySql, SqlSecurableClass.Server, "FILE", RiskLevel.Critical)]
    [InlineData(SqlEngine.MySql, SqlSecurableClass.Server, "SHUTDOWN", RiskLevel.Critical)]
    [InlineData(SqlEngine.MySql, SqlSecurableClass.Server, "SYSTEM_USER", RiskLevel.Critical)]
    [InlineData(SqlEngine.MariaDb, SqlSecurableClass.Database, "ALL PRIVILEGES", RiskLevel.Critical)]
    [InlineData(SqlEngine.MySql, SqlSecurableClass.Database, "GRANT OPTION", RiskLevel.High)]
    [InlineData(SqlEngine.MySql, SqlSecurableClass.Object, "DROP", RiskLevel.High)]
    [InlineData(SqlEngine.MariaDb, SqlSecurableClass.Database, "CREATE ROUTINE", RiskLevel.High)]
    [InlineData(SqlEngine.MySql, SqlSecurableClass.Server, "PROCESS", RiskLevel.High)]
    [InlineData(SqlEngine.MariaDb, SqlSecurableClass.Server, "RELOAD", RiskLevel.High)]
    [InlineData(SqlEngine.MySql, SqlSecurableClass.Object, "SELECT", RiskLevel.General)]
    [InlineData(SqlEngine.MySql, SqlSecurableClass.Object, "SHOW VIEW", RiskLevel.General)]
    [InlineData(SqlEngine.MariaDb, SqlSecurableClass.Server, "USAGE", RiskLevel.General)]
    [InlineData(SqlEngine.MySql, SqlSecurableClass.Server, "SHOW DATABASES", RiskLevel.General)]
    // Not in the catalog: High (RN-9).
    [InlineData(SqlEngine.SqlServer, SqlSecurableClass.Server, "VIEW SERVER STATE", RiskLevel.High)]
    [InlineData(SqlEngine.PostgreSql, SqlSecurableClass.Object, "MAINTAIN", RiskLevel.High)]
    [InlineData(SqlEngine.MySql, SqlSecurableClass.Server, "BINLOG_ADMIN", RiskLevel.High)]
    public void Permissions_follow_the_catalog(SqlEngine engine, SqlSecurableClass securableClass, string permission, RiskLevel expected)
    {
        Assert.Equal(expected, SqlRiskCatalog.ForPermission(engine, securableClass, permission, SqlPermissionState.Grant));
    }

    [Fact]
    public void Deny_has_no_risk_and_grant_option_raises_general_permissions_to_high()
    {
        Assert.Equal(RiskLevel.Denied, SqlRiskCatalog.ForPermission(SqlEngine.SqlServer, SqlSecurableClass.Server, "CONTROL SERVER", SqlPermissionState.Deny));
        Assert.Equal(RiskLevel.High, SqlRiskCatalog.ForPermission(SqlEngine.SqlServer, SqlSecurableClass.Object, "SELECT", SqlPermissionState.GrantWithGrantOption));
        Assert.Equal(RiskLevel.Critical, SqlRiskCatalog.ForPermission(SqlEngine.SqlServer, SqlSecurableClass.Database, "CONTROL", SqlPermissionState.GrantWithGrantOption));
    }

    [Theory]
    [InlineData(SqlEngine.SqlServer, "sysadmin", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, "securityadmin", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, "serveradmin", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, "db_owner", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, "db_securityadmin", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, "db_accessadmin", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, "db_datawriter", RiskLevel.High)]
    [InlineData(SqlEngine.SqlServer, "db_ddladmin", RiskLevel.High)]
    [InlineData(SqlEngine.SqlServer, "dbcreator", RiskLevel.High)]
    [InlineData(SqlEngine.SqlServer, "db_datareader", RiskLevel.General)]
    [InlineData(SqlEngine.SqlServer, "public", RiskLevel.General)]
    [InlineData(SqlEngine.PostgreSql, "pg_execute_server_program", RiskLevel.Critical)]
    [InlineData(SqlEngine.PostgreSql, "pg_read_server_files", RiskLevel.Critical)]
    [InlineData(SqlEngine.PostgreSql, "pg_write_server_files", RiskLevel.Critical)]
    [InlineData(SqlEngine.PostgreSql, "pg_write_all_data", RiskLevel.High)]
    [InlineData(SqlEngine.PostgreSql, "pg_read_all_data", RiskLevel.High)]
    [InlineData(SqlEngine.PostgreSql, "app_lectores", RiskLevel.High)]
    [InlineData(SqlEngine.MySql, "rol_app", RiskLevel.High)]
    public void Role_memberships_follow_the_catalog(SqlEngine engine, string role, RiskLevel expected)
    {
        Assert.Equal(expected, SqlRiskCatalog.ForMembership(engine, role));
    }

    [Theory]
    [InlineData(SqlEngine.SqlServer, "sa", "sql-login", RiskLevel.Critical)]
    [InlineData(SqlEngine.MySql, "root", "mysql-account", RiskLevel.Critical)]
    [InlineData(SqlEngine.MariaDb, "root", "mysql-account", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, "sysadmin", "server-role", RiskLevel.Critical)]
    [InlineData(SqlEngine.SqlServer, "app_ventas", "sql-login", RiskLevel.General)]
    [InlineData(SqlEngine.SqlServer, "sa", "database-user", RiskLevel.General)]
    [InlineData(SqlEngine.PostgreSql, "postgres", "pg-role", RiskLevel.General)]
    [InlineData(SqlEngine.MySql, "", "mysql-account", RiskLevel.High)]
    public void Accounts_are_general_unless_well_known(SqlEngine engine, string name, string type, RiskLevel expected)
    {
        Assert.Equal(expected, SqlRiskCatalog.ForAccount(engine, name, type));
    }

    [Fact]
    public void Accounts_inherit_the_risk_of_their_roles_through_nested_roles_and_cycles()
    {
        var user = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var top = Guid.NewGuid();
        var accounts = new Dictionary<Guid, RiskLevel> { [user] = RiskLevel.General, [middle] = RiskLevel.General, [top] = RiskLevel.General };
        var elements = new[]
        {
            new SqlRiskElement(user, RiskLevel.High, middle),
            new SqlRiskElement(middle, RiskLevel.High, top),
            new SqlRiskElement(top, RiskLevel.High, middle),
            new SqlRiskElement(top, RiskLevel.Critical, null),
            new SqlRiskElement(user, RiskLevel.Denied, null),
        };

        var risks = SqlRiskPropagation.Calculate(accounts, elements);

        Assert.Equal((RiskLevel.High, RiskLevel.Critical), risks[user]);
        Assert.Equal((RiskLevel.High, RiskLevel.Critical), risks[middle]);
        Assert.Equal((RiskLevel.Critical, RiskLevel.High), risks[top]);
    }
}
