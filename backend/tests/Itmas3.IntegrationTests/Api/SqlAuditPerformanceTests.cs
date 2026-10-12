using System.Diagnostics;
using Itmas3.Application.SqlAudit.SubmitSnapshot;
using Itmas3.Domain.SqlAudit;
using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itmas3.IntegrationTests.Api;

/// <summary>
/// RNF-2 and RNF-5: an instance with 200 databases, 20 000 accounts and 200 000 permissions is processed in the request
/// in less than 30 s, both the first time (all inserts) and on the next day (comparison with few changes). The use case
/// is called directly: such a snapshot exceeds the 16 MB body limit, which is reviewed with the first real instance (P-2b).
/// </summary>
public sealed class SqlAuditPerformanceTests(PostgreSqlFixture database) : ApiTestBase(database)
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task RNF2_an_instance_of_the_design_volume_is_processed_within_the_budget()
    {
        var (deviceId, _) = await Factory.RegisterDeviceAsync();
        await ActivateAsync(deviceId);

        var first = Snapshot(deviceId, DateTimeOffset.UtcNow.AddMinutes(-30), removeEvery: 0);
        var firstElapsed = await ProcessAsync(first);

        var second = Snapshot(deviceId, DateTimeOffset.UtcNow.AddMinutes(-5), removeEvery: 100);
        var secondElapsed = await ProcessAsync(second);

        Assert.True(firstElapsed < Budget, $"First snapshot took {firstElapsed}.");
        Assert.True(secondElapsed < Budget, $"Second snapshot took {secondElapsed}.");

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instanceId = await db.SqlInstances.Where(item => item.DeviceId == deviceId).Select(item => item.Id).SingleAsync(Ct);
        Assert.Equal(202_000, await db.SqlAccessItems.CountAsync(item => item.InstanceId == instanceId, Ct));
        Assert.Equal(2_000, await db.SqlAccessItems.CountAsync(item => item.InstanceId == instanceId && item.RemovedAt != null, Ct));
        Assert.Equal(4_000, await db.SqlAccessChanges.CountAsync(item => item.InstanceId == instanceId, Ct));
    }

    private async Task<TimeSpan> ProcessAsync(SubmitSqlAuditSnapshotCommand command)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SubmitSqlAuditSnapshotHandler>();
        var stopwatch = Stopwatch.StartNew();
        var outcome = await handler.HandleAsync(command, Ct);
        stopwatch.Stop();
        Assert.True(outcome.Applied);
        TestContext.Current.SendDiagnosticMessage($"Snapshot processed in {stopwatch.Elapsed}");
        return stopwatch.Elapsed;
    }

    private async Task ActivateAsync(Guid deviceId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await db.Devices.SingleAsync(item => item.Id == deviceId, Ct);
        device.Enroll(
            $"hw-{Guid.NewGuid():N}",
            Application.Inventory.DeviceSecrets.Hash(Application.Inventory.DeviceSecrets.NewApiKey()),
            new Domain.Inventory.DeviceEnrollmentData("srv-perf", "linux", "1.2.0", "2", DateTimeOffset.UtcNow, null),
            DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(Ct);
    }

    /// <summary>
    /// 200 databases with 100 accounts each (users of SQL Server) holding 10 permissions each: 20 000 accounts and
    /// 200 000 permissions. <paramref name="removeEvery"/> replaces one permission in N by a new one.
    /// </summary>
    private static SubmitSqlAuditSnapshotCommand Snapshot(Guid deviceId, DateTimeOffset collectedAt, int removeEvery)
    {
        var databases = Enumerable.Range(0, 200).Select(index => new SqlSnapshotDatabase($"db{index:D3}", "online", null)).ToList();
        var accounts = new List<SqlSnapshotAccount>(20_000);
        var permissions = new List<SqlSnapshotPermission>(200_000);
        var attributes = new SqlAccountAttributes(true, false, new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), null, null, "dbo", null, false, null);
        var counter = 0;
        foreach (var database in databases)
        {
            for (var user = 0; user < 100; user++)
            {
                var reference = $"u{accounts.Count}";
                accounts.Add(new SqlSnapshotAccount(reference, SqlAccountLevel.Database, database.Name, $"usuario_{user:D3}", null, "database-user", attributes, null));
                for (var table = 0; table < 10; table++)
                {
                    var replaced = removeEvery > 0 && counter++ % removeEvery == 0;
                    var securable = new SqlSecurable(SqlSecurableClass.Object, database.Name, "dbo", $"tabla_{table:D2}", "table", null);
                    permissions.Add(new SqlSnapshotPermission(reference, securable, replaced ? "INSERT" : "SELECT", SqlPermissionState.Grant, null));
                }
            }
        }

        return new SubmitSqlAuditSnapshotCommand(
            deviceId,
            Guid.NewGuid(),
            collectedAt,
            null,
            "volumen",
            SqlInstanceKey.Create(SqlEngine.SqlServer, "perf.example.internal", 1433, null),
            SqlTlsMode.Verify,
            new SqlInstanceMetadata("16.0.4175.1", null, "mixed"),
            databases,
            accounts,
            [],
            permissions,
            0,
            []);
    }
}
