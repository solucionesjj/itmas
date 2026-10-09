using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Domain.Inventory;

namespace Itmas3.Application.Inventory.Abstractions;

public interface IDeviceRepository
{
    Task<Device?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Device?> FindByHardwareIdAsync(string hardwareId, CancellationToken cancellationToken);

    /// <summary>Id of the device whose API key has this hash, or null.</summary>
    Task<Guid?> FindIdByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken);

    void Add(Device device);
}

public interface IInventoryReportRepository
{
    void Add(InventoryReport report);

    /// <summary>Deletes reports collected before <paramref name="cutoff"/>, except the latest report of each device.</summary>
    Task<int> DeleteCollectedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}

/// <summary>
/// Executes an inventory operation atomically: persists the changes and the audit entries recorded in the trail
/// (shared audit log of SPEC-0001) in a single transaction.
/// </summary>
public interface IInventoryUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<AuditTrail, CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}
