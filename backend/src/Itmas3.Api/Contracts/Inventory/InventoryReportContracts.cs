using System.ComponentModel.DataAnnotations;

namespace Itmas3.Api.Contracts.Inventory;

/// <summary>Shared rules of the inventory contract v1 (SPEC-0002 §12.2).</summary>
public static class InventoryContract
{
    public const int SchemaVersion = 1;
    public const int TextLength = 256;
    public const string OsFamilyPattern = "^(windows|linux|macos)$";
    public const string VersionPattern = @"^\d{1,5}\.\d{1,5}\.\d{1,5}([-+][0-9A-Za-z.-]{1,30})?$";

    /// <summary>Sections that may be reported as unavailable (and are then null).</summary>
    public static readonly IReadOnlyList<string> OptionalSections =
    [
        "hardware.memory", "hardware.cpus", "hardware.disks", "hardware.volumes",
        "network.interfaces", "network.publicNetwork", "software.applications", "accounts.users", "security.firewall",
    ];
}

/// <summary>POST /api/v1/inventory-reports. Strict: unknown properties are rejected (constitution §3.6).</summary>
public sealed class InventoryReportRequest : IValidatableObject
{
    [Range(InventoryContract.SchemaVersion, InventoryContract.SchemaVersion)]
    public required int SchemaVersion { get; init; }

    [Required]
    public required DateTimeOffset? CollectedAt { get; init; }

    [Required, StringLength(50), RegularExpression(InventoryContract.VersionPattern)]
    public required string AgentVersion { get; init; }

    [Required]
    public required SystemSection System { get; init; }

    [Required]
    public required HardwareSection Hardware { get; init; }

    [Required]
    public required NetworkSection Network { get; init; }

    [Required]
    public required SoftwareSection Software { get; init; }

    [Required]
    public required AccountsSection Accounts { get; init; }

    [Required]
    public required SecuritySection Security { get; init; }

    [Required, MaxLength(16)]
    public required List<UnavailableSection> Unavailable { get; init; }

    /// <summary>A section is null if and only if it is declared unavailable (RN-6).</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Hardware is null || Network is null || Software is null || Accounts is null || Security is null || Unavailable is null)
        {
            yield break;
        }

        var nullSections = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["hardware.memory"] = Hardware.MemoryBytes is null,
            ["hardware.cpus"] = Hardware.Cpus is null,
            ["hardware.disks"] = Hardware.Disks is null,
            ["hardware.volumes"] = Hardware.Volumes is null,
            ["network.interfaces"] = Network.Interfaces is null,
            ["network.publicNetwork"] = Network.PublicNetwork is null,
            ["software.applications"] = Software.Applications is null,
            ["accounts.users"] = Accounts.Users is null,
            ["security.firewall"] = Security.Firewall is null,
        };

        var declared = Unavailable.Where(item => item?.Section is not null).Select(item => item.Section).ToList();
        if (declared.Count != declared.Distinct(StringComparer.Ordinal).Count())
        {
            yield return new ValidationResult("Una sección no disponible está repetida.", [nameof(Unavailable)]);
        }

        foreach (var (section, isNull) in nullSections)
        {
            if (isNull != declared.Contains(section, StringComparer.Ordinal))
            {
                yield return new ValidationResult(
                    $"La sección {section} debe ser nula si y solo si se declara como no disponible.", [nameof(Unavailable)]);
            }
        }
    }
}

public sealed class SystemSection
{
    [Required, SafeText(InventoryContract.TextLength)]
    public required string Hostname { get; init; }

    [Required, RegularExpression(InventoryContract.OsFamilyPattern)]
    public required string OsFamily { get; init; }

    [Required, SafeText(InventoryContract.TextLength)]
    public required string OsName { get; init; }

    [Required, SafeText(InventoryContract.TextLength)]
    public required string OsVersion { get; init; }

    [SafeText(InventoryContract.TextLength)]
    public required string? OsBuild { get; init; }

    [Required, RegularExpression("^(x64|arm64|x86|arm|unknown)$")]
    public required string Architecture { get; init; }
}

public sealed class HardwareSection
{
    [Range(0, long.MaxValue)]
    public required long? MemoryBytes { get; init; }

    [MaxLength(16)]
    public required List<CpuItem>? Cpus { get; init; }

    [MaxLength(64)]
    public required List<DiskItem>? Disks { get; init; }

    [MaxLength(64)]
    public required List<VolumeItem>? Volumes { get; init; }
}

public sealed class CpuItem
{
    [Required, SafeText(InventoryContract.TextLength)]
    public required string Model { get; init; }

    [Range(0, 4096)]
    public required int PhysicalCores { get; init; }

    [Range(0, 8192)]
    public required int LogicalProcessors { get; init; }
}

public sealed class DiskItem
{
    [Required, SafeText(InventoryContract.TextLength)]
    public required string Id { get; init; }

    [SafeText(InventoryContract.TextLength)]
    public required string? Model { get; init; }

    [Range(0, long.MaxValue)]
    public required long SizeBytes { get; init; }

    [Required, RegularExpression("^(ssd|hdd|unknown)$")]
    public required string MediaType { get; init; }

    [Required, RegularExpression("^(nvme|sata|sas|scsi|usb|virtual|unknown)$")]
    public required string BusType { get; init; }
}

public sealed class VolumeItem
{
    [Required, SafeText(InventoryContract.TextLength)]
    public required string MountPoint { get; init; }

    [SafeText(64)]
    public required string? FileSystem { get; init; }

    [Range(0, long.MaxValue)]
    public required long SizeBytes { get; init; }

    [Range(0, long.MaxValue)]
    public required long FreeBytes { get; init; }

    [SafeText(InventoryContract.TextLength)]
    public required string? DiskId { get; init; }

    public required bool IsSystem { get; init; }

    [Required]
    public required EncryptionInfo Encryption { get; init; }
}

public sealed class EncryptionInfo
{
    [Required, RegularExpression("^(encrypted|encrypting|suspended|not-encrypted|unknown)$")]
    public required string Status { get; init; }

    [RegularExpression("^(bitlocker|filevault|apfs|luks)$")]
    public required string? Method { get; init; }
}

public sealed class NetworkSection
{
    [MaxLength(64)]
    public required List<NetworkInterfaceItem>? Interfaces { get; init; }

    public required PublicNetworkInfo? PublicNetwork { get; init; }
}

public sealed class NetworkInterfaceItem
{
    [Required, SafeText(InventoryContract.TextLength)]
    public required string Name { get; init; }

    [Required, MaxLength(64), IpAddressText]
    public required List<string> Ipv4 { get; init; }

    [Required, MaxLength(64), IpAddressText]
    public required List<string> Ipv6 { get; init; }
}

public sealed class PublicNetworkInfo
{
    [Required, IpAddressText]
    public required string Ip { get; init; }

    [Required, RegularExpression("^[A-Z]{2}$")]
    public required string Country { get; init; }

    [SafeText(InventoryContract.TextLength)]
    public required string? Region { get; init; }

    [SafeText(InventoryContract.TextLength)]
    public required string? City { get; init; }

    [SafeText(InventoryContract.TextLength)]
    public required string? Org { get; init; }
}

public sealed class SoftwareSection
{
    [SafeText(InventoryContract.TextLength, MaxCount = 5000)]
    public required List<string>? Applications { get; init; }
}

public sealed class AccountsSection
{
    [MaxLength(1000)]
    public required List<UserAccountItem>? Users { get; init; }
}

public sealed class UserAccountItem
{
    [Required, SafeText(InventoryContract.TextLength)]
    public required string Name { get; init; }

    public required bool Enabled { get; init; }

    public required bool IsAdmin { get; init; }

    [Required, SafeText(InventoryContract.TextLength, MaxCount = 200)]
    public required List<string> Groups { get; init; }
}

public sealed class SecuritySection
{
    public required FirewallInfo? Firewall { get; init; }
}

public sealed class FirewallInfo
{
    [Required, RegularExpression("^(active|inactive|unknown)$")]
    public required string Status { get; init; }

    [Required, MaxLength(16)]
    public required List<FirewallDetail> Details { get; init; }
}

public sealed class FirewallDetail
{
    [Required, SafeText(InventoryContract.TextLength)]
    public required string Name { get; init; }

    public required bool Enabled { get; init; }
}

public sealed class UnavailableSection
{
    [Required, RegularExpression("^(hardware\\.memory|hardware\\.cpus|hardware\\.disks|hardware\\.volumes|network\\.interfaces|network\\.publicNetwork|software\\.applications|accounts\\.users|security\\.firewall)$")]
    public required string Section { get; init; }

    [Required, RegularExpression("^(permission-denied|not-supported|timeout|error)$")]
    public required string Reason { get; init; }
}
