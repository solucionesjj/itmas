using Itmas3.Application.Common.Exceptions;
using Itmas3.Application.Inventory;
using Itmas3.Application.Inventory.AuthenticateDevice;
using Itmas3.Application.Inventory.ManageDevices;
using Itmas3.Domain.Common;
using Itmas3.Domain.Inventory;

namespace Itmas3.UnitTests.Application.Inventory;

public sealed class ManageDevicesHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryDevices _devices = new();
    private readonly RecordingInventoryUnitOfWork _unitOfWork = new();
    private readonly FixedTimeProvider _clock = new(Now);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CA02_register_returns_the_code_once_and_stores_only_its_hash()
    {
        var issued = await Register("Portátil Ana", "ana@example.com");

        var device = Assert.Single(_devices.Items);
        Assert.Equal(issued.DeviceId, device.Id);
        Assert.Matches("^[0-9A-Z]{5}(-[0-9A-Z]{5}){3}$", issued.EnrollmentCode);
        Assert.Equal(DeviceSecrets.HashEnrollmentCode(issued.EnrollmentCode), device.EnrollmentCodeHash);
        Assert.Equal(device.Id, await new EnrollmentCodeAuthenticator(_devices).AuthenticateAsync(issued.EnrollmentCode, Ct));

        var entry = Assert.Single(_unitOfWork.LastTrail!.Entries);
        Assert.Equal(InventoryAuditActions.DeviceCreated, entry.Action);
        Assert.DoesNotContain(issued.EnrollmentCode, System.Text.Json.JsonSerializer.Serialize(entry.After), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RN01_names_are_unique_ignoring_case_among_devices_that_are_not_deleted()
    {
        await Register("PC-ANA");

        await Assert.ThrowsAsync<DomainException>(() => Register("pc-ana"));

        _devices.Items[0].Delete(Now);
        await Register("pc-ana");
        Assert.Equal(2, _devices.Items.Count);
    }

    [Fact]
    public async Task RF04_update_requires_the_current_revision_and_keeps_names_unique()
    {
        var first = await Register("pc-ana");
        await Register("pc-luis");
        var handler = new UpdateDeviceHandler(_devices, _unitOfWork);

        var revision = await handler.HandleAsync(new UpdateDeviceCommand(first.DeviceId, 1, "srv-01", DeviceType.Server, "Ops@Example.com"), Ct);

        Assert.Equal(2, revision);
        var device = _devices.Items.Single(item => item.Id == first.DeviceId);
        Assert.Equal("ops@example.com", device.OwnerEmail);
        Assert.Equal(InventoryAuditActions.DeviceUpdated, Assert.Single(_unitOfWork.LastTrail!.Entries).Action);

        await Assert.ThrowsAsync<PreconditionFailedException>(() =>
            handler.HandleAsync(new UpdateDeviceCommand(first.DeviceId, 1, "srv-02", DeviceType.Server, null), Ct));
        await Assert.ThrowsAsync<DomainException>(() =>
            handler.HandleAsync(new UpdateDeviceCommand(first.DeviceId, 2, "PC-LUIS", DeviceType.Server, null), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new UpdateDeviceCommand(Guid.NewGuid(), 1, "x", DeviceType.Server, null), Ct));
    }

    [Fact]
    public async Task RF03_regenerating_replaces_the_previous_code()
    {
        var issued = await Register("pc-ana");
        var codes = new EnrollmentCodeAuthenticator(_devices);

        var regenerated = await new RegenerateEnrollmentCodeHandler(_devices, _unitOfWork, _clock).HandleAsync(issued.DeviceId, Ct);

        Assert.NotEqual(issued.EnrollmentCode, regenerated.EnrollmentCode);
        Assert.Null(await codes.AuthenticateAsync(issued.EnrollmentCode, Ct));
        Assert.Equal(issued.DeviceId, await codes.AuthenticateAsync(regenerated.EnrollmentCode, Ct));
        Assert.Equal(InventoryAuditActions.DeviceEnrollmentCodeRegenerated, Assert.Single(_unitOfWork.LastTrail!.Entries).Action);
    }

    [Fact]
    public async Task RF05_delete_revokes_everything_once_and_blocks_further_changes()
    {
        var device = TestDevices.Active("hw-0001-abcd", Now, apiKey: "device-key");
        _devices.Add(device);
        var handler = new DeleteDeviceHandler(_devices, _unitOfWork, _clock);

        Assert.True(await handler.HandleAsync(device.Id, Ct));
        Assert.Equal(InventoryAuditActions.DeviceDeleted, Assert.Single(_unitOfWork.LastTrail!.Entries).Action);
        Assert.False(await handler.HandleAsync(device.Id, Ct));
        Assert.Empty(_unitOfWork.LastTrail!.Entries);

        Assert.Null(await new DeviceAuthenticator(_devices).AuthenticateAsync("device-key", Ct));
        await Assert.ThrowsAsync<DomainException>(() =>
            new RegenerateEnrollmentCodeHandler(_devices, _unitOfWork, _clock).HandleAsync(device.Id, Ct));
    }

    private Task<IssuedEnrollmentCode> Register(string name, string? email = null) =>
        new RegisterDeviceHandler(_devices, _unitOfWork, _clock).HandleAsync(new RegisterDeviceCommand(name, DeviceType.Workstation, email), Ct);
}
