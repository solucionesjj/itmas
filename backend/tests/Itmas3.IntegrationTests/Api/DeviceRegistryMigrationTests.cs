using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Itmas3.IntegrationTests.Api;

/// <summary>
/// SPEC-0003 CA-16: the AddDeviceRegistry migration turns the devices enrolled with the former deployment-wide code into
/// active registry entries (P-6, P-15) and fills the new report columns. Runs on a scratch database migrated step by step.
/// </summary>
public sealed class DeviceRegistryMigrationTests(PostgreSqlFixture database) : IAsyncLifetime
{
    private const string PreviousMigration = "20261008151441_AddDeviceInventory";
    private readonly string _databaseName = $"migration_{Guid.NewGuid():N}";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(database.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(database.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task CA16_enrolled_devices_become_active_entries_with_unique_names_and_inferred_types()
    {
        await using var context = CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration, Ct);

        var windows11 = await InsertDeviceAsync(context, "PC-ANA", "windows", "Windows 11 Pro", DateTimeOffset.UtcNow.AddDays(-3));
        var server = await InsertDeviceAsync(context, "dc01", "windows", "Windows Server 2022 Datacenter", DateTimeOffset.UtcNow.AddDays(-3));
        var linux = await InsertDeviceAsync(context, "web01", "linux", "Ubuntu", DateTimeOffset.UtcNow.AddDays(-2));
        var mac = await InsertDeviceAsync(context, "pc-ana", "macos", "macOS", DateTimeOffset.UtcNow.AddDays(-1));
        await InsertReportAsync(context, linux);

        await migrator.MigrateAsync(cancellationToken: Ct);

        var rows = new Dictionary<Guid, MigratedDevice>();
        await using (var reader = await QueryAsync(context, "SELECT id, name, device_type, status, created_at, enrolled_at, revision, api_key_hash FROM app.devices"))
        {
            while (await reader.ReadAsync(Ct))
            {
                rows[reader.GetGuid(0)] = new MigratedDevice(
                    reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4),
                    reader.GetFieldValue<DateTimeOffset>(5), reader.GetInt32(6), reader.GetString(7));
            }
        }

        Assert.Equal(4, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal("active", row.Value.Status);
            Assert.Equal(row.Value.EnrolledAt, row.Value.CreatedAt);
            Assert.Equal(1, row.Value.Revision);
            // The API key is kept: agents keep sending without reinstalling.
            Assert.Equal(ApiKeyHashOf(row.Key), row.Value.ApiKeyHash);
        });
        Assert.Equal("PC-ANA", rows[windows11].Name);
        Assert.Equal("pc-ana (2)", rows[mac].Name);
        Assert.Equal("workstation", rows[windows11].DeviceType);
        Assert.Equal("workstation", rows[mac].DeviceType);
        Assert.Equal("server", rows[server].DeviceType);
        Assert.Equal("server", rows[linux].DeviceType);

        await using (var reader = await QueryAsync(context, "SELECT agent_version, unavailable_count FROM app.inventory_reports"))
        {
            Assert.True(await reader.ReadAsync(Ct));
            Assert.Equal("1.1.0", reader.GetString(0));
            Assert.Equal(2, reader.GetInt32(1));
            Assert.False(await reader.ReadAsync(Ct));
        }

        // RN-1 holds from now on: the unique index ignores case.
        await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlAsync($"UPDATE app.devices SET name = 'WEB01' WHERE id = {server}", Ct));
    }

    private AppDbContext CreateContext()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(database.AdminConnectionString) { Database = _databaseName }.ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>();
        DbContextConfigurator.Configure(options, connectionString);
        return new AppDbContext(options.Options);
    }

    private static async Task<Guid> InsertDeviceAsync(AppDbContext context, string hostname, string osFamily, string osName, DateTimeOffset enrolledAt)
    {
        var id = Guid.NewGuid();
        var hardwareId = $"hw-{id:N}";
        var apiKeyHash = ApiKeyHashOf(id);
        await context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO app.devices (id, hardware_id, api_key_hash, enrolled_at, terms_version, terms_accepted_at, hostname, os_family, os_name, agent_version)
            VALUES ({id}, {hardwareId}, {apiKeyHash}, {enrolledAt}, '1', {enrolledAt}, {hostname}, {osFamily}, {osName}, '1.1.0')
            """,
            Ct);
        return id;
    }

    private static Task InsertReportAsync(AppDbContext context, Guid deviceId)
    {
        const string payload = """{"agentVersion":"1.1.0","unavailable":[{"section":"security.firewall","reason":"timeout"},{"section":"network.publicNetwork","reason":"error"}]}""";
        return context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO app.inventory_reports (id, device_id, collected_at, received_at, payload)
            VALUES ({Guid.NewGuid()}, {deviceId}, {DateTimeOffset.UtcNow}, {DateTimeOffset.UtcNow}, CAST({payload} AS jsonb))
            """,
            Ct);
    }

    /// <summary>A distinct 64-character value per device (the column has a unique index).</summary>
    private static string ApiKeyHashOf(Guid id) => id.ToString("N") + id.ToString("N");

    private static async Task<System.Data.Common.DbDataReader> QueryAsync(AppDbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(Ct);
        }

        var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteReaderAsync(System.Data.CommandBehavior.CloseConnection, Ct);
    }

    private sealed record MigratedDevice(string Name, string DeviceType, string Status, DateTimeOffset CreatedAt, DateTimeOffset EnrolledAt, int Revision, string ApiKeyHash);
}
