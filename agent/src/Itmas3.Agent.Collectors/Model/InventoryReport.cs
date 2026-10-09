namespace Itmas3.Agent.Collectors.Model;

// Inventory contract v1 (SPEC-0002 §12.2). Every property is always serialized (nulls included): the API is strict.
// Keep in sync with agent/tests/fixtures/contract/inventory-report.v1.json (verified by ContractTests).

public sealed record InventoryReport(
    int SchemaVersion,
    DateTimeOffset CollectedAt,
    string AgentVersion,
    SystemInfo System,
    HardwareInfo Hardware,
    NetworkInfo Network,
    SoftwareInfo Software,
    AccountsInfo Accounts,
    SecurityInfo Security,
    IReadOnlyList<UnavailableSection> Unavailable)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record SystemInfo(string Hostname, string OsFamily, string OsName, string OsVersion, string? OsBuild, string Architecture);

public sealed record HardwareInfo(long? MemoryBytes, IReadOnlyList<CpuInfo>? Cpus, IReadOnlyList<DiskInfo>? Disks, IReadOnlyList<VolumeInfo>? Volumes);

public sealed record CpuInfo(string Model, int PhysicalCores, int LogicalProcessors);

public sealed record DiskInfo(string Id, string? Model, long SizeBytes, string MediaType, string BusType);

public sealed record VolumeInfo(
    string MountPoint,
    string? FileSystem,
    long SizeBytes,
    long FreeBytes,
    string? DiskId,
    bool IsSystem,
    EncryptionInfo Encryption);

public sealed record EncryptionInfo(string Status, string? Method)
{
    public static EncryptionInfo Unknown { get; } = new(EncryptionStatus.Unknown, null);

    public static EncryptionInfo NotEncrypted { get; } = new(EncryptionStatus.NotEncrypted, null);
}

public sealed record NetworkInfo(IReadOnlyList<NetworkInterfaceInfo>? Interfaces, PublicNetworkInfo? PublicNetwork);

public sealed record NetworkInterfaceInfo(string Name, IReadOnlyList<string> Ipv4, IReadOnlyList<string> Ipv6);

public sealed record PublicNetworkInfo(string Ip, string Country, string? Region, string? City, string? Org);

public sealed record SoftwareInfo(IReadOnlyList<string>? Applications);

public sealed record AccountsInfo(IReadOnlyList<UserAccountInfo>? Users);

public sealed record UserAccountInfo(string Name, bool Enabled, bool IsAdmin, IReadOnlyList<string> Groups);

public sealed record SecurityInfo(FirewallInfo? Firewall);

public sealed record FirewallInfo(string Status, IReadOnlyList<FirewallDetail> Details);

public sealed record FirewallDetail(string Name, bool Enabled);

public sealed record UnavailableSection(string Section, string Reason);

/// <summary>Closed values of the contract.</summary>
public static class OsFamilies
{
    public const string Windows = "windows";
    public const string Linux = "linux";
    public const string MacOs = "macos";
}

public static class MediaTypes
{
    public const string Ssd = "ssd";
    public const string Hdd = "hdd";
    public const string Unknown = "unknown";
}

public static class BusTypes
{
    public const string Nvme = "nvme";
    public const string Sata = "sata";
    public const string Sas = "sas";
    public const string Scsi = "scsi";
    public const string Usb = "usb";
    public const string Virtual = "virtual";
    public const string Unknown = "unknown";
}

public static class EncryptionStatus
{
    public const string Encrypted = "encrypted";
    public const string Encrypting = "encrypting";
    public const string Suspended = "suspended";
    public const string NotEncrypted = "not-encrypted";
    public const string Unknown = "unknown";
}

public static class EncryptionMethods
{
    public const string BitLocker = "bitlocker";
    public const string FileVault = "filevault";
    public const string Apfs = "apfs";
    public const string Luks = "luks";
}

public static class FirewallStatus
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Unknown = "unknown";
}

public static class Sections
{
    public const string Memory = "hardware.memory";
    public const string Cpus = "hardware.cpus";
    public const string Disks = "hardware.disks";
    public const string Volumes = "hardware.volumes";
    public const string Interfaces = "network.interfaces";
    public const string PublicNetwork = "network.publicNetwork";
    public const string Applications = "software.applications";
    public const string Users = "accounts.users";
    public const string Firewall = "security.firewall";
}

public static class UnavailableReasons
{
    public const string PermissionDenied = "permission-denied";
    public const string NotSupported = "not-supported";
    public const string Timeout = "timeout";
    public const string Error = "error";
}
