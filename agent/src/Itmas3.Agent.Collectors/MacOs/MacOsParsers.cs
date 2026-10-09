using System.Globalization;
using System.Text.RegularExpressions;
using Itmas3.Agent.Collectors.Common;
using Itmas3.Agent.Collectors.Model;

namespace Itmas3.Agent.Collectors.MacOs;

/// <summary>Pure parsers of macOS sources (SPEC-0002 §8.3). Tested with real outputs in tests/fixtures/macos.</summary>
public static partial class MacOsParsers
{
    /// <summary>APFS helper volumes of the system container that are sometimes mounted under /Volumes.</summary>
    private static readonly HashSet<string> SystemVolumeNames = new(StringComparer.Ordinal) { "Recovery", "Preboot", "VM", "Update" };

    /// <summary>"Key:\tvalue" lines (sw_vers).</summary>
    public static IReadOnlyDictionary<string, string> ParseKeyValues(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n'))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator > 0)
            {
                values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        return values;
    }

    public static string ParsePlatformUuid(string ioreg) =>
        PlatformUuidPattern().Match(ioreg) is { Success: true } match
            ? match.Groups[1].Value
            : throw new CollectorException(UnavailableReasons.Error, "IOPlatformUUID not found.");

    /// <summary>
    /// <c>sysctl -n hw.memsize machdep.cpu.brand_string hw.physicalcpu hw.logicalcpu hw.packages</c>.
    /// </summary>
    public static (long MemoryBytes, IReadOnlyList<CpuInfo> Cpus) ParseSysctl(string output)
    {
        var lines = output.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        if (lines.Length < 5
            || !long.TryParse(lines[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var memory)
            || !int.TryParse(lines[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var physical)
            || !int.TryParse(lines[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var logical)
            || !int.TryParse(lines[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var packages))
        {
            throw new CollectorException(UnavailableReasons.Error, "Unexpected sysctl output.");
        }

        packages = Math.Max(packages, 1);
        var model = TextSanitizer.Clean(lines[1]) ?? "unknown";
        var cpus = Enumerable.Range(0, packages).Select(_ => new CpuInfo(model, physical / packages, logical / packages)).ToList();
        return (memory, cpus);
    }

    /// <summary>Whole physical disks of <c>diskutil list -plist physical</c>.</summary>
    public static IReadOnlyList<string> ParseWholeDisks(string plist) =>
        Plist.ParseDictionary(plist).List("WholeDisks").OfType<string>().Where(IsDiskIdentifier).ToList();

    /// <summary>
    /// Mounted volumes worth reporting from <c>diskutil list -plist</c>: the system volume (/) and volumes under
    /// /Volumes. The internal /System/Volumes/* (Data, Preboot, VM…) share the system container and are skipped.
    /// </summary>
    public static IReadOnlyList<string> ParseMountedVolumeIdentifiers(string plist)
    {
        var identifiers = new List<string>();
        foreach (var disk in Plist.ParseDictionary(plist).List("AllDisksAndPartitions").OfType<IReadOnlyDictionary<string, object?>>())
        {
            foreach (var entry in new[] { disk }.Concat(disk.List("Partitions").OfType<IReadOnlyDictionary<string, object?>>())
                         .Concat(disk.List("APFSVolumes").OfType<IReadOnlyDictionary<string, object?>>()))
            {
                var mountPoint = entry.String("MountPoint");
                var identifier = entry.String("DeviceIdentifier");
                if (identifier is not null && IsDiskIdentifier(identifier)
                    && mountPoint is not null && (mountPoint == "/" || mountPoint.StartsWith("/Volumes/", StringComparison.Ordinal))
                    && entry.Bool("OSInternal") != true
                    && !SystemVolumeNames.Contains(entry.String("VolumeName") ?? string.Empty))
                {
                    identifiers.Add(identifier);
                }
            }
        }

        return identifiers.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary><c>diskutil info -plist diskN</c> of a whole disk; null for external, removable or virtual media.</summary>
    public static DiskInfo? ParseDisk(string plist)
    {
        var info = Plist.ParseDictionary(plist);
        if (info.Bool("Internal") == false || info.Bool("RemovableMedia") == true || info.Bool("Removable") == true)
        {
            return null;
        }

        var bus = info.String("BusProtocol") switch
        {
            "Apple Fabric" or "PCI-Express" or "PCI" or "NVMe" => BusTypes.Nvme,
            "SATA" => BusTypes.Sata,
            "SAS" => BusTypes.Sas,
            "USB" => BusTypes.Usb,
            "Disk Image" or "Virtual Interface" => BusTypes.Virtual,
            _ => BusTypes.Unknown,
        };
        if (bus == BusTypes.Usb)
        {
            return null;
        }

        var media = info.Bool("SolidState") switch { true => MediaTypes.Ssd, false => MediaTypes.Hdd, null => MediaTypes.Unknown };
        return new DiskInfo(
            info.String("DeviceIdentifier") ?? "unknown",
            TextSanitizer.Clean(info.String("MediaName")),
            info.Long("TotalSize") ?? info.Long("Size") ?? 0,
            media,
            bus);
    }

    /// <summary>
    /// <c>diskutil info -plist &lt;volume&gt;</c>; null for external media. Encryption = FileVault (the "Encryption" key
    /// is true on every Apple Silicon internal volume even without FileVault, so it is not used alone).
    /// </summary>
    public static VolumeInfo? ParseVolume(string plist, string? fileVaultStatus)
    {
        var info = Plist.ParseDictionary(plist);
        var mountPoint = info.String("MountPoint");
        if (mountPoint is null || info.Bool("Internal") == false || info.Bool("RemovableMedia") == true)
        {
            return null;
        }

        var isSystem = mountPoint == "/";
        var isApfs = info.String("FilesystemType") == "apfs";
        var size = (isApfs ? info.Long("APFSContainerSize") : null) ?? info.Long("TotalSize") ?? 0;
        var free = (isApfs ? info.Long("APFSContainerFree") : null) ?? info.Long("FreeSpace") ?? 0;

        var encryption = info.Bool("FileVault") switch
        {
            true => new EncryptionInfo(EncryptionStatus.Encrypted, EncryptionMethods.FileVault),
            false when isSystem && fileVaultStatus is not null && fileVaultStatus.Contains("Encryption in progress", StringComparison.Ordinal)
                => new EncryptionInfo(EncryptionStatus.Encrypting, EncryptionMethods.FileVault),
            false => EncryptionInfo.NotEncrypted,
            null => EncryptionInfo.Unknown,
        };

        return new VolumeInfo(
            TextSanitizer.Clean(mountPoint) ?? mountPoint,
            TextSanitizer.Clean(info.String("FilesystemType"), 64),
            size,
            free,
            PhysicalDisk(info),
            isSystem,
            encryption);
    }

    /// <summary><c>dscl . -list /Users UniqueID</c>: real users (UID ≥ 500, no "_" prefix).</summary>
    public static IReadOnlyList<string> ParseUserList(string output) =>
        output.Split('\n')
            .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 2
                && !parts[0].StartsWith('_')
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid) && uid >= 500
                && IsSafeUserName(parts[0]))
            .Select(parts => parts[0])
            .ToList();

    /// <summary><c>dscl . -read /Users/x AuthenticationAuthority</c>: disabled accounts carry ";DisabledUser;".</summary>
    public static bool ParseEnabled(string authenticationAuthority) =>
        !authenticationAuthority.Contains(";DisabledUser;", StringComparison.Ordinal);

    /// <summary><c>socketfilterfw --getglobalstate</c>.</summary>
    public static bool? ParseApplicationFirewall(string output) =>
        output.Contains("enabled", StringComparison.OrdinalIgnoreCase) ? true
        : output.Contains("disabled", StringComparison.OrdinalIgnoreCase) ? false
        : null;

    /// <summary><c>pfctl -s info</c>.</summary>
    public static bool? ParsePfStatus(string output) =>
        output.Contains("Status: Enabled", StringComparison.Ordinal) ? true
        : output.Contains("Status: Disabled", StringComparison.Ordinal) ? false
        : null;

    /// <summary>Only names that are safe to pass as a single argument to dscl/id.</summary>
    public static bool IsSafeUserName(string name) => SafeUserNamePattern().IsMatch(name);

    public static bool IsDiskIdentifier(string identifier) => DiskIdentifierPattern().IsMatch(identifier);

    private static string? PhysicalDisk(IReadOnlyDictionary<string, object?> info)
    {
        var store = info.List("APFSPhysicalStores").OfType<IReadOnlyDictionary<string, object?>>().Select(item => item.String("APFSPhysicalStore")).FirstOrDefault();
        var identifier = store ?? info.String("ParentWholeDisk");
        return identifier is null ? null : WholeDiskPattern().Match(identifier) is { Success: true } match ? match.Value : identifier;
    }

    [GeneratedRegex("\"IOPlatformUUID\"\\s*=\\s*\"([0-9A-Fa-f-]{36})\"", RegexOptions.CultureInvariant)]
    private static partial Regex PlatformUuidPattern();

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeUserNamePattern();

    [GeneratedRegex("^disk[0-9]+(s[0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex DiskIdentifierPattern();

    [GeneratedRegex("^disk[0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex WholeDiskPattern();
}
