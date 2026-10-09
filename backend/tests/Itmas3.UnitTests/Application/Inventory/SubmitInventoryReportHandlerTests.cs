using Itmas3.Application.Common.Exceptions;
using Itmas3.Application.Inventory;
using Itmas3.Application.Inventory.Abstractions;
using Itmas3.Application.Inventory.SubmitInventoryReport;
using Itmas3.Domain.Common;
using Itmas3.Domain.Inventory;

namespace Itmas3.UnitTests.Application.Inventory;

public sealed class SubmitInventoryReportHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryDevices _devices = new();
    private readonly InMemoryReports _reports = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Report_is_stored_for_the_authenticated_device_and_updates_its_summary()
    {
        var device = AddDevice();

        await Handler().HandleAsync(Command(device.Id), Ct);

        var report = Assert.Single(_reports.Items);
        Assert.Equal(device.Id, report.DeviceId);
        Assert.Equal("192.0.2.10", report.ConnectionIp);
        Assert.Equal("203.0.113.5", report.PublicIp);
        Assert.Equal(report.Id, device.LastReportId);
        Assert.Equal("encrypted", device.SystemVolumeEncryption);
        Assert.Equal("active", device.FirewallStatus);
        Assert.True(device.HasLocalAdmins);
    }

    [Fact]
    public async Task Unknown_devices_are_rejected()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handler().HandleAsync(Command(Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task Collection_dates_in_the_future_are_rejected()
    {
        var device = AddDevice();

        await Assert.ThrowsAsync<DomainException>(() =>
            Handler().HandleAsync(Command(device.Id) with { CollectedAt = Now.AddHours(1) }, Ct));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void Local_admins_count_only_enabled_admin_accounts(bool enabled, bool isAdmin, bool expected)
    {
        var summary = SubmitInventoryReportHandler.Summarize(Command(Guid.NewGuid()) with { Accounts = [new AccountData(enabled, isAdmin)] });

        Assert.Equal(expected, summary.HasLocalAdmins);
    }

    [Fact]
    public void Unavailable_sections_leave_the_summary_unknown()
    {
        var summary = SubmitInventoryReportHandler.Summarize(Command(Guid.NewGuid()) with { Volumes = null, Accounts = null, FirewallStatus = null });

        Assert.Null(summary.SystemVolumeEncryption);
        Assert.Null(summary.HasLocalAdmins);
        Assert.Null(summary.FirewallStatus);
    }

    [Fact]
    public void Without_a_system_volume_the_encryption_is_unknown()
    {
        var summary = SubmitInventoryReportHandler.Summarize(Command(Guid.NewGuid()) with { Volumes = [new VolumeData(false, "encrypted")] });

        Assert.Equal("unknown", summary.SystemVolumeEncryption);
    }

    private SubmitInventoryReportHandler Handler() =>
        new(_devices, _reports, new RecordingInventoryUnitOfWork(), new FixedTimeProvider(Now));

    private Device AddDevice()
    {
        var device = Device.Enroll("hw-0001-abcd", DeviceSecrets.Hash("key"), new DeviceEnrollmentData("pc-01", "linux", "1.0.0", "1", Now, null), Now);
        _devices.Add(device);
        return device;
    }

    private static SubmitInventoryReportCommand Command(Guid deviceId) => new(
        deviceId,
        Now.AddMinutes(-2),
        "1.0.0",
        "192.0.2.10",
        new SystemData("pc-01", "linux", "Ubuntu", "24.04"),
        new DeclaredPublicNetwork("203.0.113.5", "CO", null, "Bogotá"),
        [new VolumeData(true, "encrypted"), new VolumeData(false, "not-encrypted")],
        "active",
        [new AccountData(true, true), new AccountData(true, false)],
        "{}");

    private sealed class InMemoryReports : IInventoryReportRepository
    {
        public List<InventoryReport> Items { get; } = [];

        public void Add(InventoryReport report) => Items.Add(report);

        public Task<int> DeleteCollectedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) => Task.FromResult(0);
    }
}
