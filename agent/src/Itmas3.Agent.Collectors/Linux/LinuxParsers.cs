using System.Globalization;
using System.Text.Json;
using Itmas3.Agent.Collectors.Common;
using Itmas3.Agent.Collectors.Model;

namespace Itmas3.Agent.Collectors.Linux;

/// <summary>Pure parsers of Linux sources (SPEC-0002 §8.3). Tested with real outputs in tests/fixtures/linux.</summary>
public static class LinuxParsers
{
    private static readonly HashSet<string> AdminGroups = new(StringComparer.Ordinal) { "sudo", "wheel", "admin" };
    private static readonly HashSet<string> DisabledShells = new(StringComparer.Ordinal)
    {
        "/usr/sbin/nologin", "/sbin/nologin", "/bin/false", "/usr/bin/false",
    };

    private static readonly string[] IgnoredDiskPrefixes = ["zram", "nbd", "loop", "ram", "sr"];

    private static readonly HashSet<string> IgnoredFileSystems = new(StringComparer.Ordinal)
    {
        "swap", "squashfs", "iso9660", "udf", "tmpfs", "overlay", "devtmpfs",
    };

    /// <summary>KEY=value lines of /etc/os-release (quotes removed).</summary>
    public static IReadOnlyDictionary<string, string> ParseOsRelease(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in Lines(text))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0 && !line.StartsWith('#'))
            {
                values[line[..separator]] = line[(separator + 1)..].Trim().Trim('"', '\'');
            }
        }

        return values;
    }

    /// <summary>MemTotal of /proc/meminfo, in bytes.</summary>
    public static long ParseMemTotal(string meminfo)
    {
        foreach (var line in Lines(meminfo))
        {
            if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kilobytes))
                {
                    return kilobytes * 1024;
                }
            }
        }

        throw new CollectorException(UnavailableReasons.Error, "MemTotal not found in /proc/meminfo.");
    }

    /// <summary>
    /// <c>lscpu -J</c> (flat or nested). One entry per socket (or cluster on ARM) of each "Model name" block.
    /// </summary>
    public static IReadOnlyList<CpuInfo> ParseLscpu(string json)
    {
        using var document = JsonDocument.Parse(json);
        var fields = new List<(string Field, string Data)>();
        if (document.RootElement.TryGetProperty("lscpu", out var root))
        {
            Flatten(root, fields);
        }

        if (fields.Count == 0)
        {
            throw new CollectorException(UnavailableReasons.Error, "lscpu returned no data.");
        }

        // Without any "Model name" (some ARM virtual machines), all fields form a single group named after the vendor.
        var vendor = fields.FirstOrDefault(item => item.Field == "Vendor ID:").Data;
        if (!fields.Any(item => item.Field == "Model name:"))
        {
            fields.Insert(0, ("Model name:", "-"));
        }

        var cpus = new List<CpuInfo>();
        string? model = null;
        var group = new Dictionary<string, string>(StringComparer.Ordinal);

        void Emit()
        {
            if (model is null)
            {
                return;
            }

            var sockets = Number(group, "Socket(s):") ?? Number(group, "Cluster(s):") ?? 1;
            var cores = Number(group, "Core(s) per socket:") ?? Number(group, "Core(s) per cluster:") ?? 1;
            var threads = Number(group, "Thread(s) per core:") ?? 1;
            for (var socket = 0; socket < sockets; socket++)
            {
                cpus.Add(new CpuInfo(ModelName(model, vendor), cores, cores * threads));
            }
        }

        foreach (var (field, data) in fields)
        {
            if (field == "Model name:")
            {
                Emit();
                model = data;
                group.Clear();
            }
            else if (model is not null)
            {
                group[field] = data;
            }
        }

        Emit();
        return cpus.Count > 0 ? cpus : throw new CollectorException(UnavailableReasons.Error, "lscpu did not report a CPU model.");
    }

    /// <summary>
    /// <c>lsblk -J -b -o NAME,TYPE,SIZE,ROTA,RM,TRAN,MODEL,FSTYPE,MOUNTPOINT</c>: fixed physical disks and mounted volumes.
    /// A volume is encrypted when a <c>crypt</c> device (dm-crypt/LUKS) is among its ancestors.
    /// </summary>
    public static (IReadOnlyList<DiskInfo> Disks, IReadOnlyList<VolumeInfo> Volumes) ParseLsblk(
        string json, Func<string, (long Total, long Free)?> space)
    {
        ArgumentNullException.ThrowIfNull(space);
        using var document = JsonDocument.Parse(json);
        var disks = new List<DiskInfo>();
        var volumes = new Dictionary<string, VolumeInfo>(StringComparer.Ordinal);

        foreach (var device in document.RootElement.GetProperty("blockdevices").EnumerateArray())
        {
            if (JsonValues.String(device, "type") != "disk" || JsonValues.Bool(device, "rm") == true)
            {
                continue;
            }

            var name = JsonValues.String(device, "name") ?? "unknown";
            var transport = JsonValues.String(device, "tran");
            var model = TextSanitizer.Clean(JsonValues.String(device, "model"));
            var size = JsonValues.Long(device, "size") ?? 0;
            if (transport == "usb" || size == 0 || IgnoredDiskPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            {
                continue;
            }

            var bus = Bus(name, transport, model);
            var media = bus == BusTypes.Virtual ? MediaTypes.Unknown
                : JsonValues.Bool(device, "rota") switch { true => MediaTypes.Hdd, false => MediaTypes.Ssd, null => MediaTypes.Unknown };
            disks.Add(new DiskInfo(name, model, size, media, bus));
            CollectVolumes(device, name, encrypted: false, volumes, space);
        }

        return (disks, volumes.Values.OrderBy(volume => volume.MountPoint, StringComparer.Ordinal).ToList());
    }

    /// <summary>One package per line (<c>apt-mark showmanual</c>).</summary>
    public static IReadOnlyList<string> ParseLines(string text) =>
        Lines(text).Select(line => line.Trim()).Where(line => line.Length > 0).ToList();

    /// <summary>UID_MIN of /etc/login.defs (default 1000).</summary>
    public static int ParseUidMin(string? loginDefs)
    {
        foreach (var line in Lines(loginDefs ?? string.Empty))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts is ["UID_MIN", var value, ..] && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uidMin))
            {
                return uidMin;
            }
        }

        return 1000;
    }

    /// <summary>
    /// Local users (UID 0 and UID ≥ UID_MIN, except nobody) with their groups (primary included).
    /// Administrator: UID 0 or member of sudo/wheel/admin. Disabled: no-login shell or expired account (shadow).
    /// </summary>
    public static IReadOnlyList<UserAccountInfo> ParseUsers(string passwd, string group, int uidMin, string? shadow, DateOnly today)
    {
        var groupsById = new Dictionary<int, string>();
        var membership = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var fields in Lines(group).Select(line => line.Split(':')).Where(fields => fields.Length >= 4))
        {
            if (int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var gid))
            {
                groupsById.TryAdd(gid, fields[0]);
            }

            foreach (var member in fields[3].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!membership.TryGetValue(member, out var list))
                {
                    membership[member] = list = [];
                }

                list.Add(fields[0]);
            }
        }

        var expired = ExpiredAccounts(shadow, today);
        var users = new List<UserAccountInfo>();
        foreach (var fields in Lines(passwd).Select(line => line.Split(':')).Where(fields => fields.Length >= 7))
        {
            if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid)
                || (uid != 0 && uid < uidMin) || uid == 65534 || TextSanitizer.Clean(fields[0]) is not { } name)
            {
                continue;
            }

            var groups = new List<string>();
            if (int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var gid) && groupsById.TryGetValue(gid, out var primary))
            {
                groups.Add(primary);
            }

            groups.AddRange(membership.GetValueOrDefault(name) ?? []);
            var distinctGroups = TextSanitizer.DistinctSorted(groups, 200);
            var enabled = !DisabledShells.Contains(fields[6].Trim()) && !expired.Contains(name);
            users.Add(new UserAccountInfo(name, enabled, uid == 0 || distinctGroups.Any(AdminGroups.Contains), distinctGroups));
        }

        return users;
    }

    /// <summary><c>ufw status</c>: true when active, false when inactive, null when unrecognised.</summary>
    public static bool? ParseUfwStatus(string output)
    {
        var line = Lines(output).FirstOrDefault(item => item.StartsWith("Status:", StringComparison.Ordinal));
        return line?.Trim() switch
        {
            "Status: active" => true,
            "Status: inactive" => false,
            _ => null,
        };
    }

    /// <summary><c>nft -j list ruleset</c>: true when any chain hooked on input has at least one rule or a drop policy.</summary>
    public static bool ParseNftHasInputRules(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("nftables", out var items))
        {
            return false;
        }

        var inputChains = new HashSet<(string?, string?, string?)>();
        var dropPolicy = false;
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("chain", out var chain) && JsonValues.String(chain, "hook") == "input")
            {
                inputChains.Add((JsonValues.String(chain, "family"), JsonValues.String(chain, "table"), JsonValues.String(chain, "name")));
                dropPolicy |= JsonValues.String(chain, "policy") == "drop";
            }
        }

        return dropPolicy || items.EnumerateArray().Any(item =>
            item.TryGetProperty("rule", out var rule)
            && inputChains.Contains((JsonValues.String(rule, "family"), JsonValues.String(rule, "table"), JsonValues.String(rule, "chain"))));
    }

    /// <summary><c>iptables -S INPUT</c>: true when the INPUT chain has rules or a DROP/REJECT policy.</summary>
    public static bool ParseIptablesInputActive(string output) =>
        Lines(output).Any(line =>
            line.StartsWith("-A INPUT", StringComparison.Ordinal)
            || line.Trim() is "-P INPUT DROP" or "-P INPUT REJECT");

    private static void CollectVolumes(
        JsonElement node, string diskId, bool encrypted, Dictionary<string, VolumeInfo> volumes, Func<string, (long Total, long Free)?> space)
    {
        encrypted |= JsonValues.String(node, "type") == "crypt";
        var mountPoint = JsonValues.String(node, "mountpoint");
        var fileSystem = JsonValues.String(node, "fstype");

        if (mountPoint is { Length: > 0 } && mountPoint.StartsWith('/') && !IsIgnoredMount(mountPoint, fileSystem) && !volumes.ContainsKey(mountPoint))
        {
            var (total, free) = space(mountPoint) ?? (JsonValues.Long(node, "size") ?? 0, 0);
            volumes[mountPoint] = new VolumeInfo(
                TextSanitizer.Clean(mountPoint) ?? mountPoint,
                TextSanitizer.Clean(fileSystem, 64),
                total,
                free,
                diskId,
                mountPoint == "/",
                encrypted ? new EncryptionInfo(EncryptionStatus.Encrypted, EncryptionMethods.Luks) : EncryptionInfo.NotEncrypted);
        }

        if (node.TryGetProperty("children", out var children))
        {
            foreach (var child in children.EnumerateArray())
            {
                CollectVolumes(child, diskId, encrypted, volumes, space);
            }
        }
    }

    // Mounts without a file system type are bind mounts of files (e.g. /etc/hosts inside containers), not volumes.
    private static bool IsIgnoredMount(string mountPoint, string? fileSystem) =>
        fileSystem is null
        || IgnoredFileSystems.Contains(fileSystem)
        || mountPoint == "/boot/efi"
        || mountPoint.StartsWith("/snap/", StringComparison.Ordinal)
        || mountPoint.StartsWith("/var/snap/", StringComparison.Ordinal);

    private static string Bus(string name, string? transport, string? model) => transport switch
    {
        "nvme" => BusTypes.Nvme,
        "sata" or "ata" => BusTypes.Sata,
        "sas" => BusTypes.Sas,
        "spi" or "iscsi" or "fc" => BusTypes.Scsi,
        "virtio" => BusTypes.Virtual,
        _ when name.StartsWith("nvme", StringComparison.Ordinal) => BusTypes.Nvme,
        _ when name.StartsWith("vd", StringComparison.Ordinal) || name.StartsWith("xvd", StringComparison.Ordinal) => BusTypes.Virtual,
        _ when model is not null && (model.Contains("QEMU", StringComparison.OrdinalIgnoreCase)
            || model.Contains("VMware", StringComparison.OrdinalIgnoreCase)
            || model.Contains("Virtual", StringComparison.OrdinalIgnoreCase)) => BusTypes.Virtual,
        _ => BusTypes.Unknown,
    };

    private static HashSet<string> ExpiredAccounts(string? shadow, DateOnly today)
    {
        var expired = new HashSet<string>(StringComparer.Ordinal);
        var todayDays = today.DayNumber - DateOnly.FromDateTime(DateTime.UnixEpoch).DayNumber;
        foreach (var fields in Lines(shadow ?? string.Empty).Select(line => line.Split(':')).Where(fields => fields.Length >= 8))
        {
            if (long.TryParse(fields[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expireDays) && expireDays <= todayDays)
            {
                expired.Add(fields[0]);
            }
        }

        return expired;
    }

    private static void Flatten(JsonElement items, List<(string Field, string Data)> fields)
    {
        foreach (var item in items.EnumerateArray())
        {
            fields.Add((JsonValues.String(item, "field") ?? string.Empty, JsonValues.String(item, "data") ?? string.Empty));
            if (item.TryGetProperty("children", out var children))
            {
                Flatten(children, fields);
            }
        }
    }

    private static string ModelName(string? model, string? vendor) =>
        TextSanitizer.Clean(model is null or "-" ? vendor : model) is { } name && name != "-" ? name : "unknown";

    private static int? Number(Dictionary<string, string> group, string field) =>
        group.TryGetValue(field, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > 0
            ? number
            : null;

    private static IEnumerable<string> Lines(string text) => text.Split('\n').Select(line => line.TrimEnd('\r'));
}
