using Itmas3.Application.Common.Exceptions;
using Itmas3.Application.Inventory.Abstractions;
using Itmas3.Domain.Common;
using Itmas3.Domain.Inventory;

namespace Itmas3.Application.Inventory.SubmitInventoryReport;

public sealed record SystemData(string Hostname, string OsFamily, string? OsName, string? OsVersion);

public sealed record VolumeData(bool IsSystem, string EncryptionStatus);

public sealed record AccountData(bool Enabled, bool IsAdmin);

/// <summary>
/// A validated inventory (contract v1). Only the values needed by business rules are structured; the full document
/// travels in <see cref="PayloadJson"/>.
/// </summary>
public sealed record SubmitInventoryReportCommand(
    Guid DeviceId,
    DateTimeOffset CollectedAt,
    string AgentVersion,
    string? ConnectionIp,
    SystemData System,
    DeclaredPublicNetwork? PublicNetwork,
    IReadOnlyList<VolumeData>? Volumes,
    string? FirewallStatus,
    IReadOnlyList<AccountData>? Accounts,
    int UnavailableCount,
    string PayloadJson);

/// <summary>Use case: store an inventory sent by an enrolled device (RF-15, RF-16, RN-1, RN-5).</summary>
public sealed class SubmitInventoryReportHandler(
    IDeviceRepository devices,
    IInventoryReportRepository reports,
    IInventoryUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>Clock skew tolerated between the device and the server.</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    public async Task<Guid> HandleAsync(SubmitInventoryReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = timeProvider.GetUtcNow();
        if (command.CollectedAt > now + MaxClockSkew)
        {
            throw new DomainException("La fecha de recopilación del inventario no puede ser futura.");
        }

        var summary = Summarize(command);

        return await unitOfWork.ExecuteAsync(async (_, ct) =>
        {
            // RN-1: the device comes from the authenticated credential, never from the payload.
            var device = await devices.GetAsync(command.DeviceId, ct) ?? throw new NotFoundException("Device not found.");
            var report = InventoryReport.Receive(
                device.Id, command.CollectedAt, now, command.ConnectionIp, command.PublicNetwork, command.AgentVersion, command.UnavailableCount, command.PayloadJson);
            reports.Add(report);
            device.ApplyReport(report, summary);
            return report.Id;
        }, cancellationToken);
    }

    /// <summary>Summary values of SPEC-0002 §8.3.6 used to filter devices.</summary>
    public static DeviceSummary Summarize(SubmitInventoryReportCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var systemVolume = command.Volumes?.FirstOrDefault(volume => volume.IsSystem);
        bool? hasLocalAdmins = command.Accounts is null ? null : command.Accounts.Any(account => account.Enabled && account.IsAdmin);

        return new DeviceSummary(
            command.System.Hostname,
            command.System.OsFamily,
            command.System.OsName,
            command.System.OsVersion,
            command.AgentVersion,
            command.Volumes is null ? null : systemVolume?.EncryptionStatus ?? "unknown",
            command.FirewallStatus,
            hasLocalAdmins);
    }
}
