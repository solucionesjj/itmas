using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Inventory.Abstractions;
using Itmas3.Domain.Common;
using Itmas3.Domain.Inventory;

namespace Itmas3.Application.Inventory.EnrollDevice;

public sealed record EnrollDeviceCommand(
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
/// Use case: enroll a device that presented a valid authorization code (RF-14). Requires accepted terms (RN-3).
/// A known hardware id is re-linked and its previous API key stops working (RN-4).
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
        var data = new DeviceEnrollmentData(
            command.Hostname, command.OsFamily, command.AgentVersion, command.TermsVersion, command.TermsAcceptedAt, command.SourceIp);

        return await unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var device = await devices.FindByHardwareIdAsync(command.HardwareId.Trim(), ct);
            var relinked = device is not null;
            var before = device is null ? null : new { device.Hostname, device.EnrolledAt, device.EnrolledFromIp, device.TermsVersion };

            if (device is null)
            {
                device = Device.Enroll(command.HardwareId, apiKeyHash, data, now);
                devices.Add(device);
            }
            else
            {
                device.Relink(apiKeyHash, data, now);
            }

            audit.Actor = new AuditActor(null, $"Agente {device.Hostname}");
            audit.Record(
                relinked ? InventoryAuditActions.DeviceRelinked : InventoryAuditActions.DeviceEnrolled,
                InventoryAuditActions.DeviceResourceType,
                device.Id.ToString(),
                before,
                new
                {
                    device.Hostname,
                    device.OsFamily,
                    device.AgentVersion,
                    device.TermsVersion,
                    device.TermsAcceptedAt,
                    device.EnrolledFromIp,
                });

            return new EnrollDeviceResult(device.Id, apiKey, relinked);
        }, cancellationToken);
    }
}
