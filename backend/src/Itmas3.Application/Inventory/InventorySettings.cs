namespace Itmas3.Application.Inventory;

/// <summary>Deployment settings of the inventory module (SPEC-0002), bound by the composition root.</summary>
public sealed class InventorySettings
{
    /// <summary>Terms shown by new installations of the agent (SPEC-0005 Anexo A).</summary>
    public const string CurrentTermsVersion = "2";

    /// <summary>Default accepted versions: existing installations keep version 1 (SPEC-0005 P-17b).</summary>
    public const string DefaultAcceptedTermsVersions = "1,2";

    /// <summary>Versions of the agent terms and conditions currently accepted for enrollment (RN-3).</summary>
    public IReadOnlySet<string> AcceptedTermsVersions { get; init; } = new HashSet<string>(StringComparer.Ordinal) { "1", CurrentTermsVersion };

    /// <summary>Days of inventory history kept per device (RF-17). The latest inventory is always kept.</summary>
    public int HistoryRetentionDays { get; init; } = 90;
}
