using Itmas3.Domain.Inventory;

namespace Itmas3.Application.Inventory;

/// <summary>Audit actions of the inventory module (SPEC-0002 §14.1, SPEC-0003 §12.2), written to the shared audit log.</summary>
public static class InventoryAuditActions
{
    public const string DeviceEnrolled = "device.enrolled";
    public const string DeviceRelinked = "device.relinked";
    public const string DeviceCreated = "device.created";
    public const string DeviceUpdated = "device.updated";
    public const string DeviceEnrollmentCodeRegenerated = "device.enrollment-code.regenerated";
    public const string DeviceDeleted = "device.deleted";
    public const string DeviceEnrollmentRejected = "device.enrollment.rejected";

    public const string DeviceResourceType = "device";
}

/// <summary>Audited view of a device: the data maintained by users. Never the code, the API key or their hashes.</summary>
internal static class DeviceAuditView
{
    public static object Of(Device device) => new
    {
        device.Name,
        Type = DeviceTypes.ToCode(device.Type),
        device.OwnerEmail,
        Status = DeviceStatuses.ToCode(device.Status),
    };
}

/// <summary>Wire names of <see cref="DeviceType"/> (<c>workstation</c>, <c>server</c>).</summary>
public static class DeviceTypes
{
    public const string Workstation = "workstation";
    public const string Server = "server";

    public static string ToCode(DeviceType type) => type == DeviceType.Server ? Server : Workstation;

    public static DeviceType? Parse(string? code) => code switch
    {
        Workstation => DeviceType.Workstation,
        Server => DeviceType.Server,
        _ => null,
    };
}

/// <summary>Wire names of <see cref="DeviceStatus"/> (<c>pending</c>, <c>active</c>, <c>deleted</c>).</summary>
public static class DeviceStatuses
{
    public const string Pending = "pending";
    public const string Active = "active";
    public const string Deleted = "deleted";

    public static string ToCode(DeviceStatus status) => status switch
    {
        DeviceStatus.Active => Active,
        DeviceStatus.Deleted => Deleted,
        _ => Pending,
    };
}
