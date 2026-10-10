using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Application.Inventory.Abstractions;
using Itmas3.Domain.Common;
using Itmas3.Domain.Inventory;

namespace Itmas3.Application.Inventory.EnrollDevice;

/// <summary>
/// Enrollment of the agent of <paramref name="DeviceId"/>, the device that owns the presented code; the code hash is
/// checked again inside the transaction so it can be used only once (ADR-0012).
/// </summary>
public sealed record EnrollDeviceCommand(
    Guid DeviceId,
    string EnrollmentCodeHash,
    string HardwareId,
    string Hostname,
    string OsFamily,
    string AgentVersion,
    string TermsVersion,
    DateTimeOffset TermsAcceptedAt,
    string? SourceIp);

/// <summary>The API key is returned only here, once (ADR-0009).</summary>
public sealed record EnrollDeviceResult(Guid DeviceId, string ApiKey, bool Relinked);

/// <summary>
/// Use case: enroll the agent of a registered device with its one-time code (SPEC-0003 RF-7..RF-10, ADR-0012).
/// Requires accepted terms (SPEC-0002 RN-3). Re-enrolling an active device replaces its API key (RF-9). A hardware id
/// that belongs to another non-deleted device is rejected (409) and audited (RF-10).
/// </summary>
public sealed class EnrollDeviceHandler(
    IDeviceRepository devices,
    IInventoryUnitOfWork unitOfWork,
    InventorySettings settings,
    TimeProvider timeProvider)
{
    public async Task<EnrollDeviceResult> HandleAsync(EnrollDeviceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!settings.AcceptedTermsVersions.Contains(command.TermsVersion))
        {
            throw new DomainException("La versión de los términos y condiciones aceptada no está vigente. Actualice el agente.");
        }

        var now = timeProvider.GetUtcNow();
        if (command.TermsAcceptedAt > now.AddMinutes(5))
        {
            throw new DomainException("La fecha de aceptación de los términos no puede ser futura.");
        }

        var apiKey = DeviceSecrets.NewApiKey();
        var apiKeyHash = DeviceSecrets.Hash(apiKey);
        var hardwareId = command.HardwareId.Trim();
        var data = new DeviceEnrollmentData(
            command.Hostname, command.OsFamily, command.AgentVersion, command.TermsVersion, command.TermsAcceptedAt, command.SourceIp);

        var outcome = await unitOfWork.ExecuteAsync<(EnrollDeviceResult? Result, bool Conflict)>(async (audit, ct) =>
        {
            var device = await devices.GetAsync(command.DeviceId, ct);
            if (device is null || device.Status == DeviceStatus.Deleted || device.EnrollmentCodeHash != command.EnrollmentCodeHash)
            {
                // Consumed or replaced between authentication and now: same answer as an unknown code.
                return (null, false);
            }

            audit.Actor = new AuditActor(null, $"Agente {command.Hostname.Trim()}");

            var owner = await devices.FindCurrentByHardwareIdAsync(hardwareId, ct);
            if (owner is not null && owner.Id != device.Id)
            {
                audit.Record(
                    InventoryAuditActions.DeviceEnrollmentRejected,
                    InventoryAuditActions.DeviceResourceType,
                    device.Id.ToString(),
                    null,
                    new { Reason = "hardware-registered-as-another-device", OtherDeviceId = owner.Id, command.Hostname, command.SourceIp });
                return (null, true);
            }

            var before = device.Status == DeviceStatus.Active
                ? new { device.Hostname, device.EnrolledAt, device.EnrolledFromIp, device.TermsVersion }
                : null;
            var relinked = device.Enroll(hardwareId, apiKeyHash, data, now);

            audit.Record(
                relinked ? InventoryAuditActions.DeviceRelinked : InventoryAuditActions.DeviceEnrolled,
                InventoryAuditActions.DeviceResourceType,
                device.Id.ToString(),
                before,
                new
                {
                    device.Name,
                    device.Hostname,
                    device.OsFamily,
                    device.AgentVersion,
                    device.TermsVersion,
                    device.TermsAcceptedAt,
                    device.EnrolledFromIp,
                });

            return (new EnrollDeviceResult(device.Id, apiKey, relinked), false);
        }, cancellationToken);

        if (outcome.Conflict)
        {
            // The rejection is already audited (committed with the trail): only now is it reported to the agent.
            throw new ConflictException("Este equipo ya está registrado en el inventario con otro nombre. Elimine primero ese registro.");
        }

        return outcome.Result ?? throw new CredentialRejectedException();
    }
}
