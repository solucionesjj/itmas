using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Inventory;
using Itmas3.Application.Inventory.Abstractions;
using Itmas3.Domain.Inventory;

namespace Itmas3.UnitTests.Application.Inventory;

internal sealed class InMemoryDevices : IDeviceRepository
{
    public List<Device> Items { get; } = [];

    public Task<Device?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(device => device.Id == id));

    public Task<Device?> FindCurrentByHardwareIdAsync(string hardwareId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(device => device.HardwareId == hardwareId && device.Status != DeviceStatus.Deleted));

    public Task<Guid?> FindActiveIdByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(device => device.ApiKeyHash == apiKeyHash && device.Status == DeviceStatus.Active)?.Id);

    public Task<Guid?> FindIdByEnrollmentCodeHashAsync(string enrollmentCodeHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(device => device.EnrollmentCodeHash == enrollmentCodeHash && device.Status != DeviceStatus.Deleted)?.Id);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(device => device.Status != DeviceStatus.Deleted
            && string.Equals(device.Name, name, StringComparison.OrdinalIgnoreCase)
            && device.Id != excludeId));

    public void Add(Device device) => Items.Add(device);
}

internal sealed class RecordingInventoryUnitOfWork : IInventoryUnitOfWork
{
    public AuditTrail? LastTrail { get; private set; }

    public Task<T> ExecuteAsync<T>(Func<AuditTrail, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        LastTrail = new AuditTrail();
        return work(LastTrail, cancellationToken);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>Devices in a given state, built through the domain like the use cases do.</summary>
internal static class TestDevices
{
    public static Device Pending(string name, DateTimeOffset now, string? code = null) =>
        Device.Register(name, DeviceType.Workstation, null, DeviceSecrets.HashEnrollmentCode(code ?? DeviceSecrets.NewEnrollmentCode())!, now);

    public static Device Active(string hardwareId, DateTimeOffset now, string apiKey = "key", string hostname = "pc-01")
    {
        var device = Pending($"Equipo {hardwareId}", now);
        device.Enroll(hardwareId, DeviceSecrets.Hash(apiKey), new DeviceEnrollmentData(hostname, "linux", "1.0.0", "1", now, "198.51.100.7"), now);
        return device;
    }
}
