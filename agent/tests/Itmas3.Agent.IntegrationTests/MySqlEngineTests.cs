using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.Collectors.SqlAudit.MySql;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;

namespace Itmas3.Agent.IntegrationTests;

/// <summary>
/// MySQL 9 LTS or MariaDB 12 LTS with TLS (test certificate) and without TLS, with the agent account of SPEC-0005 §10.3,
/// tricky names and grants at every level.
/// </summary>
public abstract class MySqlFamilyFixture(string image, bool mariaDb) : IAsyncLifetime
{
    public const string Ventas = "ventas`; DROP DATABASE x;--";
    public const string Tricky = "o'brien\"<script>";

    /// <summary>MySQL: SELECT only on the non-authentication columns of mysql.user (§10.3).</summary>
    private const string MySqlUserColumns =
        "Host, User, Select_priv, Insert_priv, Update_priv, Delete_priv, Create_priv, Drop_priv, Reload_priv, Shutdown_priv, Process_priv, "
        + "File_priv, Grant_priv, References_priv, Index_priv, Alter_priv, Show_db_priv, Super_priv, Create_tmp_table_priv, Lock_tables_priv, "
        + "Execute_priv, Repl_slave_priv, Repl_client_priv, Create_view_priv, Show_view_priv, Create_routine_priv, Alter_routine_priv, "
        + "Create_user_priv, Event_priv, Trigger_priv, Create_tablespace_priv, Create_role_priv, Drop_role_priv, account_locked, "
        + "password_expired, password_last_changed, password_lifetime";

    public bool MariaDb { get; } = mariaDb;

    public TestCertificate Certificate { get; } = TestCertificate.Create();

    public string AgentPassword { get; } = Support.RandomPassword();

    public string RootPassword { get; } = Support.RandomPassword();

    public IContainer Tls { get; private set; } = null!;

    public IContainer Plain { get; private set; } = null!;

    public string? SkipReason { get; private set; }

    public string Engine => MariaDb ? SqlEngines.MariaDb : SqlEngines.MySql;

    public async ValueTask InitializeAsync()
    {
        if ((SkipReason = Support.DockerUnavailableReason()) is not null)
        {
            return;
        }

        var passwordVariable = MariaDb ? "MARIADB_ROOT_PASSWORD" : "MYSQL_ROOT_PASSWORD";
        Tls = new ContainerBuilder(image)
            .WithEnvironment(passwordVariable, RootPassword)
            .WithResourceMapping(Encoding.UTF8.GetBytes(Certificate.CertificatePem), "/tls/server.crt")
            .WithResourceMapping(Encoding.UTF8.GetBytes(Certificate.KeyPem), "/tls/server.key")
            .WithCommand("--ssl-cert=/tls/server.crt", "--ssl-key=/tls/server.key")
            .WithPortBinding(3306, true)
            .Build();
        Plain = new ContainerBuilder(image)
            .WithEnvironment(passwordVariable, RootPassword)
            .WithCommand(MariaDb ? "--skip-ssl" : "--tls-version=")
            .WithPortBinding(3306, true)
            .Build();
        await Task.WhenAll(Tls.StartAsync(Support.Ct), Plain.StartAsync(Support.Ct));

        foreach (var container in new[] { Tls, Plain })
        {
            await Support.WaitUntilAsync(async ct => { await using var c = await AdminAsync(container, ct); }, TimeSpan.FromMinutes(3), Support.Ct);
            await using var admin = await AdminAsync(container, Support.Ct);
            var agentGrants = MariaDb
                ? new[]
                {
                    "GRANT SELECT ON mysql.global_priv TO 'itmas_agent'@'%'",
                    "GRANT SELECT ON mysql.roles_mapping TO 'itmas_agent'@'%'",
                }
                : new[]
                {
                    $"GRANT SELECT ({MySqlUserColumns}) ON mysql.user TO 'itmas_agent'@'%'",
                    "GRANT SELECT ON mysql.role_edges TO 'itmas_agent'@'%'",
                    "GRANT SELECT ON mysql.global_grants TO 'itmas_agent'@'%'",
                };
            await Support.ExecuteAsync(admin,
            [
                $"CREATE USER 'itmas_agent'@'%' IDENTIFIED BY '{AgentPassword}'",
                "GRANT SHOW DATABASES ON *.* TO 'itmas_agent'@'%'",
                .. agentGrants,
                "GRANT SELECT ON mysql.db TO 'itmas_agent'@'%'",
                "GRANT SELECT ON mysql.tables_priv TO 'itmas_agent'@'%'",
                "GRANT SELECT ON mysql.columns_priv TO 'itmas_agent'@'%'",
                "GRANT SELECT ON mysql.procs_priv TO 'itmas_agent'@'%'",
                "CREATE DATABASE `ventas``; DROP DATABASE x;--`",
                $"CREATE USER 'app'@'10.0.%' IDENTIFIED BY '{Support.RandomPassword()}' PASSWORD EXPIRE INTERVAL 90 DAY",
                "CREATE ROLE 'rol_lectura'",
                "GRANT SELECT ON `app\\_%`.* TO 'rol_lectura'",
                "GRANT 'rol_lectura' TO 'app'@'10.0.%'",
                "GRANT ALL ON `ventas``; DROP DATABASE x;--`.* TO 'app'@'10.0.%' WITH GRANT OPTION",
                "GRANT PROCESS ON *.* TO 'app'@'10.0.%'",
                MariaDb ? "GRANT CONNECTION ADMIN ON *.* TO 'app'@'10.0.%'" : "GRANT BACKUP_ADMIN ON *.* TO 'app'@'10.0.%'",
                "CREATE TABLE `ventas``; DROP DATABASE x;--`.`facturas]` (id int, `email;` text)",
                $"CREATE USER 'o''brien\"<script>'@'%' IDENTIFIED BY '{Support.RandomPassword()}' ACCOUNT LOCK",
                "GRANT SELECT, INSERT ON `ventas``; DROP DATABASE x;--`.`facturas]` TO 'o''brien\"<script>'@'%' WITH GRANT OPTION",
                "GRANT SELECT (`email;`), UPDATE (`email;`) ON `ventas``; DROP DATABASE x;--`.`facturas]` TO 'o''brien\"<script>'@'%'",
                "CREATE FUNCTION `ventas``; DROP DATABASE x;--`.calcular() RETURNS INT DETERMINISTIC RETURN 1",
                "GRANT EXECUTE, ALTER ROUTINE ON FUNCTION `ventas``; DROP DATABASE x;--`.calcular TO 'app'@'10.0.%'",
            ]);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Certificate.Dispose();
        if (SkipReason is null)
        {
            await Tls.DisposeAsync();
            await Plain.DisposeAsync();
        }
    }

    public SqlConnectionSettings Settings(IContainer container, string tls, string? pinned = null, string user = "itmas_agent") =>
        Support.Settings(Engine, container.Hostname, container.GetMappedPublicPort(3306), user, tls, pinned);

    private async Task<MySqlConnection> AdminAsync(IContainer container, CancellationToken cancellationToken)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = container.Hostname,
            Port = container.GetMappedPublicPort(3306),
            UserID = "root",
            Password = RootPassword,
            SslMode = MySqlSslMode.Preferred,
            AllowPublicKeyRetrieval = true,
            Pooling = false,
        };
        var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

public sealed class MySqlFixture() : MySqlFamilyFixture("mysql:9.7", mariaDb: false);

public sealed class MariaDbFixture() : MySqlFamilyFixture("mariadb:12.3", mariaDb: true);

/// <summary>SPEC-0005 CA-4, CA-7 and CA-17 against MySQL 9 LTS / MariaDB 12 LTS with the account of §10.3.</summary>
public abstract class MySqlFamilyEngineTests<TFixture>(TFixture fixture)
    where TFixture : MySqlFamilyFixture
{
    private MySqlEngine Engine => new(fixture.MariaDb);

    [Fact]
    public async Task CA07_collects_every_level_with_the_least_privileged_account()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var collector = new SqlAuditCollector(_ => Engine, TimeProvider.System, NullLogger<SqlAuditCollector>.Instance);
        var snapshot = await collector.CollectAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Pinned, fixture.Certificate.Thumbprint), fixture.AgentPassword, "1.2.0", Support.Ct);

        SnapshotAssert.IsValidContract(snapshot);
        Assert.Empty(snapshot.Unavailable);
        Assert.Contains(snapshot.Databases, database => database.Name == MySqlFamilyFixture.Ventas);

        var root = Assert.Single(snapshot.Accounts, account => account.Name == "root" && account.Host == "localhost");
        var app = Assert.Single(snapshot.Accounts, account => account.Name == "app" && account.Host == "10.0.%");
        var role = Assert.Single(snapshot.Accounts, account => account.Name == "rol_lectura");
        var tricky = Assert.Single(snapshot.Accounts, account => account.Name == MySqlFamilyFixture.Tricky);
        Assert.Equal(SqlAccountTypes.MySqlRole, role.Type);
        Assert.Equal(SqlAccountTypes.MySqlAccount, app.Type);
        Assert.True(tricky.Locked);
        Assert.True(app.Password!.ExpirationEnforced);
        Assert.Contains("rol_lectura", SnapshotAssert.Roles(snapshot, app));

        var rootPermissions = SnapshotAssert.Of(snapshot, root);
        Assert.Contains(rootPermissions, permission => permission is { Permission: "ALL PRIVILEGES", Securable.Class: SqlSecurableClasses.Server });
        Assert.Contains(rootPermissions, permission => permission is { Permission: "GRANT OPTION", Securable.Class: SqlSecurableClasses.Server });

        var appPermissions = SnapshotAssert.Of(snapshot, app);
        Assert.Contains(appPermissions, permission => permission is { Permission: "PROCESS", Securable.Class: SqlSecurableClasses.Server });
        Assert.Contains(appPermissions, permission => permission is { Securable.Class: SqlSecurableClasses.Server } && permission.Permission is "BACKUP_ADMIN" or "CONNECTION ADMIN");
        Assert.Contains(appPermissions, permission => permission is { Permission: "ALL PRIVILEGES", Securable: { Class: SqlSecurableClasses.Database, Database: MySqlFamilyFixture.Ventas } });
        Assert.Contains(appPermissions, permission => permission is { Permission: "GRANT OPTION", Securable.Class: SqlSecurableClasses.Database });
        Assert.Contains(appPermissions, permission => permission is { Permission: "ALTER ROUTINE", Securable: { Name: "calcular", ObjectType: SqlObjectTypes.Function } });

        Assert.Contains(SnapshotAssert.Of(snapshot, role), permission => permission is { Permission: "SELECT", Securable: { Class: SqlSecurableClasses.Database, Database: "app\\_%" } });

        var trickyPermissions = SnapshotAssert.Of(snapshot, tricky);
        Assert.Contains(trickyPermissions, permission => permission is { Permission: "INSERT", Securable: { Class: SqlSecurableClasses.Object, Name: "facturas]" } } && permission.GrantorRef != null);
        Assert.Contains(trickyPermissions, permission => permission is { Permission: "GRANT OPTION", Securable.Name: "facturas]" });
        Assert.Contains(trickyPermissions, permission => permission is { Permission: "UPDATE", Securable: { Class: SqlSecurableClasses.Column, Column: "email;" } });

        var agent = Assert.Single(snapshot.Accounts, account => account.Name == "itmas_agent");
        Assert.Contains(SnapshotAssert.Of(snapshot, agent), permission => permission.Permission == "SHOW DATABASES");
    }

    [Fact]
    public async Task The_minimum_account_has_no_excess_privileges_and_root_is_flagged()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var agent = await Engine.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Unverified), fixture.AgentPassword, Support.Ct);
        Assert.Empty(agent.ExcessPrivileges);

        var root = await Engine.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Unverified, user: "root"), fixture.RootPassword, Support.Ct);
        Assert.Contains(root.ExcessPrivileges, privilege => privilege.StartsWith("privilegio global", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_other_engine_of_the_family_is_detected()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var other = new MySqlEngine(!fixture.MariaDb);
        var failure = await Assert.ThrowsAsync<SqlAuditException>(() => other.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Unverified), fixture.AgentPassword, Support.Ct));
        Assert.Equal(SqlFailureKind.EngineMismatch, failure.Kind);
    }

    [Fact]
    public async Task CA04_verify_rejects_a_self_signed_certificate()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var failure = await Assert.ThrowsAsync<SqlAuditException>(() => Engine.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Verify), fixture.AgentPassword, Support.Ct));
        Assert.Equal(SqlFailureKind.Certificate, failure.Kind);
    }

    [Fact]
    public async Task CA04_pinned_accepts_only_the_pinned_certificate()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        Assert.NotNull(await Engine.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Pinned, fixture.Certificate.Thumbprint), fixture.AgentPassword, Support.Ct));

        using var other = TestCertificate.Create();
        var failure = await Assert.ThrowsAsync<SqlAuditException>(() => Engine.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Pinned, other.Thumbprint), fixture.AgentPassword, Support.Ct));
        Assert.Equal(SqlFailureKind.Certificate, failure.Kind);
    }

    [Fact]
    public async Task CA04_unverified_accepts_the_self_signed_certificate()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        Assert.NotNull(await Engine.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Unverified), fixture.AgentPassword, Support.Ct));
    }

    [Theory]
    [InlineData(SqlTlsModes.Verify)]
    [InlineData(SqlTlsModes.Pinned)]
    [InlineData(SqlTlsModes.Unverified)]
    public async Task CA04_an_engine_without_tls_is_rejected_in_every_mode(string mode)
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var failure = await Assert.ThrowsAsync<SqlAuditException>(() => Engine.ProbeAsync(fixture.Settings(fixture.Plain, mode, fixture.Certificate.Thumbprint), fixture.AgentPassword, Support.Ct));
        Assert.Equal(SqlFailureKind.TlsNotSupported, failure.Kind);
    }

    [Fact]
    public async Task Wrong_credentials_are_explained_as_such()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var failure = await Assert.ThrowsAsync<SqlAuditException>(() => Engine.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Unverified), "wrong-placeholder", Support.Ct));
        Assert.Equal(SqlFailureKind.Authentication, failure.Kind);
    }
}

public sealed class MySqlEngineTests(MySqlFixture fixture) : MySqlFamilyEngineTests<MySqlFixture>(fixture), IClassFixture<MySqlFixture>;

public sealed class MariaDbEngineTests(MariaDbFixture fixture) : MySqlFamilyEngineTests<MariaDbFixture>(fixture), IClassFixture<MariaDbFixture>;
