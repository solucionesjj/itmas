using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Inventory.Abstractions;
using Itmas3.Domain.Inventory;

namespace Itmas3.UnitTests.Application.Inventory;

internal sealed class InMemoryDevices : IDeviceRepository
{
    public List<Device> Items { get; } = [];

    public Task<Device?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(device => device.Id == id));

    public Task<Device?> FindByHardwareIdAsync(string hardwareId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(device => device.HardwareId == hardwareId));

    public Task<Guid?> FindIdByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(device => device.ApiKeyHash == apiKeyHash)?.Id);

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
