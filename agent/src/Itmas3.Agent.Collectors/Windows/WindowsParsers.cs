using Itmas3.Agent.Collectors.Common;
using Itmas3.Agent.Collectors.Model;

namespace Itmas3.Agent.Collectors.Windows;

/// <summary>Uninstall-key values of one registry entry.</summary>
public sealed record UninstallEntry(string? DisplayName, int? SystemComponent, string? ParentKeyName, string? ReleaseType);

/// <summary>A local account or group (key = "DOMAIN\Name"), with the members of groups.</summary>
public sealed record LocalPrincipal(string Key, string Name, bool IsGroup, bool Disabled, string? Sid, IReadOnlyList<string> Members);

/// <summary>Pure interpretation of Windows sources (SPEC-0002 §8.3), testable on any OS.</summary>
public static class WindowsParsers
{
    public const string AdministratorsSid = "S-1-5-32-544";

    /// <summary>Windows 11 still reports "Windows 10" in ProductName; it is recognised by build ≥ 22000 on client editions.</summary>
    public static string OsName(string? productName, int? build, string? installationType)
    {
        var name = TextSanitizer.Clean(productName) ?? "Windows";
        return build >= 22000 && !string.Equals(installationType, "Server", StringComparison.OrdinalIgnoreCase)
            ? name.Replace("Windows 10", "Windows 11", StringComparison.Ordinal)
            : name;
    }

    /// <summary>Entries shown in "Installed apps": named, not system components, not updates.</summary>
    public static bool IsListedApplication(UninstallEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return !string.IsNullOrWhiteSpace(entry.DisplayName)
            && entry.SystemComponent != 1
            && string.IsNullOrEmpty(entry.ParentKeyName)
            && entry.ReleaseType is not ("Update" or "Hotfix" or "Security Update" or "Service Pack");
    }

    /// <summary>MSFT_PhysicalDisk.BusType → contract value; null for removable buses (USB, SD, MMC), which are excluded.</summary>
    public static string? BusType(int busType) => busType switch
    {
        17 => BusTypes.Nvme,
        11 or 3 => BusTypes.Sata,
        10 => BusTypes.Sas,
        1 or 6 or 9 => BusTypes.Scsi,
        14 or 15 or 16 => BusTypes.Virtual,
        7 or 12 or 13 => null,
        _ => BusTypes.Unknown,
    };

    /// <summary>MSFT_PhysicalDisk.MediaType: 3 HDD, 4 SSD, 5 SCM.</summary>
    public static string MediaType(int mediaType) => mediaType switch
    {
        3 => MediaTypes.Hdd,
        4 or 5 => MediaTypes.Ssd,
        _ => MediaTypes.Unknown,
    };

    /// <summary>Win32_EncryptableVolume ProtectionStatus (0 off, 1 on) and ConversionStatus (0..5).</summary>
    public static EncryptionInfo BitLocker(int? protectionStatus, int? conversionStatus) => conversionStatus switch
    {
        1 when protectionStatus == 1 => new EncryptionInfo(EncryptionStatus.Encrypted, EncryptionMethods.BitLocker),
        1 => new EncryptionInfo(EncryptionStatus.Suspended, EncryptionMethods.BitLocker),
        2 or 4 => new EncryptionInfo(EncryptionStatus.Encrypting, EncryptionMethods.BitLocker),
        0 or 3 or 5 => EncryptionInfo.NotEncrypted,
        _ => EncryptionInfo.Unknown,
    };

    /// <summary>
    /// SecurityCenter2 FirewallProduct.productState: the second byte is 0x10/0x11 when the product is enabled.
    /// </summary>
    public static bool ThirdPartyFirewallEnabled(uint productState) => ((productState >> 8) & 0xFF) is 0x10 or 0x11;

    /// <summary>Active when the three Windows Firewall profiles are on, or a third-party firewall is on (P-14).</summary>
    public static string FirewallStatus(IReadOnlyList<FirewallDetail> profiles, IReadOnlyList<FirewallDetail> thirdParty)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(thirdParty);
        if (profiles.Count == 0 && thirdParty.Count == 0)
        {
            return Model.FirewallStatus.Unknown;
        }

        return (profiles.Count == 3 && profiles.All(profile => profile.Enabled)) || thirdParty.Any(product => product.Enabled)
            ? Model.FirewallStatus.Active
            : Model.FirewallStatus.Inactive;
    }

    /// <summary>
    /// Local users with the local groups they belong to (directly or through nested local groups). Administrator:
    /// member of the group whose SID is S-1-5-32-544.
    /// </summary>
    public static IReadOnlyList<UserAccountInfo> ResolveUsers(IReadOnlyList<LocalPrincipal> principals)
    {
        ArgumentNullException.ThrowIfNull(principals);
        var groups = principals.Where(principal => principal.IsGroup).ToList();
        var result = new List<UserAccountInfo>();

        foreach (var user in principals.Where(principal => !principal.IsGroup))
        {
            var memberOf = new List<LocalPrincipal>();
            var pending = new Queue<string>([user.Key]);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (pending.TryDequeue(out var key))
            {
                foreach (var group in groups.Where(group => group.Members.Contains(key, StringComparer.OrdinalIgnoreCase)))
                {
                    if (visited.Add(group.Key))
                    {
                        memberOf.Add(group);
                        pending.Enqueue(group.Key);
                    }
                }
            }

            result.Add(new UserAccountInfo(
                user.Name,
                !user.Disabled,
                memberOf.Any(group => string.Equals(group.Sid, AdministratorsSid, StringComparison.OrdinalIgnoreCase)),
                TextSanitizer.DistinctSorted(memberOf.Select(group => group.Name), 200)));
        }

        return result;
    }

    /// <summary>Escapes a value for a WQL string literal (values come from WMI itself, never from the network).</summary>
    public static string WqlLiteral(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);
    }

    /// <summary>SMBIOS UUIDs made of zeros or Fs are placeholders of some firmware/VMs.</summary>
    public static bool IsUsableUuid(string? uuid) =>
        uuid is { Length: >= 32 } && uuid.Replace("-", string.Empty, StringComparison.Ordinal).Distinct().Count() > 1;
}
