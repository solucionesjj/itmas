using Itmas3.Agent.Collectors.Common;
using Itmas3.Agent.Collectors.Model;
using Microsoft.Extensions.Logging;

namespace Itmas3.Agent.Collectors;

/// <summary>
/// Builds an inventory (contract v1). Every section is collected independently: a failure marks only that section as
/// unavailable, with its reason (RF-9, RN-6), and never blocks the report.
/// </summary>
public sealed partial class InventoryCollector(
    IPlatformInventorySource platform,
    INetworkInterfaceSource networkInterfaces,
    IPublicNetworkSource publicNetwork,
    TimeProvider timeProvider,
    ILogger<InventoryCollector> logger)
{
    public async Task<InventoryReport> CollectAsync(string agentVersion, CancellationToken cancellationToken)
    {
        var unavailable = new List<UnavailableSection>();
        var collectedAt = timeProvider.GetUtcNow();

        var system = await platform.GetSystemAsync(cancellationToken);
        var memory = await TryAsync(Sections.Memory, async ct => (long?)await platform.GetMemoryBytesAsync(ct), unavailable, cancellationToken);
        var cpus = await TryAsync(Sections.Cpus, platform.GetCpusAsync, unavailable, cancellationToken);
        var disks = await TryAsync(Sections.Disks, platform.GetDisksAsync, unavailable, cancellationToken);
        var volumes = await TryAsync(Sections.Volumes, platform.GetVolumesAsync, unavailable, cancellationToken);
        var interfaces = await TryAsync(Sections.Interfaces, _ => Task.FromResult(networkInterfaces.GetInterfaces()), unavailable, cancellationToken);
        var publicInfo = await TryAsync(Sections.PublicNetwork, publicNetwork.GetAsync, unavailable, cancellationToken);
        var applications = await TryAsync(Sections.Applications, platform.GetApplicationsAsync, unavailable, cancellationToken);
        var users = await TryAsync(Sections.Users, platform.GetUsersAsync, unavailable, cancellationToken);
        var firewall = await TryAsync(Sections.Firewall, platform.GetFirewallAsync, unavailable, cancellationToken);

        return new InventoryReport(
            InventoryReport.CurrentSchemaVersion,
            collectedAt,
            agentVersion,
            system,
            new HardwareInfo(memory, cpus, disks, volumes),
            new NetworkInfo(interfaces, publicInfo),
            new SoftwareInfo(applications is null ? null : TextSanitizer.DistinctSorted(applications, 5000)),
            new AccountsInfo(users),
            new SecurityInfo(firewall),
            unavailable);
    }

    private async Task<T?> TryAsync<T>(
        string section, Func<CancellationToken, Task<T>> collect, List<UnavailableSection> unavailable, CancellationToken cancellationToken)
        where T : class?
    {
        try
        {
            return await collect(cancellationToken);
        }
        catch (Exception exception) when (Reason(exception, cancellationToken) is { } reason)
        {
            LogSectionUnavailable(logger, section, reason, exception.GetType().Name, exception.Message);
            unavailable.Add(new UnavailableSection(section, reason));
            return default;
        }
    }

    private async Task<long?> TryAsync(
        string section, Func<CancellationToken, Task<long?>> collect, List<UnavailableSection> unavailable, CancellationToken cancellationToken)
    {
        try
        {
            return await collect(cancellationToken);
        }
        catch (Exception exception) when (Reason(exception, cancellationToken) is { } reason)
        {
            LogSectionUnavailable(logger, section, reason, exception.GetType().Name, exception.Message);
            unavailable.Add(new UnavailableSection(section, reason));
            return null;
        }
    }

    private static string? Reason(Exception exception, CancellationToken cancellationToken) => exception switch
    {
        OperationCanceledException when cancellationToken.IsCancellationRequested => null,
        AggregateException { InnerException: { } inner } => Reason(inner, cancellationToken),
        CollectorException collector => collector.Reason,
        UnauthorizedAccessException => UnavailableReasons.PermissionDenied,
        PlatformNotSupportedException => UnavailableReasons.NotSupported,
        TimeoutException or OperationCanceledException => UnavailableReasons.Timeout,
        _ => UnavailableReasons.Error,
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Inventory section {Section} unavailable ({Reason}): {ExceptionType} {Detail}")]
    private static partial void LogSectionUnavailable(ILogger logger, string section, string reason, string exceptionType, string detail);
}
