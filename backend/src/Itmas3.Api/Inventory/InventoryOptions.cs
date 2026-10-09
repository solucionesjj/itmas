using System.ComponentModel.DataAnnotations;

namespace Itmas3.Api.Inventory;

/// <summary>Configuration section <c>Inventory</c> (SPEC-0002).</summary>
internal sealed class InventoryOptions
{
    public const string SectionName = "Inventory";
    public const int MinEnrollmentCodeLength = 32;

    /// <summary>
    /// Authorization code agents present to enroll (secret; environment variable INVENTORY_ENROLLMENT_CODE).
    /// Empty = enrollment disabled (404).
    /// </summary>
    public string? EnrollmentCode { get; set; }

    [Range(1, 3650)]
    public int HistoryRetentionDays { get; set; } = 90;

    /// <summary>Accepted versions of the agent terms and conditions, comma separated.</summary>
    [Required]
    public string AcceptedTermsVersions { get; set; } = Application.Inventory.InventorySettings.CurrentTermsVersion;

    public bool EnrollmentEnabled => !string.IsNullOrEmpty(EnrollmentCode);
}
