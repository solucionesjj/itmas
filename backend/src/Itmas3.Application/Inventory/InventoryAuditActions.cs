namespace Itmas3.Application.Inventory;

/// <summary>Audit actions of the inventory module (SPEC-0002 §14.1), written to the shared audit log.</summary>
public static class InventoryAuditActions
{
    public const string DeviceEnrolled = "device.enrolled";
    public const string DeviceRelinked = "device.relinked";

    public const string DeviceResourceType = "device";
}
