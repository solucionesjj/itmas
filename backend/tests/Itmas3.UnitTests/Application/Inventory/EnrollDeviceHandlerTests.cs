using Itmas3.Application.Inventory;
using Itmas3.Application.Inventory.AuthenticateDevice;
using Itmas3.Application.Inventory.EnrollDevice;
using Itmas3.Domain.Common;

namespace Itmas3.UnitTests.Application.Inventory;

public sealed class EnrollDeviceHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryDevices _devices = new();
    private readonly RecordingInventoryUnitOfWork _unitOfWork = new();

    private EnrollDeviceHandler Handler => new(_devices, _unitOfWork, new InventorySettings(), new FixedTimeProvider(Now));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Enrollment_issues_a_key_that_authenticates_the_new_device_and_is_audited()
    {
        var result = await Handler.HandleAsync(Command("hw-0001-abcd"), Ct);

        Assert.False(result.Relinked);
        Assert.Equal(result.DeviceId, await new DeviceAuthenticator(_devices).AuthenticateAsync(result.ApiKey, Ct));
        Assert.DoesNotContain(_devices.Items, device => device.ApiKeyHash == result.ApiKey);
        var entry = Assert.Single(_unitOfWork.LastTrail!.Entries);
        Assert.Equal(InventoryAuditActions.DeviceEnrolled, entry.Action);
    }

    [Fact]
    public async Task RN04_reenrollment_relinks_and_invalidates_the_previous_key()
    {
        var first = await Handler.HandleAsync(Command("hw-0001-abcd"), Ct);
        var second = await Handler.HandleAsync(Command("hw-0001-abcd"), Ct);

        Assert.True(second.Relinked);
        Assert.Equal(first.DeviceId, second.DeviceId);
        Assert.Single(_devices.Items);
        Assert.Null(await new DeviceAuthenticator(_devices).AuthenticateAsync(first.ApiKey, Ct));
        Assert.Equal(InventoryAuditActions.DeviceRelinked, Assert.Single(_unitOfWork.LastTrail!.Entries).Action);
    }

    [Fact]
    public async Task RN03_terms_must_be_a_current_version()
    {
        await Assert.ThrowsAsync<DomainException>(() => Handler.HandleAsync(Command("hw-0001-abcd") with { TermsVersion = "0" }, Ct));
        Assert.Empty(_devices.Items);
    }

    [Fact]
    public async Task Terms_cannot_be_accepted_in_the_future()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            Handler.HandleAsync(Command("hw-0001-abcd") with { TermsAcceptedAt = Now.AddHours(1) }, Ct));
    }

    [Fact]
    public async Task Unknown_or_malformed_keys_do_not_authenticate()
    {
        var authenticator = new DeviceAuthenticator(_devices);

        Assert.Null(await authenticator.AuthenticateAsync("", Ct));
        Assert.Null(await authenticator.AuthenticateAsync(new string('a', 500), Ct));
        Assert.Null(await authenticator.AuthenticateAsync(DeviceSecrets.NewApiKey(), Ct));
    }

    private static EnrollDeviceCommand Command(string hardwareId) =>
        new(hardwareId, "pc-01", "windows", "1.0.0", "1", Now.AddMinutes(-1), "198.51.100.7");
}
