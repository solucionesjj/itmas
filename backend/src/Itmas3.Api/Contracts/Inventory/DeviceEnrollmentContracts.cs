using System.ComponentModel.DataAnnotations;

namespace Itmas3.Api.Contracts.Inventory;

/// <summary>POST /api/v1/device-enrollments (SPEC-0002 §12.1).</summary>
public sealed class DeviceEnrollmentRequest
{
    [Required, SafeText(128), RegularExpression("^[A-Za-z0-9._:{}-]{8,128}$")]
    public required string HardwareId { get; init; }

    [Required, SafeText(256)]
    public required string Hostname { get; init; }

    [Required, RegularExpression(InventoryContract.OsFamilyPattern)]
    public required string OsFamily { get; init; }

    [Required, StringLength(50), RegularExpression(InventoryContract.VersionPattern)]
    public required string AgentVersion { get; init; }

    [Required, StringLength(20), RegularExpression("^[0-9A-Za-z.-]{1,20}$")]
    public required string TermsVersion { get; init; }

    [Required]
    public required DateTimeOffset? TermsAcceptedAt { get; init; }
}

/// <summary>The API key is returned only once.</summary>
public sealed record DeviceEnrollmentResponse(Guid DeviceId, string ApiKey);
