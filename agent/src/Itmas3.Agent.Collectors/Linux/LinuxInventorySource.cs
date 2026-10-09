using Itmas3.Agent.Collectors.Common;
using Itmas3.Agent.Collectors.Model;
using Itmas3.Agent.Collectors.Processes;

namespace Itmas3.Agent.Collectors.Linux;

/// <summary>Linux sources (Ubuntu 22.04/24.04, Debian 12): /proc, /etc, lsblk, lscpu, apt-mark and firewall tools.</summary>
public sealed class LinuxInventorySource(IProcessRunner processes, ISystemFiles files, TimeProvider timeProvider) : IPlatformInventorySource
{
    private const string Lsblk = "/usr/bin/lsblk";
    private const string Lscpu = "/usr/bin/lscpu";
    private const string AptMark = "/usr/bin/apt-mark";
    private const string Ufw = "/usr/sbin/ufw";
    private const string FirewallCmd = "/usr/bin/firewall-cmd";
    private const string Nft = "/usr/sbin/nft";
    private const string Iptables = "/usr/sbin/iptables";

    public string OsFamily => OsFamilies.Linux;

    public async Task<string> GetHardwareIdAsync(CancellationToken cancellationToken)
    {
        foreach (var path in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id", "/sys/class/dmi/id/product_uuid" })
        {
            if (files.FileExists(path) && (await files.ReadAllTextAsync(path, cancellationToken)).Trim() is { Length: >= 8 } id)
            {
                return $"linux-{id}";
            }
        }

        throw new CollectorException(UnavailableReasons.NotSupported, "No machine id found.");
    }

    public async Task<SystemInfo> GetSystemAsync(CancellationToken cancellationToken)
    {
        var release = files.FileExists("/etc/os-release")
            ? LinuxParsers.ParseOsRelease(await files.ReadAllTextAsync("/etc/os-release", cancellationToken))
            : new Dictionary<string, string>();
        var kernel = files.FileExists("/proc/sys/kernel/osrelease")
            ? (await files.ReadAllTextAsync("/proc/sys/kernel/osrelease", cancellationToken)).Trim()
            : Environment.OSVersion.Version.ToString();

        return new SystemInfo(
            TextSanitizer.Clean(Environment.MachineName) ?? "unknown",
            OsFamilies.Linux,
            TextSanitizer.Clean(release.GetValueOrDefault("NAME")) ?? "Linux",
            TextSanitizer.Clean(release.GetValueOrDefault("VERSION_ID")) ?? "unknown",
            TextSanitizer.Clean(kernel),
            RuntimeArchitecture.Current);
    }

    public async Task<long> GetMemoryBytesAsync(CancellationToken cancellationToken) =>
        LinuxParsers.ParseMemTotal(await files.ReadAllTextAsync("/proc/meminfo", cancellationToken));

    public async Task<IReadOnlyList<CpuInfo>> GetCpusAsync(CancellationToken cancellationToken) =>
        LinuxParsers.ParseLscpu(await RunAsync(Lscpu, ["-J"], cancellationToken));

    public async Task<IReadOnlyList<DiskInfo>> GetDisksAsync(CancellationToken cancellationToken) =>
        (await BlockDevicesAsync(cancellationToken)).Disks;

    public async Task<IReadOnlyList<VolumeInfo>> GetVolumesAsync(CancellationToken cancellationToken) =>
        (await BlockDevicesAsync(cancellationToken)).Volumes;

    public async Task<IReadOnlyList<string>> GetApplicationsAsync(CancellationToken cancellationToken) =>
        LinuxParsers.ParseLines(await RunAsync(AptMark, ["showmanual"], cancellationToken));

    public async Task<IReadOnlyList<UserAccountInfo>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var passwd = await files.ReadAllTextAsync("/etc/passwd", cancellationToken);
        var group = await files.ReadAllTextAsync("/etc/group", cancellationToken);
        var loginDefs = files.FileExists("/etc/login.defs") ? await files.ReadAllTextAsync("/etc/login.defs", cancellationToken) : null;
        string? shadow = null;
        try
        {
            shadow = await files.ReadAllTextAsync("/etc/shadow", cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            // Without root, account expiry cannot be checked; the shell still tells disabled accounts.
        }

        return LinuxParsers.ParseUsers(passwd, group, LinuxParsers.ParseUidMin(loginDefs), shadow, DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
    }

    public async Task<FirewallInfo> GetFirewallAsync(CancellationToken cancellationToken)
    {
        var details = new List<FirewallDetail>();
        var failures = new List<CollectorException>();

        async Task CheckAsync(string name, string path, string[] arguments, Func<ProcessResult, bool?> interpret)
        {
            if (!processes.Exists(path))
            {
                return;
            }

            try
            {
                var result = await processes.RunAsync(path, arguments, cancellationToken);
                if (interpret(result) is { } enabled)
                {
                    details.Add(new FirewallDetail(name, enabled));
                }
                else
                {
                    failures.Add(new CollectorException(Reason(result), $"{name} returned an unexpected result."));
                }
            }
            catch (CollectorException exception)
            {
                failures.Add(exception);
            }
        }

        await CheckAsync("ufw", Ufw, ["status"], result => LinuxParsers.ParseUfwStatus(result.StandardOutput));
        await CheckAsync("firewalld", FirewallCmd, ["--state"], result => result.StandardOutput.Trim() switch
        {
            "running" => true,
            "not running" => false,
            _ => null,
        });
        await CheckAsync("nftables", Nft, ["-j", "list", "ruleset"], result => result.ExitCode == 0 ? LinuxParsers.ParseNftHasInputRules(result.StandardOutput) : null);
        await CheckAsync("iptables", Iptables, ["-S", "INPUT"], result => result.ExitCode == 0 ? LinuxParsers.ParseIptablesInputActive(result.StandardOutput) : null);

        if (details.Count == 0 && failures.Count > 0)
        {
            throw failures[0];
        }

        var status = details.Count == 0 ? FirewallStatus.Unknown
            : details.Any(detail => detail.Enabled) ? FirewallStatus.Active
            : FirewallStatus.Inactive;
        return new FirewallInfo(status, details);
    }

    private async Task<(IReadOnlyList<DiskInfo> Disks, IReadOnlyList<VolumeInfo> Volumes)> BlockDevicesAsync(CancellationToken cancellationToken) =>
        LinuxParsers.ParseLsblk(
            await RunAsync(Lsblk, ["-J", "-b", "-o", "NAME,TYPE,SIZE,ROTA,RM,TRAN,MODEL,FSTYPE,MOUNTPOINT"], cancellationToken),
            files.GetSpace);

    private async Task<string> RunAsync(string path, string[] arguments, CancellationToken cancellationToken)
    {
        var result = await processes.RunAsync(path, arguments, cancellationToken);
        return result.ExitCode == 0
            ? result.StandardOutput
            : throw new CollectorException(Reason(result), $"{Path.GetFileName(path)} exited with code {result.ExitCode}.");
    }

    private static string Reason(ProcessResult result) =>
        result.StandardError.Contains("root", StringComparison.OrdinalIgnoreCase)
        || result.StandardError.Contains("permission", StringComparison.OrdinalIgnoreCase)
        || result.StandardError.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase)
            ? UnavailableReasons.PermissionDenied
            : UnavailableReasons.Error;
}
