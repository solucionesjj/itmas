using System.Management;
using System.Runtime.Versioning;
using Itmas3.Agent.Collectors.Common;
using Itmas3.Agent.Collectors.Model;
using Microsoft.Win32;

namespace Itmas3.Agent.Collectors.Windows;

/// <summary>Windows 11 / Server 2019-2025 sources: registry, WMI (CIMV2, Storage, BitLocker) and SecurityCenter2.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsInventorySource : IPlatformInventorySource
{
    private const string Cimv2 = @"root\CIMV2";
    private const string StorageNamespace = @"root\Microsoft\Windows\Storage";
    private const string BitLockerNamespace = @"root\CIMV2\Security\MicrosoftVolumeEncryption";
    private const string SecurityCenterNamespace = @"root\SecurityCenter2";
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string FirewallPolicyPath = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy";
    private const string FirewallGroupPolicyPath = @"SOFTWARE\Policies\Microsoft\WindowsFirewall";

    public string OsFamily => OsFamilies.Windows;

    public Task<string> GetHardwareIdAsync(CancellationToken cancellationToken) => Run(() =>
    {
        var uuid = Query(Cimv2, "SELECT UUID FROM Win32_ComputerSystemProduct").Select(item => item["UUID"] as string).FirstOrDefault();
        if (WindowsParsers.IsUsableUuid(uuid))
        {
            return $"windows-{uuid}";
        }

        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        return key?.GetValue("MachineGuid") is string guid
            ? $"windows-{guid}"
            : throw new CollectorException(UnavailableReasons.NotSupported, "No machine identifier found.");
    });

    public Task<SystemInfo> GetSystemAsync(CancellationToken cancellationToken)
    {
        string? productName = null, displayVersion = null, installationType = null, build = null;
        int? ubr = null;
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            productName = key?.GetValue("ProductName") as string;
            displayVersion = key?.GetValue("DisplayVersion") as string ?? key?.GetValue("ReleaseId") as string;
            installationType = key?.GetValue("InstallationType") as string;
            build = key?.GetValue("CurrentBuild") as string;
            ubr = key?.GetValue("UBR") as int?;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Fall back to runtime information below.
        }

        var buildNumber = int.TryParse(build, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number)
            ? number
            : Environment.OSVersion.Version.Build;

        return Task.FromResult(new SystemInfo(
            TextSanitizer.Clean(Environment.MachineName) ?? "unknown",
            OsFamilies.Windows,
            WindowsParsers.OsName(productName, buildNumber, installationType),
            TextSanitizer.Clean(displayVersion) ?? Environment.OSVersion.Version.ToString(),
            ubr is null ? buildNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{buildNumber}.{ubr}",
            RuntimeArchitecture.Current));
    }

    public Task<long> GetMemoryBytesAsync(CancellationToken cancellationToken) => Run(() =>
        Query(Cimv2, "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem")
            .Select(item => Convert.ToInt64(item["TotalPhysicalMemory"], System.Globalization.CultureInfo.InvariantCulture))
            .First());

    public Task<IReadOnlyList<CpuInfo>> GetCpusAsync(CancellationToken cancellationToken) => Run<IReadOnlyList<CpuInfo>>(() =>
        Query(Cimv2, "SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor")
            .Select(item => new CpuInfo(
                TextSanitizer.Clean(item["Name"] as string) ?? "unknown",
                ToInt(item["NumberOfCores"]),
                ToInt(item["NumberOfLogicalProcessors"])))
            .ToList());

    public Task<IReadOnlyList<DiskInfo>> GetDisksAsync(CancellationToken cancellationToken) => Run<IReadOnlyList<DiskInfo>>(() =>
    {
        var disks = new List<DiskInfo>();
        foreach (var item in Query(StorageNamespace, "SELECT DeviceId, FriendlyName, Size, MediaType, BusType FROM MSFT_PhysicalDisk"))
        {
            if (WindowsParsers.BusType(ToInt(item["BusType"])) is not { } bus)
            {
                continue;
            }

            disks.Add(new DiskInfo(
                item["DeviceId"]?.ToString() ?? "unknown",
                TextSanitizer.Clean(item["FriendlyName"] as string),
                ToLong(item["Size"]),
                WindowsParsers.MediaType(ToInt(item["MediaType"])),
                bus));
        }

        return disks;
    });

    public Task<IReadOnlyList<VolumeInfo>> GetVolumesAsync(CancellationToken cancellationToken) => Run<IReadOnlyList<VolumeInfo>>(() =>
    {
        var encryption = BitLockerVolumes();
        var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
        var volumes = new List<VolumeInfo>();

        // DriveType 3 = local fixed disk (removable, network and optical drives are excluded).
        foreach (var item in Query(Cimv2, "SELECT DeviceID, FileSystem, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 3"))
        {
            var deviceId = item["DeviceID"] as string ?? string.Empty;
            var diskIndex = Query(Cimv2, $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{WindowsParsers.WqlLiteral(deviceId)}'}} WHERE AssocClass=Win32_LogicalDiskToPartition")
                .Select(partition => partition["DiskIndex"]?.ToString())
                .FirstOrDefault();

            volumes.Add(new VolumeInfo(
                deviceId + "\\",
                TextSanitizer.Clean(item["FileSystem"] as string, 64),
                ToLong(item["Size"]),
                ToLong(item["FreeSpace"]),
                diskIndex,
                string.Equals(deviceId, systemDrive, StringComparison.OrdinalIgnoreCase),
                encryption is null ? EncryptionInfo.Unknown : encryption.GetValueOrDefault(deviceId, EncryptionInfo.NotEncrypted)));
        }

        return volumes;
    });

    public Task<IReadOnlyList<string>> GetApplicationsAsync(CancellationToken cancellationToken) => Run<IReadOnlyList<string>>(() =>
    {
        var names = new List<string>();
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            names.AddRange(ReadUninstallKey(baseKey, UninstallPath));
        }

        // Per-user installations of the loaded profiles (P-18).
        foreach (var sid in Registry.Users.GetSubKeyNames()
                     .Where(sid => sid.StartsWith("S-1-5-21-", StringComparison.Ordinal) && !sid.EndsWith("_Classes", StringComparison.Ordinal)))
        {
            names.AddRange(ReadUninstallKey(Registry.Users, $@"{sid}\{UninstallPath}"));
        }

        return names;
    });

    public Task<IReadOnlyList<UserAccountInfo>> GetUsersAsync(CancellationToken cancellationToken) => Run(() =>
    {
        var principals = new List<LocalPrincipal>();
        foreach (var user in Query(Cimv2, "SELECT Domain, Name, Disabled, SID FROM Win32_UserAccount WHERE LocalAccount = TRUE"))
        {
            var name = user["Name"] as string ?? string.Empty;
            principals.Add(new LocalPrincipal($"{user["Domain"]}\\{name}", name, IsGroup: false, user["Disabled"] is true, user["SID"] as string, []));
        }

        foreach (var group in Query(Cimv2, "SELECT Domain, Name, SID FROM Win32_Group WHERE LocalAccount = TRUE"))
        {
            var domain = group["Domain"] as string ?? string.Empty;
            var name = group["Name"] as string ?? string.Empty;
            var members = Query(
                    Cimv2,
                    $"ASSOCIATORS OF {{Win32_Group.Domain='{WindowsParsers.WqlLiteral(domain)}',Name='{WindowsParsers.WqlLiteral(name)}'}} WHERE AssocClass=Win32_GroupUser Role=GroupComponent ResultRole=PartComponent")
                .Select(member => $"{member["Domain"]}\\{member["Name"]}")
                .ToList();
            principals.Add(new LocalPrincipal($"{domain}\\{name}", name, IsGroup: true, Disabled: false, group["SID"] as string, members));
        }

        return WindowsParsers.ResolveUsers(principals);
    });

    public Task<FirewallInfo> GetFirewallAsync(CancellationToken cancellationToken) => Run(() =>
    {
        var profiles = new List<FirewallDetail>();
        foreach (var (localName, policyName, label) in new[]
                 {
                     ("DomainProfile", "DomainProfile", "Firewall de Windows (dominio)"),
                     ("StandardProfile", "PrivateProfile", "Firewall de Windows (privado)"),
                     ("PublicProfile", "PublicProfile", "Firewall de Windows (público)"),
                 })
        {
            var enabled = ReadDword($@"{FirewallGroupPolicyPath}\{policyName}", "EnableFirewall")
                ?? ReadDword($@"{FirewallPolicyPath}\{localName}", "EnableFirewall");
            if (enabled is { } value)
            {
                profiles.Add(new FirewallDetail(label, value != 0));
            }
        }

        var thirdParty = new List<FirewallDetail>();
        try
        {
            // Security Center exists only on client editions (Windows 11), not on Windows Server.
            foreach (var product in Query(SecurityCenterNamespace, "SELECT displayName, productState FROM FirewallProduct"))
            {
                if (TextSanitizer.Clean(product["displayName"] as string) is { } name)
                {
                    thirdParty.Add(new FirewallDetail(name, WindowsParsers.ThirdPartyFirewallEnabled(Convert.ToUInt32(product["productState"], System.Globalization.CultureInfo.InvariantCulture))));
                }
            }
        }
        catch (ManagementException)
        {
            // Namespace not available.
        }

        return new FirewallInfo(WindowsParsers.FirewallStatus(profiles, thirdParty), [.. profiles, .. thirdParty]);
    });

    private static Dictionary<string, EncryptionInfo>? BitLockerVolumes()
    {
        try
        {
            return Query(BitLockerNamespace, "SELECT DriveLetter, ProtectionStatus, ConversionStatus FROM Win32_EncryptableVolume")
                .Where(item => item["DriveLetter"] is string)
                .GroupBy(item => (string)item["DriveLetter"], StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => WindowsParsers.BitLocker(ToNullableInt(group.First()["ProtectionStatus"]), ToNullableInt(group.First()["ConversionStatus"])),
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (ManagementException)
        {
            // BitLocker WMI provider missing (e.g. feature not installed on Server) or access denied: unknown.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static List<string> ReadUninstallKey(RegistryKey root, string path)
    {
        var names = new List<string>();
        using var uninstall = root.OpenSubKey(path);
        if (uninstall is null)
        {
            return names;
        }

        foreach (var subKeyName in uninstall.GetSubKeyNames())
        {
            using var entry = uninstall.OpenSubKey(subKeyName);
            if (entry is null)
            {
                continue;
            }

            var candidate = new UninstallEntry(
                entry.GetValue("DisplayName") as string,
                entry.GetValue("SystemComponent") as int?,
                entry.GetValue("ParentKeyName") as string,
                entry.GetValue("ReleaseType") as string);
            if (WindowsParsers.IsListedApplication(candidate))
            {
                names.Add(candidate.DisplayName!);
            }
        }

        return names;
    }

    private static int? ReadDword(string path, string name)
    {
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(path);
        return key?.GetValue(name) as int?;
    }

    private static List<ManagementBaseObject> Query(string scope, string wql)
    {
        using var searcher = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(wql));
        try
        {
            return searcher.Get().Cast<ManagementBaseObject>().ToList();
        }
        catch (ManagementException exception) when (exception.ErrorCode == ManagementStatus.AccessDenied)
        {
            throw new UnauthorizedAccessException($"Access denied to {scope}.", exception);
        }
    }

    private static Task<T> Run<T>(Func<T> collect)
    {
        try
        {
            return Task.FromResult(collect());
        }
        catch (ManagementException exception) when (exception.ErrorCode is ManagementStatus.InvalidNamespace or ManagementStatus.InvalidClass)
        {
            throw new CollectorException(UnavailableReasons.NotSupported, exception.Message, exception);
        }
        catch (ManagementException exception)
        {
            throw new CollectorException(UnavailableReasons.Error, exception.Message, exception);
        }
        catch (System.Security.SecurityException exception)
        {
            throw new UnauthorizedAccessException(exception.Message, exception);
        }
    }

    private static int ToInt(object? value) => value is null ? 0 : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);

    private static int? ToNullableInt(object? value) => value is null ? null : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);

    private static long ToLong(object? value) => value is null ? 0 : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
}
