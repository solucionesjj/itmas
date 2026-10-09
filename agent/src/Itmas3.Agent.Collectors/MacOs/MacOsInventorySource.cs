using Itmas3.Agent.Collectors.Common;
using Itmas3.Agent.Collectors.Model;
using Itmas3.Agent.Collectors.Processes;

namespace Itmas3.Agent.Collectors.MacOs;

/// <summary>macOS sources: sw_vers, sysctl, ioreg, diskutil, fdesetup, dscl, id, socketfilterfw and pfctl.</summary>
public sealed class MacOsInventorySource(IProcessRunner processes, ISystemFiles files) : IPlatformInventorySource
{
    private const string SwVers = "/usr/bin/sw_vers";
    private const string Sysctl = "/usr/sbin/sysctl";
    private const string Ioreg = "/usr/sbin/ioreg";
    private const string Scutil = "/usr/sbin/scutil";
    private const string Diskutil = "/usr/sbin/diskutil";
    private const string Fdesetup = "/usr/bin/fdesetup";
    private const string Dscl = "/usr/bin/dscl";
    private const string Id = "/usr/bin/id";
    private const string SocketFilterFw = "/usr/libexec/ApplicationFirewall/socketfilterfw";
    private const string Pfctl = "/sbin/pfctl";

    public string OsFamily => OsFamilies.MacOs;

    public async Task<string> GetHardwareIdAsync(CancellationToken cancellationToken) =>
        $"macos-{MacOsParsers.ParsePlatformUuid(await RunAsync(Ioreg, ["-rd1", "-c", "IOPlatformExpertDevice"], cancellationToken))}";

    public async Task<SystemInfo> GetSystemAsync(CancellationToken cancellationToken)
    {
        var versions = new Dictionary<string, string>(StringComparer.Ordinal);
        string? computerName = null;
        try
        {
            versions = MacOsParsers.ParseKeyValues(await RunAsync(SwVers, [], cancellationToken)).ToDictionary(StringComparer.Ordinal);
            computerName = (await RunAsync(Scutil, ["--get", "ComputerName"], cancellationToken)).Trim();
        }
        catch (CollectorException)
        {
            // Fall back to runtime information below.
        }

        return new SystemInfo(
            TextSanitizer.Clean(computerName) ?? TextSanitizer.Clean(Environment.MachineName) ?? "unknown",
            OsFamilies.MacOs,
            TextSanitizer.Clean(versions.GetValueOrDefault("ProductName")) ?? "macOS",
            TextSanitizer.Clean(versions.GetValueOrDefault("ProductVersion")) ?? Environment.OSVersion.Version.ToString(),
            TextSanitizer.Clean(versions.GetValueOrDefault("BuildVersion")),
            RuntimeArchitecture.Current);
    }

    public async Task<long> GetMemoryBytesAsync(CancellationToken cancellationToken) => (await SysctlAsync(cancellationToken)).MemoryBytes;

    public async Task<IReadOnlyList<CpuInfo>> GetCpusAsync(CancellationToken cancellationToken) => (await SysctlAsync(cancellationToken)).Cpus;

    public async Task<IReadOnlyList<DiskInfo>> GetDisksAsync(CancellationToken cancellationToken)
    {
        var disks = new List<DiskInfo>();
        foreach (var disk in MacOsParsers.ParseWholeDisks(await RunAsync(Diskutil, ["list", "-plist", "physical"], cancellationToken)))
        {
            if (MacOsParsers.ParseDisk(await RunAsync(Diskutil, ["info", "-plist", disk], cancellationToken)) is { } info)
            {
                disks.Add(info);
            }
        }

        return disks;
    }

    public async Task<IReadOnlyList<VolumeInfo>> GetVolumesAsync(CancellationToken cancellationToken)
    {
        string? fileVault = null;
        try
        {
            fileVault = await RunAsync(Fdesetup, ["status"], cancellationToken);
        }
        catch (CollectorException)
        {
            // diskutil's FileVault key is still available.
        }

        var volumes = new List<VolumeInfo>();
        foreach (var identifier in MacOsParsers.ParseMountedVolumeIdentifiers(await RunAsync(Diskutil, ["list", "-plist"], cancellationToken)))
        {
            if (MacOsParsers.ParseVolume(await RunAsync(Diskutil, ["info", "-plist", identifier], cancellationToken), fileVault) is { } volume)
            {
                volumes.Add(volume);
            }
        }

        return volumes;
    }

    public Task<IReadOnlyList<string>> GetApplicationsAsync(CancellationToken cancellationToken)
    {
        const string root = "/Applications";
        if (!files.DirectoryExists(root))
        {
            throw new CollectorException(UnavailableReasons.NotSupported, "/Applications not found.");
        }

        var applications = new List<string>();
        foreach (var directory in files.EnumerateDirectories(root))
        {
            if (directory.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                applications.Add(Path.GetFileNameWithoutExtension(directory));
            }
            else
            {
                // One level of folders such as /Applications/Utilities.
                applications.AddRange(files.EnumerateDirectories(directory)
                    .Where(child => child.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFileNameWithoutExtension)
                    .OfType<string>());
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(applications);
    }

    public async Task<IReadOnlyList<UserAccountInfo>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var users = new List<UserAccountInfo>();
        foreach (var name in MacOsParsers.ParseUserList(await RunAsync(Dscl, [".", "-list", "/Users", "UniqueID"], cancellationToken)))
        {
            // Names come from the local directory and are validated by IsSafeUserName before being used as an argument.
            var groups = TextSanitizer.DistinctSorted((await RunAsync(Id, ["-Gn", name], cancellationToken)).Split(' ', StringSplitOptions.RemoveEmptyEntries), 200);
            var authority = await processes.RunAsync(Dscl, [".", "-read", $"/Users/{name}", "AuthenticationAuthority"], cancellationToken);
            users.Add(new UserAccountInfo(name, MacOsParsers.ParseEnabled(authority.StandardOutput), groups.Contains("admin", StringComparer.Ordinal), groups));
        }

        return users;
    }

    public async Task<FirewallInfo> GetFirewallAsync(CancellationToken cancellationToken)
    {
        var application = MacOsParsers.ParseApplicationFirewall(await RunAsync(SocketFilterFw, ["--getglobalstate"], cancellationToken))
            ?? throw new CollectorException(UnavailableReasons.Error, "Unexpected socketfilterfw output.");
        var details = new List<FirewallDetail> { new("Firewall de aplicaciones", application) };

        try
        {
            var pf = await processes.RunAsync(Pfctl, ["-s", "info"], cancellationToken);
            if (MacOsParsers.ParsePfStatus(pf.StandardOutput) is { } pfEnabled)
            {
                details.Add(new FirewallDetail("pf", pfEnabled));
            }
        }
        catch (CollectorException)
        {
            // pf is additional information only (requires root).
        }

        return new FirewallInfo(application ? FirewallStatus.Active : FirewallStatus.Inactive, details);
    }

    private async Task<(long MemoryBytes, IReadOnlyList<CpuInfo> Cpus)> SysctlAsync(CancellationToken cancellationToken) =>
        MacOsParsers.ParseSysctl(await RunAsync(
            Sysctl, ["-n", "hw.memsize", "machdep.cpu.brand_string", "hw.physicalcpu", "hw.logicalcpu", "hw.packages"], cancellationToken));

    private async Task<string> RunAsync(string path, string[] arguments, CancellationToken cancellationToken)
    {
        var result = await processes.RunAsync(path, arguments, cancellationToken);
        return result.ExitCode == 0
            ? result.StandardOutput
            : throw new CollectorException(
                result.StandardError.Contains("ermission", StringComparison.Ordinal) ? UnavailableReasons.PermissionDenied : UnavailableReasons.Error,
                $"{Path.GetFileName(path)} exited with code {result.ExitCode}.");
    }
}
