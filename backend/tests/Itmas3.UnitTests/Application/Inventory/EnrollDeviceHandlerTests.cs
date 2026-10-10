using Itmas3.Application.Common.Exceptions;
using Itmas3.Application.Inventory;
using Itmas3.Application.Inventory.AuthenticateDevice;
using Itmas3.Application.Inventory.EnrollDevice;
using Itmas3.Domain.Common;
using Itmas3.Domain.Inventory;

namespace Itmas3.UnitTests.Application.Inventory;

public sealed class EnrollDeviceHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryDevices _devices = new();
    private readonly RecordingInventoryUnitOfWork _unitOfWork = new();

    private EnrollDeviceHandler Handler => new(_devices, _unitOfWork, new InventorySettings(), new FixedTimeProvider(Now));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RF07_the_code_of_a_pending_device_enrolls_it_and_issues_a_key_that_authenticates_it()
    {
        var (device, code) = Register("pc-ana");

        var result = await EnrollAsync(device, code, "hw-0001-abcd");

        Assert.False(result.Relinked);
        Assert.Equal(device.Id, result.DeviceId);
        Assert.Equal(DeviceStatus.Active, device.Status);
        Assert.Equal(device.Id, await new DeviceAuthenticator(_devices).AuthenticateAsync(result.ApiKey, Ct));
        Assert.Null(await new EnrollmentCodeAuthenticator(_devices).AuthenticateAsync(code, Ct));
        Assert.DoesNotContain(_devices.Items, item => item.ApiKeyHash == result.ApiKey);
        var entry = Assert.Single(_unitOfWork.LastTrail!.Entries);
        Assert.Equal(InventoryAuditActions.DeviceEnrolled, entry.Action);
        Assert.Equal("Agente pc-01", _unitOfWork.LastTrail.Actor!.DisplayName);
    }

    [Fact]
    public async Task RF08_a_consumed_code_is_rejected_as_an_invalid_credential()
    {
        var (device, code) = Register("pc-ana");
        await EnrollAsync(device, code, "hw-0001-abcd");

        await Assert.ThrowsAsync<CredentialRejectedException>(() => EnrollAsync(device, code, "hw-0001-abcd"));
    }

    [Fact]
    public async Task RF09_reenrollment_with_a_regenerated_code_relinks_and_invalidates_the_previous_key()
    {
        var (device, code) = Register("pc-ana");
        var first = await EnrollAsync(device, code, "hw-0001-abcd");
        var newCode = DeviceSecrets.NewEnrollmentCode();
        device.IssueEnrollmentCode(DeviceSecrets.HashEnrollmentCode(newCode)!, Now);

        var second = await EnrollAsync(device, newCode, "hw-0001-abcd");

        Assert.True(second.Relinked);
        Assert.Equal(first.DeviceId, second.DeviceId);
        Assert.Null(await new DeviceAuthenticator(_devices).AuthenticateAsync(first.ApiKey, Ct));
        Assert.Equal(device.Id, await new DeviceAuthenticator(_devices).AuthenticateAsync(second.ApiKey, Ct));
        Assert.Equal(InventoryAuditActions.DeviceRelinked, Assert.Single(_unitOfWork.LastTrail!.Entries).Action);
    }

    [Fact]
    public async Task RF10_hardware_of_another_device_is_rejected_with_conflict_and_audited()
    {
        var (first, firstCode) = Register("pc-ana");
        await EnrollAsync(first, firstCode, "hw-0001-abcd");
        var (second, secondCode) = Register("pc-ana-2");

        await Assert.ThrowsAsync<ConflictException>(() => EnrollAsync(second, secondCode, "hw-0001-abcd"));

        Assert.Equal(DeviceStatus.Pending, second.Status);
        Assert.NotNull(second.EnrollmentCodeHash);
        var entry = Assert.Single(_unitOfWork.LastTrail!.Entries);
        Assert.Equal(InventoryAuditActions.DeviceEnrollmentRejected, entry.Action);
        Assert.Equal(second.Id.ToString(), entry.ResourceId);
    }

    [Fact]
    public async Task RN04_hardware_of_a_deleted_device_can_be_registered_again()
    {
        var (old, oldCode) = Register("pc-ana");
        await EnrollAsync(old, oldCode, "hw-0001-abcd");
        old.Delete(Now);
        var (replacement, code) = Register("pc-ana");

        var result = await EnrollAsync(replacement, code, "hw-0001-abcd");

        Assert.Equal(replacement.Id, result.DeviceId);
    }

    [Fact]
    public async Task Deleted_devices_cannot_enroll()
    {
        var (device, code) = Register("pc-ana");
        var hash = DeviceSecrets.HashEnrollmentCode(code)!;
        device.Delete(Now);

        await Assert.ThrowsAsync<CredentialRejectedException>(() => Handler.HandleAsync(Command(device.Id, hash, "hw-0001-abcd"), Ct));
    }

    [Fact]
    public async Task RN03_terms_must_be_a_current_version()
    {
        var (device, code) = Register("pc-ana");

        await Assert.ThrowsAsync<DomainException>(() =>
            Handler.HandleAsync(Command(device.Id, DeviceSecrets.HashEnrollmentCode(code)!, "hw-0001-abcd") with { TermsVersion = "0" }, Ct));
        Assert.Equal(DeviceStatus.Pending, device.Status);
    }

    [Fact]
    public async Task Terms_cannot_be_accepted_in_the_future()
    {
        var (device, code) = Register("pc-ana");

        await Assert.ThrowsAsync<DomainException>(() =>
            Handler.HandleAsync(Command(device.Id, DeviceSecrets.HashEnrollmentCode(code)!, "hw-0001-abcd") with { TermsAcceptedAt = Now.AddHours(1) }, Ct));
    }

    [Fact]
    public async Task Unknown_or_malformed_credentials_do_not_authenticate()
    {
        var keys = new DeviceAuthenticator(_devices);
        var codes = new EnrollmentCodeAuthenticator(_devices);
        Register("pc-ana");

        Assert.Null(await keys.AuthenticateAsync("", Ct));
        Assert.Null(await keys.AuthenticateAsync(new string('a', 500), Ct));
        Assert.Null(await keys.AuthenticateAsync(DeviceSecrets.NewApiKey(), Ct));
        Assert.Null(await codes.AuthenticateAsync("not-a-code", Ct));
        Assert.Null(await codes.AuthenticateAsync(DeviceSecrets.NewEnrollmentCode(), Ct));
    }

    [Fact]
    public async Task Enrollment_codes_authenticate_in_any_spelling()
    {
        var (device, code) = Register("pc-ana");

        Assert.Equal(device.Id, await new EnrollmentCodeAuthenticator(_devices).AuthenticateAsync(code.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant(), Ct));
    }

    private (Device Device, string Code) Register(string name)
    {
        var code = DeviceSecrets.NewEnrollmentCode();
        var device = TestDevices.Pending(name, Now, code);
        _devices.Add(device);
        return (device, code);
    }

    private Task<EnrollDeviceResult> EnrollAsync(Device device, string code, string hardwareId) =>
        Handler.HandleAsync(Command(device.Id, DeviceSecrets.HashEnrollmentCode(code)!, hardwareId), Ct);

    private static EnrollDeviceCommand Command(Guid deviceId, string codeHash, string hardwareId) =>
        new(deviceId, codeHash, hardwareId, "pc-01", "windows", "1.0.0", "1", Now.AddMinutes(-1), "198.51.100.7");
}
