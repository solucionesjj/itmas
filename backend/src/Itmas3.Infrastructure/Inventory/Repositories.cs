using Itmas3.Application.Inventory.Abstractions;
using Itmas3.Domain.Inventory;
using Itmas3.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itmas3.Infrastructure.Inventory;

internal sealed class DeviceRepository(AppDbContext db) : IDeviceRepository
{
    public Task<Device?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Devices.SingleOrDefaultAsync(device => device.Id == id, cancellationToken);

    public Task<Device?> FindCurrentByHardwareIdAsync(string hardwareId, CancellationToken cancellationToken) =>
        db.Devices.SingleOrDefaultAsync(
            device => device.HardwareId == hardwareId && device.Status != DeviceStatus.Deleted, cancellationToken);

    public async Task<Guid?> FindActiveIdByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken) =>
        await db.Devices.AsNoTracking()
            .Where(device => device.ApiKeyHash == apiKeyHash && device.Status == DeviceStatus.Active)
            .Select(device => (Guid?)device.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<Guid?> FindIdByEnrollmentCodeHashAsync(string enrollmentCodeHash, CancellationToken cancellationToken) =>
        await db.Devices.AsNoTracking()
            .Where(device => device.EnrollmentCodeHash == enrollmentCodeHash && device.Status != DeviceStatus.Deleted)
            .Select(device => (Guid?)device.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var lowered = name.ToLowerInvariant();
        return db.Devices.AnyAsync(
            device => device.Status != DeviceStatus.Deleted && device.Name.ToLower() == lowered && device.Id != excludeId,
            cancellationToken);
    }

    public void Add(Device device) => db.Devices.Add(device);
}

internal sealed class InventoryReportRepository(AppDbContext db) : IInventoryReportRepository
{
    public void Add(InventoryReport report) => db.InventoryReports.Add(report);

    public Task<int> DeleteCollectedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        db.InventoryReports
            .Where(report => report.CollectedAt < cutoff
                && !db.Devices.Any(device => device.LastReportId == report.Id))
            .ExecuteDeleteAsync(cancellationToken);
}
