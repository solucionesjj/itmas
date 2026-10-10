using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Domain.Inventory;

namespace Itmas3.Application.Inventory.Abstractions;

public interface IDeviceRepository
{
    Task<Device?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The non-deleted device linked to this hardware id, or null (SPEC-0003 RN-4).</summary>
    Task<Device?> FindCurrentByHardwareIdAsync(string hardwareId, CancellationToken cancellationToken);

    /// <summary>Id of the active device whose API key has this hash, or null.</summary>
    Task<Guid?> FindActiveIdByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken);

    /// <summary>Id of the non-deleted device whose current enrollment code has this hash, or null (ADR-0012).</summary>
    Task<Guid?> FindIdByEnrollmentCodeHashAsync(string enrollmentCodeHash, CancellationToken cancellationToken);

    /// <summary>Whether a non-deleted device other than <paramref name="excludeId"/> has this name, ignoring case (RN-1).</summary>
    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken);

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
/// (shared audit log of SPEC-0001) in a single transaction. The actor is the trail's, or else the current user, or else
/// the system.
/// </summary>
public interface IInventoryUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<AuditTrail, CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}
