using Itmas3.Application.Common.Exceptions;
using Itmas3.Application.Inventory.Abstractions;
using Itmas3.Domain.Common;
using Itmas3.Domain.Inventory;

namespace Itmas3.Application.Inventory.ManageDevices;

public sealed record RegisterDeviceCommand(string Name, DeviceType Type, string? OwnerEmail);

/// <summary>The enrollment code is returned only here and on regeneration, once (ADR-0012).</summary>
public sealed record IssuedEnrollmentCode(Guid DeviceId, string EnrollmentCode, DateTimeOffset IssuedAt);

public sealed record UpdateDeviceCommand(Guid DeviceId, int ExpectedRevision, string Name, DeviceType Type, string? OwnerEmail);

/// <summary>Use case: register a device and issue its first one-time enrollment code (SPEC-0003 RF-1, RF-2).</summary>
public sealed class RegisterDeviceHandler(IDeviceRepository devices, IInventoryUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public Task<IssuedEnrollmentCode> HandleAsync(RegisterDeviceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = DeviceSecrets.NewEnrollmentCode();

        return unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var now = timeProvider.GetUtcNow();
            var device = Device.Register(command.Name, command.Type, command.OwnerEmail, DeviceSecrets.HashEnrollmentCode(code)!, now);
            await DeviceNames.EnsureAvailableAsync(devices, device.Name, null, ct);

            devices.Add(device);
            audit.Record(InventoryAuditActions.DeviceCreated, InventoryAuditActions.DeviceResourceType, device.Id.ToString(), null, DeviceAuditView.Of(device));
            return new IssuedEnrollmentCode(device.Id, code, now);
        }, cancellationToken);
    }
}

/// <summary>Use case: change the name, type and owner of a device with optimistic concurrency (RF-4, RNF-3).</summary>
public sealed class UpdateDeviceHandler(IDeviceRepository devices, IInventoryUnitOfWork unitOfWork)
{
    public Task<int> HandleAsync(UpdateDeviceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var device = await devices.GetAsync(command.DeviceId, ct) ?? throw new NotFoundException("Device not found.");
            if (device.Revision != command.ExpectedRevision)
            {
                throw new PreconditionFailedException("Otro usuario modificó este equipo. Recarga los datos e inténtalo de nuevo.");
            }

            var before = DeviceAuditView.Of(device);
            device.Update(command.Name, command.Type, command.OwnerEmail);
            await DeviceNames.EnsureAvailableAsync(devices, device.Name, device.Id, ct);

            audit.Record(InventoryAuditActions.DeviceUpdated, InventoryAuditActions.DeviceResourceType, device.Id.ToString(), before, DeviceAuditView.Of(device));
            return device.Revision;
        }, cancellationToken);
    }
}

/// <summary>Use case: replace the enrollment code of a device that is not deleted (RF-3). The previous code stops working.</summary>
public sealed class RegenerateEnrollmentCodeHandler(IDeviceRepository devices, IInventoryUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public Task<IssuedEnrollmentCode> HandleAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var code = DeviceSecrets.NewEnrollmentCode();

        return unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var device = await devices.GetAsync(deviceId, ct) ?? throw new NotFoundException("Device not found.");
            var now = timeProvider.GetUtcNow();
            var hadCode = device.EnrollmentCodeHash is not null;
            device.IssueEnrollmentCode(DeviceSecrets.HashEnrollmentCode(code)!, now);

            audit.Record(
                InventoryAuditActions.DeviceEnrollmentCodeRegenerated,
                InventoryAuditActions.DeviceResourceType,
                device.Id.ToString(),
                new { PreviousCodeWasValid = hadCode },
                DeviceAuditView.Of(device));
            return new IssuedEnrollmentCode(device.Id, code, now);
        }, cancellationToken);
    }
}

/// <summary>
/// Use case: logical deletion (RF-5). The code and the API key stop working at once (RN-6); the history is kept.
/// Idempotent: deleting a deleted device changes nothing and is not audited again.
/// </summary>
public sealed class DeleteDeviceHandler(IDeviceRepository devices, IInventoryUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public Task<bool> HandleAsync(Guid deviceId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var device = await devices.GetAsync(deviceId, ct) ?? throw new NotFoundException("Device not found.");
            var before = DeviceAuditView.Of(device);
            if (!device.Delete(timeProvider.GetUtcNow()))
            {
                return false;
            }

            audit.Record(InventoryAuditActions.DeviceDeleted, InventoryAuditActions.DeviceResourceType, device.Id.ToString(), before, DeviceAuditView.Of(device));
            return true;
        }, cancellationToken);
}

internal static class DeviceNames
{
    /// <summary>RN-1: names are unique, ignoring case, among devices that are not deleted.</summary>
    public static async Task EnsureAvailableAsync(IDeviceRepository devices, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await devices.NameExistsAsync(name, excludeId, cancellationToken))
        {
            throw new DomainException("Ya existe un equipo con este nombre.");
        }
    }
}
