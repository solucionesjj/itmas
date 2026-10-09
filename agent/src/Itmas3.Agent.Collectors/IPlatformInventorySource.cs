using Itmas3.Agent.Collectors.Model;

namespace Itmas3.Agent.Collectors;

/// <summary>
/// Operating-system specific sources of the inventory. Each member may throw (<see cref="CollectorException"/>,
/// <see cref="UnauthorizedAccessException"/>, …): <see cref="InventoryCollector"/> turns failures into
/// "unavailable" sections so a partial inventory is still sent (RF-9).
/// </summary>
public interface IPlatformInventorySource
{
    string OsFamily { get; }

    /// <summary>Stable identifier of the machine, used to link re-installations (RN-4).</summary>
    Task<string> GetHardwareIdAsync(CancellationToken cancellationToken);

    /// <summary>Never fails: falls back to runtime information.</summary>
    Task<SystemInfo> GetSystemAsync(CancellationToken cancellationToken);

    Task<long> GetMemoryBytesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CpuInfo>> GetCpusAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DiskInfo>> GetDisksAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<VolumeInfo>> GetVolumesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetApplicationsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<UserAccountInfo>> GetUsersAsync(CancellationToken cancellationToken);

    Task<FirewallInfo> GetFirewallAsync(CancellationToken cancellationToken);
}
