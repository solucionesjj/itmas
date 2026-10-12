using System.ComponentModel.DataAnnotations;

namespace Itmas3.Api.Inventory;

/// <summary>
/// Configuration section <c>Inventory</c> (SPEC-0002). Enrollment codes are issued per device (SPEC-0003, ADR-0012):
/// there is no deployment-wide code any more.
/// </summary>
internal sealed class InventoryOptions
{
    public const string SectionName = "Inventory";

    [Range(1, 3650)]
    public int HistoryRetentionDays { get; set; } = 90;

    /// <summary>Accepted versions of the agent terms and conditions, comma separated.</summary>
    [Required]
    public string AcceptedTermsVersions { get; set; } = Application.Inventory.InventorySettings.DefaultAcceptedTermsVersions;
}
