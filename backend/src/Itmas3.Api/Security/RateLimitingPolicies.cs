namespace Itmas3.Api.Security;

public static class RateLimitingPolicies
{
    public const string Authentication = "authentication";

    public const string DeviceEnrollment = "device-enrollment";

    public const string InventoryReports = "inventory-reports";

    public const string SqlAuditSnapshots = "sql-audit-snapshots";
}
