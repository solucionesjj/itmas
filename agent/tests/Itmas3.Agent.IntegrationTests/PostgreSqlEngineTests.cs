using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.Collectors.SqlAudit.PostgreSql;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Itmas3.Agent.IntegrationTests;

/// <summary>PostgreSQL 18 with TLS (test certificate) and without TLS, prepared with tricky names and grants at every level.</summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public const string Image = "postgres:18";
    public const string Ventas = "ventas;<db>";
    public const string Tricky = "O'Brien\"; DROP ROLE app;--";

    public TestCertificate Certificate { get; } = TestCertificate.Create();

    public string AgentPassword { get; } = Support.RandomPassword();

    public IContainer Tls { get; private set; } = null!;

    public IContainer Plain { get; private set; } = null!;

    public string? SkipReason { get; private set; }

    private string AdminPassword { get; } = Support.RandomPassword();

    public async ValueTask InitializeAsync()
    {
        if ((SkipReason = Support.DockerUnavailableReason()) is not null)
        {
            return;
        }

        Tls = new ContainerBuilder(Image)
            .WithEnvironment("POSTGRES_PASSWORD", AdminPassword)
            .WithResourceMapping(Encoding.UTF8.GetBytes(Certificate.CertificatePem), "/tls-src/server.crt")
            .WithResourceMapping(Encoding.UTF8.GetBytes(Certificate.KeyPem), "/tls-src/server.key")
            .WithEntrypoint("/bin/sh", "-c")
            .WithCommand("mkdir -p /tls && cp /tls-src/* /tls/ && chown postgres:postgres /tls/* && chmod 600 /tls/server.key && "
                + "exec docker-entrypoint.sh postgres -c ssl=on -c ssl_cert_file=/tls/server.crt -c ssl_key_file=/tls/server.key")
            .WithPortBinding(5432, true)
            .Build();
        Plain = new ContainerBuilder(Image)
            .WithEnvironment("POSTGRES_PASSWORD", AdminPassword)
            .WithPortBinding(5432, true)
            .Build();
        await Task.WhenAll(Tls.StartAsync(Support.Ct), Plain.StartAsync(Support.Ct));
        await Support.WaitUntilAsync(async ct => { await using var c = await AdminAsync(Tls, "postgres", ct); }, TimeSpan.FromMinutes(2), Support.Ct);
        await Support.WaitUntilAsync(async ct => { await using var c = await AdminAsync(Plain, "postgres", ct); }, TimeSpan.FromMinutes(2), Support.Ct);

        foreach (var container in new[] { Tls, Plain })
        {
            await using (var admin = await AdminAsync(container, "postgres", Support.Ct))
            {
                await Support.ExecuteAsync(admin,
                    $"CREATE ROLE itmas_agent LOGIN PASSWORD '{AgentPassword}'",
                    $"CREATE ROLE app LOGIN CREATEDB PASSWORD '{Support.RandomPassword()}' VALID UNTIL '2030-01-01'",
                    "CREATE ROLE lectura NOLOGIN",
                    "CREATE ROLE \"O'Brien\"\"; DROP ROLE app;--\" LOGIN",
                    "GRANT lectura TO app",
                    "GRANT pg_read_all_data TO \"O'Brien\"\"; DROP ROLE app;--\"",
                    "CREATE DATABASE \"ventas;<db>\" OWNER app",
                    "CREATE DATABASE bloqueada",
                    "REVOKE CONNECT ON DATABASE bloqueada FROM PUBLIC",
                    "GRANT SET ON PARAMETER work_mem TO app");
            }

            await using (var admin = await AdminAsync(container, Ventas, Support.Ct))
            {
                await Support.ExecuteAsync(admin,
                    "CREATE SCHEMA \"esquema\"\"raro\" AUTHORIZATION app",
                    "CREATE TABLE \"esquema\"\"raro\".facturas (id int, \"email]\" text)",
                    "ALTER TABLE \"esquema\"\"raro\".facturas OWNER TO app",
                    "GRANT USAGE ON SCHEMA \"esquema\"\"raro\" TO lectura",
                    "GRANT SELECT ON \"esquema\"\"raro\".facturas TO lectura",
                    "GRANT UPDATE ON \"esquema\"\"raro\".facturas TO lectura WITH GRANT OPTION",
                    "GRANT INSERT (\"email]\") ON \"esquema\"\"raro\".facturas TO lectura",
                    "CREATE TABLE \"esquema\"\"raro\".\"tabla'; drop\" (x int)",
                    "ALTER TABLE \"esquema\"\"raro\".\"tabla'; drop\" OWNER TO \"O'Brien\"\"; DROP ROLE app;--\"",
                    "GRANT ALL ON \"esquema\"\"raro\".\"tabla'; drop\" TO app",
                    "CREATE FUNCTION \"esquema\"\"raro\".calcular(integer, text) RETURNS integer LANGUAGE sql AS 'SELECT 1'",
                    "ALTER FUNCTION \"esquema\"\"raro\".calcular(integer, text) OWNER TO app",
                    "CREATE TYPE \"esquema\"\"raro\".estado AS ENUM ('a', 'b')",
                    "ALTER TYPE \"esquema\"\"raro\".estado OWNER TO app",
                    "ALTER DEFAULT PRIVILEGES FOR ROLE app IN SCHEMA \"esquema\"\"raro\" GRANT SELECT ON TABLES TO lectura",
                    "GRANT CREATE ON SCHEMA public TO PUBLIC",
                    "CREATE TABLE public.\"<script>alert(1)</script>\" (x int)");
            }
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
        Support.Settings(SqlEngines.PostgreSql, container.Hostname, container.GetMappedPublicPort(5432), user, tls, pinned, database: "postgres");

    public string AdminPasswordForTests => AdminPassword;

    private async Task<NpgsqlConnection> AdminAsync(IContainer container, string database, CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = container.Hostname,
            Port = container.GetMappedPublicPort(5432),
            Username = "postgres",
            Password = AdminPassword,
            Database = database,
            SslMode = SslMode.Prefer,
            Pooling = false,
        };
        var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

/// <summary>SPEC-0005 CA-4, CA-6, CA-7 and CA-17 against PostgreSQL 18 with the least-privileged account of §10.3 (LOGIN only).</summary>
public sealed class PostgreSqlEngineTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlEngine _engine = new();

    [Fact]
    public async Task CA07_collects_every_level_with_the_least_privileged_account()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var collector = new SqlAuditCollector(_ => _engine, TimeProvider.System, NullLogger<SqlAuditCollector>.Instance);
        var snapshot = await collector.CollectAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Pinned, fixture.Certificate.Thumbprint), fixture.AgentPassword, "1.2.0", Support.Ct);

        SnapshotAssert.IsValidContract(snapshot);
        Assert.StartsWith("18", snapshot.Instance!.Version, StringComparison.Ordinal);
        Assert.Contains(snapshot.Databases, database => database.Name == PostgreSqlFixture.Ventas);
        Assert.Contains(snapshot.Databases, database => database.Name == "template0");

        var app = SnapshotAssert.Account(snapshot, "app");
        var lectura = SnapshotAssert.Account(snapshot, "lectura");
        var tricky = SnapshotAssert.Account(snapshot, PostgreSqlFixture.Tricky);
        var postgres = SnapshotAssert.Account(snapshot, "postgres");
        var everyone = SnapshotAssert.Account(snapshot, "PUBLIC");
        Assert.Equal(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), app.Password!.ValidUntil);
        Assert.Contains("lectura", SnapshotAssert.Roles(snapshot, app));
        Assert.Contains("pg_read_all_data", SnapshotAssert.Roles(snapshot, tricky));

        var appPermissions = SnapshotAssert.Of(snapshot, app);
        Assert.Contains(appPermissions, permission => permission is { Permission: "CREATEDB", Securable.Class: SqlSecurableClasses.Server });
        Assert.Contains(SnapshotAssert.Of(snapshot, postgres), permission => permission is { Permission: "SUPERUSER", Securable.Class: SqlSecurableClasses.Server });
        Assert.Contains(appPermissions, permission => permission is { Permission: SqlPermissionNames.Ownership, Securable: { Class: SqlSecurableClasses.Database, Database: PostgreSqlFixture.Ventas } });
        Assert.Contains(appPermissions, permission => permission is { Permission: SqlPermissionNames.Ownership, Securable: { Class: SqlSecurableClasses.Schema, Schema: "esquema\"raro" } });
        Assert.Contains(appPermissions, permission => permission is { Permission: "ALL", Securable: { Class: SqlSecurableClasses.Object, Name: "tabla'; drop" } } && permission.GrantorRef == tricky.Ref);
        Assert.Contains(appPermissions, permission => permission is { Permission: "SET", Securable: { Name: "work_mem", ObjectType: SqlObjectTypes.Parameter } });

        var lecturaPermissions = SnapshotAssert.Of(snapshot, lectura);
        Assert.Contains(lecturaPermissions, permission => permission is { Permission: "USAGE", Securable: { Class: SqlSecurableClasses.Schema, Database: PostgreSqlFixture.Ventas, Schema: "esquema\"raro" } });
        Assert.Contains(lecturaPermissions, permission => permission is { Permission: "SELECT", State: SqlPermissionStates.Grant, Securable: { Name: "facturas", ObjectType: SqlObjectTypes.Table } } && permission.GrantorRef == app.Ref);
        Assert.Contains(lecturaPermissions, permission => permission is { Permission: "UPDATE", State: SqlPermissionStates.GrantWithGrantOption, Securable.Name: "facturas" });
        Assert.Contains(lecturaPermissions, permission => permission is { Permission: "INSERT", Securable: { Class: SqlSecurableClasses.Column, Column: "email]" } });
        Assert.Contains(lecturaPermissions, permission => permission is { Permission: "SELECT", Securable: { Class: SqlSecurableClasses.DefaultPrivileges, ObjectType: SqlObjectTypes.Table } });

        Assert.Contains(SnapshotAssert.Of(snapshot, tricky), permission => permission is { Permission: SqlPermissionNames.Ownership, Securable.Name: "tabla'; drop" });
        Assert.Contains(SnapshotAssert.Of(snapshot, postgres), permission => permission is { Permission: SqlPermissionNames.Ownership, Securable.Name: "<script>alert(1)</script>" });

        var publicPermissions = SnapshotAssert.Of(snapshot, everyone);
        Assert.Contains(publicPermissions, permission => permission is { Permission: "EXECUTE", Securable: { Name: "calcular(integer, text)", ObjectType: SqlObjectTypes.Function } });
        Assert.Contains(publicPermissions, permission => permission is { Permission: "USAGE", Securable: { Name: "estado", ObjectType: SqlObjectTypes.Type } });
        Assert.Contains(publicPermissions, permission => permission is { Permission: "CREATE", Securable: { Class: SqlSecurableClasses.Schema, Schema: "public", Database: PostgreSqlFixture.Ventas } });
        Assert.Contains(publicPermissions, permission => permission is { Permission: "CONNECT", Securable: { Class: SqlSecurableClasses.Database, Database: "postgres" } });

        // RF-5: initdb grants to PUBLIC on pg_catalog/information_schema objects are counted, not sent.
        Assert.True(snapshot.Omitted.BuiltinPublicPermissions > 1000, snapshot.Omitted.BuiltinPublicPermissions.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.DoesNotContain(snapshot.Permissions, permission => permission.Securable.Schema is "pg_catalog" or "information_schema" && permission.GranteeRef == everyone.Ref);

        // RF-7 / CA-6: a database the account cannot enter is reported, the rest is sent.
        Assert.Equal([new SqlUnavailableSection("database:bloqueada", SqlUnavailableReasons.PermissionDenied)], snapshot.Unavailable);
    }

    [Fact]
    public async Task The_minimum_account_has_no_excess_privileges_and_a_superuser_is_flagged()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var agent = await _engine.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Unverified), fixture.AgentPassword, Support.Ct);
        Assert.Empty(agent.ExcessPrivileges);

        var admin = await _engine.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Unverified, user: "postgres"), fixture.AdminPasswordForTests, Support.Ct);
        Assert.Contains("atributo SUPERUSER", admin.ExcessPrivileges);
    }

    [Fact]
    public async Task CA04_verify_rejects_a_self_signed_certificate()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var failure = await Assert.ThrowsAsync<SqlAuditException>(() => _engine.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Verify), fixture.AgentPassword, Support.Ct));
        Assert.Equal(SqlFailureKind.Certificate, failure.Kind);
    }

    [Fact]
    public async Task CA04_pinned_accepts_only_the_pinned_certificate()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        Assert.NotNull(await _engine.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Pinned, fixture.Certificate.Thumbprint), fixture.AgentPassword, Support.Ct));

        using var other = TestCertificate.Create();
        var failure = await Assert.ThrowsAsync<SqlAuditException>(() => _engine.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Pinned, other.Thumbprint), fixture.AgentPassword, Support.Ct));
        Assert.Equal(SqlFailureKind.Certificate, failure.Kind);
    }

    [Fact]
    public async Task CA04_unverified_accepts_the_self_signed_certificate()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        Assert.NotNull(await _engine.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Unverified), fixture.AgentPassword, Support.Ct));
    }

    [Theory]
    [InlineData(SqlTlsModes.Verify)]
    [InlineData(SqlTlsModes.Pinned)]
    [InlineData(SqlTlsModes.Unverified)]
    public async Task CA04_an_engine_without_tls_is_rejected_in_every_mode(string mode)
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var failure = await Assert.ThrowsAsync<SqlAuditException>(() => _engine.ProbeAsync(fixture.Settings(fixture.Plain, mode, fixture.Certificate.Thumbprint), fixture.AgentPassword, Support.Ct));
        Assert.Equal(SqlFailureKind.TlsNotSupported, failure.Kind);
    }

    [Fact]
    public async Task Wrong_credentials_are_explained_as_such()
    {
        Assert.SkipWhen(fixture.SkipReason is not null, fixture.SkipReason ?? string.Empty);
        var failure = await Assert.ThrowsAsync<SqlAuditException>(() => _engine.ProbeAsync(fixture.Settings(fixture.Tls, SqlTlsModes.Unverified), "wrong-placeholder", Support.Ct));
        Assert.Equal(SqlFailureKind.Authentication, failure.Kind);
        Assert.DoesNotContain("itmas_agent", failure.Message, StringComparison.Ordinal);
    }
}
