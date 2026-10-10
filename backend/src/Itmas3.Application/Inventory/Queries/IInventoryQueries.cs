using Itmas3.Application.Common;

namespace Itmas3.Application.Inventory.Queries;

/// <summary>
/// Read side of the device inventory (SPEC-0003 RF-12..RF-17): read-only projections, no domain rules. The collaborator
/// name is resolved at query time from the ITMas user whose e-mail matches the owner e-mail (RF-4b).
/// </summary>
public interface IInventoryQueries
{
    Task<PagedResult<DeviceListItem>> SearchDevicesAsync(DeviceSearch search, CancellationToken cancellationToken);

    Task<DeviceDetail?> GetDeviceAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>History of a device, newest first; null when the device does not exist.</summary>
    Task<PagedResult<DeviceReportListItem>?> GetDeviceReportsAsync(Guid deviceId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>One report of a device (the latest when <paramref name="reportId"/> is null); null when not found.</summary>
    Task<DeviceReportDetail?> GetDeviceReportAsync(Guid deviceId, Guid? reportId, CancellationToken cancellationToken);
}

/// <summary>Status filter of the list: <see cref="Current"/> (pending and active) is the default.</summary>
public enum DeviceStatusFilter
{
    Current,
    Pending,
    Active,
    Deleted,
    All,
}

public enum DeviceSort
{
    Name,
    Owner,
    LastCollectedAt,
}

public sealed record DeviceSearch(
    string? Text, DeviceStatusFilter Status, string? Type, DeviceSort Sort, bool Descending, PageRequest Page);

/// <summary>Collaborator associated with a device: <see cref="Name"/> is null when no ITMas user has the e-mail.</summary>
public sealed record DeviceOwner(string Email, string? Name);

public sealed record DeviceListItem(
    Guid Id, string Name, string Type, string Status, string? Hostname, DeviceOwner? Owner, DateTimeOffset? LastCollectedAt, int ReportCount);

public sealed record DeviceDetail(
    Guid Id,
    string Name,
    string Type,
    string Status,
    int Revision,
    DeviceOwner? Owner,
    string? Hostname,
    string? OsName,
    string? OsVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EnrolledAt,
    DateTimeOffset? EnrollmentCodeIssuedAt,
    bool HasPendingEnrollmentCode,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? LastCollectedAt,
    DateTimeOffset? DeletedAt,
    int ReportCount);

public sealed record DeviceReportListItem(
    Guid Id,
    DateTimeOffset CollectedAt,
    DateTimeOffset ReceivedAt,
    string? ConnectionIp,
    string? PublicIp,
    string? CountryCode,
    string? Region,
    string? City,
    string AgentVersion,
    int UnavailableCount,
    bool IsLatest);

/// <summary>Header of the device shown with its reports.</summary>
public sealed record DeviceHeader(Guid Id, string Name, string Type, string Status, DeviceOwner? Owner);

/// <summary>A stored report: <see cref="PayloadJson"/> is the validated inventory (contract v1).</summary>
public sealed record DeviceReportDetail(
    Guid Id,
    DeviceHeader Device,
    DateTimeOffset CollectedAt,
    DateTimeOffset ReceivedAt,
    string? ConnectionIp,
    bool IsLatest,
    string PayloadJson);
