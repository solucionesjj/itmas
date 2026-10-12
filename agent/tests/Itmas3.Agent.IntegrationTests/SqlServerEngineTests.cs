using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.Collectors.SqlAudit.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace Itmas3.Agent.IntegrationTests;

/// <summary>
/// SQL Server in a container with the test certificate configured in mssql.conf, the agent login of SPEC-0005 §10.3
/// (CONNECT ANY DATABASE + VIEW ANY DEFINITION), tricky names and grants at every level. SQL Server images exist only
/// for amd64: where Docker cannot emulate amd64 the tests are skipped with the reason, never passed.
/// </summary>
public abstract class SqlServerFixture(string image, string? platform) : IAsyncLifetime
{
    public const string Ventas = "Ventas']; --";
    public const string Tricky = "O'Brien]; DROP LOGIN sa;--";
    private const uint MssqlUser = 10001;

    public string Image => image;

    public string? SkipReason { get; private set; }

    public TestCertificate Certificate { get; } = TestCertificate.Create();

    public string AgentPassword { get; } = Support.RandomPassword();

    public string SaPassword { get; } = Support.RandomPassword();

    public IContainer Server { get; private set; } = null!;

    public string CertificateDirectory { get; } = Directory.CreateTempSubdirectory("itmas-sqlserver-pin").FullName;

    public async ValueTask InitializeAsync()
    {
        if ((SkipReason = Support.DockerUnavailableReason()) is not null)
        {
            return;
        }

        const string Configuration = "[network]\ntlscert = /var/opt/mssql/itmas-test.crt\ntlskey = /var/opt/mssql/itmas-test.key\ntlsprotocols = 1.2\nforceencryption = 0\n";
        Server = new ContainerBuilder(platform is null ? new DockerImage(image) : new DockerImage(image, new Platform(platform)))
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("MSSQL_SA_PASSWORD", SaPassword)
            .WithResourceMapping(Encoding.UTF8.GetBytes(Certificate.CertificatePem), "/var/opt/mssql/itmas-test.crt", MssqlUser, 0, Unix.FileMode644)
            .WithResourceMapping(Encoding.UTF8.GetBytes(Certificate.KeyPem), "/var/opt/mssql/itmas-test.key", MssqlUser, 0, UnixFileModes.UserRead | UnixFileModes.UserWrite)
            .WithResourceMapping(Encoding.UTF8.GetBytes(Configuration), "/var/opt/mssql/mssql.conf", MssqlUser, 0, Unix.FileMode644)
            .WithPortBinding(1433, true)
            .Build();
        try
        {
            await Server.StartAsync(Support.Ct);
        }
        catch (TimeoutException)
        {
            // Testcontainers gives up when the process dies at once (e.g. an amd64 image without emulation): explained below.
        }

        await Support.WaitUntilAsync(async ct =>
        {
            if (Server.State == TestcontainersStates.Exited)
            {
                return; // Explained below.
            }

            await using var c = await AdminAsync("master", ct);
        }, TimeSpan.FromSeconds(90), Support.Ct);
        if ((SkipReason = await Support.UnrunnableReasonAsync(Server)) is not null)
        {
            if (!SkipReason.Contains("exec format error", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(SkipReason);
            }

            return;
        }
        await using (var master = await AdminAsync("master", Support.Ct))
        {
            await Support.ExecuteAsync(master,
                $"CREATE LOGIN itmas_agent WITH PASSWORD = '{AgentPassword}'",
                "GRANT CONNECT ANY DATABASE TO itmas_agent",
                "GRANT VIEW ANY DEFINITION TO itmas_agent",
                $"CREATE LOGIN app_ventas WITH PASSWORD = '{Support.RandomPassword()}', CHECK_POLICY = ON, CHECK_EXPIRATION = ON",
                $"CREATE LOGIN [O'Brien]]; DROP LOGIN sa;--] WITH PASSWORD = '{Support.RandomPassword()}', CHECK_POLICY = OFF",
                $"CREATE LOGIN huerfano WITH PASSWORD = '{Support.RandomPassword()}'",
                "ALTER SERVER ROLE securityadmin ADD MEMBER app_ventas",
                "GRANT VIEW SERVER STATE TO app_ventas WITH GRANT OPTION",
                "GRANT IMPERSONATE ON LOGIN::sa TO app_ventas",
                "DENY VIEW ANY DATABASE TO [O'Brien]]; DROP LOGIN sa;--]",
                "CREATE DATABASE [Ventas']]; --]",
                "CREATE DATABASE Archivo");
        }

        await using (var ventas = await AdminAsync(Ventas, Support.Ct))
        {
            await Support.ExecuteAsync(ventas,
                "CREATE USER app_ventas FOR LOGIN app_ventas",
                "CREATE USER huerfano FOR LOGIN huerfano",
                "CREATE USER [sin\"login] WITHOUT LOGIN",
                "CREATE ROLE [rol;lectura]",
                "ALTER ROLE [rol;lectura] ADD MEMBER app_ventas",
                "ALTER ROLE db_owner ADD MEMBER huerfano",
                "CREATE SCHEMA [ventas;esquema] AUTHORIZATION app_ventas",
                "CREATE TABLE dbo.Facturas (Id int, Email nvarchar(100))",
                "CREATE TABLE dbo.[Tabla<script>] (x int)",
                "ALTER AUTHORIZATION ON OBJECT::dbo.[Tabla<script>] TO [sin\"login]",
                "GRANT SELECT, DELETE ON dbo.Facturas TO app_ventas",
                "GRANT UPDATE ON dbo.Facturas TO app_ventas WITH GRANT OPTION",
                "DENY SELECT ON dbo.Facturas (Email) TO app_ventas",
                "GRANT SELECT ON SCHEMA::[ventas;esquema] TO [rol;lectura]",
                "CREATE PROCEDURE dbo.[usp_Resumen'Ventas] AS SELECT 1",
                "GRANT EXECUTE ON dbo.[usp_Resumen'Ventas] TO [rol;lectura]");
        }

        await using (var master = await AdminAsync("master", Support.Ct))
        {
            await Support.ExecuteAsync(master, "DROP LOGIN huerfano", "ALTER DATABASE Archivo SET OFFLINE WITH ROLLBACK IMMEDIATE");
        }
    }

    public async ValueTask DisposeAsync()
    {
        Certificate.Dispose();
        if (Server is not null)
        {
            await Server.DisposeAsync();
        }

        Directory.Delete(CertificateDirectory, recursive: true);
    }

    public SqlConnectionSettings Settings(string tls, TestCertificate? pinned = null, string user = "itmas_agent") =>
        Support.Settings(SqlEngines.SqlServer, Server.Hostname, Server.GetMappedPublicPort(1433), user, tls, pinned?.Thumbprint, pinned?.WriteDer(CertificateDirectory));

    private async Task<SqlConnection> AdminAsync(string database, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"tcp:{Server.Hostname},{Server.GetMappedPublicPort(1433)}",
            InitialCatalog = database,
            UserID = "sa",
            Password = SaPassword,
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = true,
            Pooling = false,
            ConnectTimeout = 10,
        };
        var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

public sealed class SqlServer2019Fixture() : SqlServerFixture("mcr.microsoft.com/mssql/server:2019-latest", "linux/amd64");

public sealed class SqlServer2022Fixture() : SqlServerFixture("mcr.microsoft.com/mssql/server:2022-latest", "linux/amd64");

public sealed class SqlServer2025Fixture() : SqlServerFixture("mcr.microsoft.com/mssql/server:2025-latest", "linux/amd64");

/// <summary>
/// Azure SQL Edge (SQL Server engine 15, native arm64): NOT one of the supported versions of SPEC-0005; it only lets the
/// SQL Server code path run on machines without amd64 emulation. The real versions are the 2019/2022/2025 classes.
/// </summary>
public sealed class AzureSqlEdgeFixture() : SqlServerFixture("mcr.microsoft.com/azure-sql-edge:latest", null);

/// <summary>SPEC-0005 CA-4, CA-6, CA-7 and CA-17 against SQL Server with the login of §10.3.</summary>
public abstract class SqlServerEngineTests<TFixture>(TFixture fixture)
    where TFixture : SqlServerFixture
{
    private readonly SqlServerEngine _engine = new();

    [Fact]
    public async Task CA07_collects_every_level_with_the_least_privileged_login()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var collector = new SqlAuditCollector(_ => _engine, TimeProvider.System, NullLogger<SqlAuditCollector>.Instance);
        var snapshot = await collector.CollectAsync(fixture.Settings(SqlTlsModes.Pinned, fixture.Certificate), fixture.AgentPassword, "1.2.0", Support.Ct);

        SnapshotAssert.IsValidContract(snapshot);
        Assert.Equal(SqlAuthenticationModes.Mixed, snapshot.Instance!.AuthenticationMode);
        Assert.Contains(snapshot.Databases, database => database.Name == SqlServerFixture.Ventas);

        var sa = SnapshotAssert.Account(snapshot, "sa");
        var appLogin = SnapshotAssert.Account(snapshot, "app_ventas");
        var tricky = SnapshotAssert.Account(snapshot, SqlServerFixture.Tricky);
        Assert.Equal(SqlAccountTypes.SqlLogin, appLogin.Type);
        Assert.Equal((true, true), (appLogin.Password!.PolicyEnforced, appLogin.Password.ExpirationEnforced));
        Assert.Contains("securityadmin", SnapshotAssert.Roles(snapshot, appLogin));
        Assert.Contains(SnapshotAssert.Of(snapshot, appLogin), permission => permission is { Permission: "VIEW SERVER STATE", State: SqlPermissionStates.GrantWithGrantOption });
        Assert.Contains(SnapshotAssert.Of(snapshot, appLogin), permission => permission is { Permission: "IMPERSONATE", Securable: { Name: "sa", ObjectType: SqlObjectTypes.Login } });
        Assert.Contains(SnapshotAssert.Of(snapshot, tricky), permission => permission is { Permission: "VIEW ANY DATABASE", State: SqlPermissionStates.Deny });

        var user = SnapshotAssert.Account(snapshot, "app_ventas", SqlServerFixture.Ventas);
        Assert.Equal(appLogin.Ref, user.LoginRef);
        Assert.Contains("rol;lectura", SnapshotAssert.Roles(snapshot, user));
        var orphan = SnapshotAssert.Account(snapshot, "huerfano", SqlServerFixture.Ventas);
        Assert.True(orphan.IsOrphaned);
        Assert.Contains("db_owner", SnapshotAssert.Roles(snapshot, orphan));
        Assert.Equal(SqlAccountTypes.UserWithoutLogin, SnapshotAssert.Account(snapshot, "sin\"login", SqlServerFixture.Ventas).Type);

        var userPermissions = SnapshotAssert.Of(snapshot, user);
        Assert.Contains(userPermissions, permission => permission is { Permission: "CONNECT", Securable.Class: SqlSecurableClasses.Database });
        Assert.Contains(userPermissions, permission => permission is { Permission: "DELETE", Securable: { Schema: "dbo", Name: "Facturas", ObjectType: SqlObjectTypes.Table } });
        Assert.Contains(userPermissions, permission => permission is { Permission: "UPDATE", State: SqlPermissionStates.GrantWithGrantOption });
        Assert.Contains(userPermissions, permission => permission is { Permission: "SELECT", State: SqlPermissionStates.Deny, Securable: { Class: SqlSecurableClasses.Column, Column: "Email" } });
        Assert.Contains(userPermissions, permission => permission is { Permission: SqlPermissionNames.Ownership, Securable: { Class: SqlSecurableClasses.Schema, Schema: "ventas;esquema" } });

        var role = SnapshotAssert.Account(snapshot, "rol;lectura", SqlServerFixture.Ventas);
        Assert.Contains(SnapshotAssert.Of(snapshot, role), permission => permission is { Permission: "SELECT", Securable: { Class: SqlSecurableClasses.Schema, Schema: "ventas;esquema" } });
        Assert.Contains(SnapshotAssert.Of(snapshot, role), permission => permission is { Permission: "EXECUTE", Securable: { Name: "usp_Resumen'Ventas", ObjectType: SqlObjectTypes.Procedure } });
        Assert.Contains(SnapshotAssert.Of(snapshot, SnapshotAssert.Account(snapshot, "sin\"login", SqlServerFixture.Ventas)),
            permission => permission is { Permission: SqlPermissionNames.Ownership, Securable.Name: "Tabla<script>" });
        Assert.Contains(SnapshotAssert.Of(snapshot, sa), permission => permission is { Permission: SqlPermissionNames.Ownership, Securable: { Class: SqlSecurableClasses.Database, Database: SqlServerFixture.Ventas } });

        // RF-5: grants to public on objects shipped with SQL Server (master is full of them) are counted, not sent.
        Assert.True(snapshot.Omitted.BuiltinPublicPermissions > 100, snapshot.Omitted.BuiltinPublicPermissions.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // RF-7 / CA-6: an offline database is reported as such; the rest is sent.
        Assert.Contains(new SqlUnavailableSection("database:Archivo", SqlUnavailableReasons.Offline), snapshot.Unavailable);
        Assert.DoesNotContain(snapshot.Unavailable, item => item.Section == "database:" + SqlServerFixture.Ventas);
    }

    [Fact]
    public async Task The_minimum_login_has_no_excess_privileges_and_sa_is_flagged()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var agent = await _engine.ProbeAsync(fixture.Settings(SqlTlsModes.Unverified), fixture.AgentPassword, Support.Ct);
        Assert.Empty(agent.ExcessPrivileges);

        var sa = await _engine.ProbeAsync(fixture.Settings(SqlTlsModes.Unverified, user: "sa"), fixture.SaPassword, Support.Ct);
        Assert.Contains("permiso de servidor CONTROL SERVER", sa.ExcessPrivileges);
    }

    [Fact]
    public async Task CA04_verify_rejects_a_self_signed_certificate()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var failure = await Assert.ThrowsAsync<SqlAuditException>(() => _engine.ProbeAsync(fixture.Settings(SqlTlsModes.Verify), fixture.AgentPassword, Support.Ct));
        Assert.Equal(SqlFailureKind.Certificate, failure.Kind);
    }

    [Fact]
    public async Task CA04_pinned_accepts_only_the_pinned_certificate()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        Assert.NotNull(await _engine.ProbeAsync(fixture.Settings(SqlTlsModes.Pinned, fixture.Certificate), fixture.AgentPassword, Support.Ct));

        using var other = TestCertificate.Create();
        var failure = await Assert.ThrowsAsync<SqlAuditException>(() => _engine.ProbeAsync(fixture.Settings(SqlTlsModes.Pinned, other), fixture.AgentPassword, Support.Ct));
        Assert.Equal(SqlFailureKind.Certificate, failure.Kind);
    }

    [Fact]
    public async Task CA04_unverified_accepts_the_self_signed_certificate()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        Assert.NotNull(await _engine.ProbeAsync(fixture.Settings(SqlTlsModes.Unverified), fixture.AgentPassword, Support.Ct));
    }

    [Fact]
    public async Task The_certificate_to_pin_is_obtained_without_logging_in()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        using var presented = await SqlServerCertificateFetcher.FetchAsync(fixture.Server.Hostname, fixture.Server.GetMappedPublicPort(1433), null, Support.Ct);

        Assert.Equal(fixture.Certificate.Thumbprint, SqlTls.Thumbprint(presented));
    }

    [Fact]
    public async Task Wrong_credentials_are_explained_as_such()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var failure = await Assert.ThrowsAsync<SqlAuditException>(() => _engine.ProbeAsync(fixture.Settings(SqlTlsModes.Unverified), "wrong-placeholder", Support.Ct));
        Assert.Equal(SqlFailureKind.Authentication, failure.Kind);
    }
}

public sealed class SqlServer2019EngineTests(SqlServer2019Fixture fixture) : SqlServerEngineTests<SqlServer2019Fixture>(fixture), IClassFixture<SqlServer2019Fixture>;

public sealed class SqlServer2022EngineTests(SqlServer2022Fixture fixture) : SqlServerEngineTests<SqlServer2022Fixture>(fixture), IClassFixture<SqlServer2022Fixture>;

public sealed class SqlServer2025EngineTests(SqlServer2025Fixture fixture) : SqlServerEngineTests<SqlServer2025Fixture>(fixture), IClassFixture<SqlServer2025Fixture>;

public sealed class AzureSqlEdgeEngineTests(AzureSqlEdgeFixture fixture) : SqlServerEngineTests<AzureSqlEdgeFixture>(fixture), IClassFixture<AzureSqlEdgeFixture>;
