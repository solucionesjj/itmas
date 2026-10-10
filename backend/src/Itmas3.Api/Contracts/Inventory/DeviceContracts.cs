using System.ComponentModel.DataAnnotations;
using Itmas3.Application.Inventory.Queries;

namespace Itmas3.Api.Contracts.Inventory;

/// <summary>Shared rules of the device registry contract (SPEC-0003 §12.1).</summary>
public static class DeviceContract
{
    public const string TypePattern = "^(workstation|server)$";
    public const string StatusFilterPattern = "^(current|pending|active|deleted|all)$";
    public const string SortPattern = "^-?(name|owner|lastCollectedAt)$";
}

/// <summary>POST /api/v1/devices. The name and e-mail rules (RN-1, RN-2) are enforced by the domain (422).</summary>
public sealed record CreateDeviceRequest(
    [Required, SafeText(100)] string Name,
    [Required, RegularExpression(DeviceContract.TypePattern)] string Type,
    [StringLength(254)] string? OwnerEmail);

/// <summary>PUT /api/v1/devices/{id} with <c>If-Match</c>.</summary>
public sealed record UpdateDeviceRequest(
    [Required, SafeText(100)] string Name,
    [Required, RegularExpression(DeviceContract.TypePattern)] string Type,
    [StringLength(254)] string? OwnerEmail);

/// <summary>The enrollment code is returned only in this response (and on regeneration): once (ADR-0012).</summary>
public sealed record DeviceCreatedResponse(
    Guid Id,
    string Name,
    string Type,
    string Status,
    DeviceOwner? Owner,
    DateTimeOffset CreatedAt,
    string EnrollmentCode,
    DateTimeOffset EnrollmentCodeIssuedAt);

/// <summary>POST /api/v1/devices/{id}/enrollment-code: the new code, returned once.</summary>
public sealed record EnrollmentCodeResponse(string EnrollmentCode, DateTimeOffset IssuedAt);

/// <summary>
/// A stored report (SPEC-0003 RF-17). <see cref="Inventory"/> follows the inventory contract v1 (SPEC-0002 §12.2); the
/// same type that validated it on arrival is used here as the read model, so only known properties are returned.
/// </summary>
public sealed record DeviceReportResponse(
    Guid Id,
    DeviceHeader Device,
    DateTimeOffset CollectedAt,
    DateTimeOffset ReceivedAt,
    string? ConnectionIp,
    bool IsLatest,
    InventoryReportRequest Inventory);
