using System.ComponentModel.DataAnnotations;

namespace Itmas3.Api.Security;

internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Requests per minute per authenticated user (or per client IP when anonymous).</summary>
    [Range(1, 100_000)]
    public int PermitLimitPerMinute { get; set; } = 300;

    /// <summary>Sign-in attempts per minute per client IP (protects the OIDC entry point).</summary>
    [Range(1, 1_000)]
    public int AuthenticationPermitLimitPerMinute { get; set; } = 20;

    /// <summary>Device enrollment attempts per minute per client IP (SPEC-0002).</summary>
    [Range(1, 10_000)]
    public int DeviceEnrollmentPermitLimitPerMinute { get; set; } = 10;

    /// <summary>Inventory reports per hour per device credential (SPEC-0002).</summary>
    [Range(1, 10_000)]
    public int InventoryReportsPermitLimitPerHour { get; set; } = 12;
}
