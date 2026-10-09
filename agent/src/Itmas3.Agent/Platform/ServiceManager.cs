using System.Runtime.Versioning;
using Itmas3.Agent.Collectors.Processes;
using Itmas3.Agent.Configuration;
using Itmas3.Agent.Service;

namespace Itmas3.Agent.Platform;

/// <summary>Registers, starts, stops and removes the OS service (ADR-0010 §3).</summary>
public interface IServiceManager
{
    Task InstallAndStartAsync(AgentPaths paths, CancellationToken cancellationToken);

    Task StopAndRemoveAsync(CancellationToken cancellationToken);

    Task<string> GetStatusAsync(CancellationToken cancellationToken);
}

public static class ServiceManagers
{
    public static IServiceManager ForCurrentPlatform(IProcessRunner processes) =>
        OperatingSystem.IsWindows() ? new WindowsServiceManager(processes)
        : OperatingSystem.IsMacOS() ? new LaunchdServiceManager(processes)
        : new SystemdServiceManager(processes);

    internal static async Task RunCheckedAsync(IProcessRunner processes, string path, string[] arguments, CancellationToken cancellationToken)
    {
        var result = await processes.RunAsync(path, arguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)} {string.Join(' ', arguments.Take(2))} failed ({result.ExitCode}): {result.StandardError.Trim()} {result.StandardOutput.Trim()}".Trim());
        }
    }
}

internal sealed class WindowsServiceManager(IProcessRunner processes) : IServiceManager
{
    private static string Sc => Path.Combine(Environment.SystemDirectory, "sc.exe");

    public async Task InstallAndStartAsync(AgentPaths paths, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows() && !System.Diagnostics.EventLog.SourceExists(AgentRuntime.EventLogSource))
        {
            System.Diagnostics.EventLog.CreateEventSource(AgentRuntime.EventLogSource, "Application");
        }

        await ServiceManagers.RunCheckedAsync(processes, Sc,
            ["create", AgentRuntime.WindowsServiceName, "binPath=", $"\"{paths.ExecutablePath}\" run", "start=", "auto", "DisplayName=", "ITMas Agent"],
            cancellationToken);
        await ServiceManagers.RunCheckedAsync(processes, Sc,
            ["description", AgentRuntime.WindowsServiceName, "Agente de inventario de ITMas (solo lectura; envía el inventario una vez al día)."],
            cancellationToken);
        await ServiceManagers.RunCheckedAsync(processes, Sc,
            ["failure", AgentRuntime.WindowsServiceName, "reset=", "86400", "actions=", "restart/60000/restart/60000/restart/60000"],
            cancellationToken);
        await ServiceManagers.RunCheckedAsync(processes, Sc, ["start", AgentRuntime.WindowsServiceName], cancellationToken);
    }

    public async Task StopAndRemoveAsync(CancellationToken cancellationToken)
    {
        await processes.RunAsync(Sc, ["stop", AgentRuntime.WindowsServiceName], cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        await processes.RunAsync(Sc, ["delete", AgentRuntime.WindowsServiceName], cancellationToken);
    }

    public async Task<string> GetStatusAsync(CancellationToken cancellationToken)
    {
        var result = await processes.RunAsync(Sc, ["query", AgentRuntime.WindowsServiceName], cancellationToken);
        return result.ExitCode != 0 ? "no instalado"
            : result.StandardOutput.Contains("RUNNING", StringComparison.Ordinal) ? "en ejecución"
            : "detenido";
    }
}

[UnsupportedOSPlatform("windows")]
internal sealed class SystemdServiceManager(IProcessRunner processes) : IServiceManager
{
    private const string UnitPath = "/etc/systemd/system/itmas-agent.service";

    private string Systemctl => processes.Exists("/usr/bin/systemctl") ? "/usr/bin/systemctl" : "/bin/systemctl";

    public async Task InstallAndStartAsync(AgentPaths paths, CancellationToken cancellationToken)
    {
        var unit = AgentRuntime.ReadResource("itmas-agent.service").Replace("{{EXECUTABLE}}", paths.ExecutablePath, StringComparison.Ordinal);
        await File.WriteAllTextAsync(UnitPath, unit, cancellationToken);
        File.SetUnixFileMode(UnitPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        await ServiceManagers.RunCheckedAsync(processes, Systemctl, ["daemon-reload"], cancellationToken);
        await ServiceManagers.RunCheckedAsync(processes, Systemctl, ["enable", "--now", AgentRuntime.SystemdUnitName], cancellationToken);
    }

    public async Task StopAndRemoveAsync(CancellationToken cancellationToken)
    {
        await processes.RunAsync(Systemctl, ["disable", "--now", AgentRuntime.SystemdUnitName], cancellationToken);
        File.Delete(UnitPath);
        await processes.RunAsync(Systemctl, ["daemon-reload"], cancellationToken);
    }

    public async Task<string> GetStatusAsync(CancellationToken cancellationToken)
    {
        var result = await processes.RunAsync(Systemctl, ["is-active", AgentRuntime.SystemdUnitName], cancellationToken);
        return result.StandardOutput.Trim() switch
        {
            "active" => "en ejecución",
            "inactive" or "failed" or "activating" => result.StandardOutput.Trim(),
            _ => File.Exists(UnitPath) ? "detenido" : "no instalado",
        };
    }
}

[UnsupportedOSPlatform("windows")]
internal sealed class LaunchdServiceManager(IProcessRunner processes) : IServiceManager
{
    private const string Launchctl = "/bin/launchctl";
    private const string PlistPath = "/Library/LaunchDaemons/cloud.sacservices.itmas.agent.plist";
    private const string NewsyslogPath = "/etc/newsyslog.d/itmas-agent.conf";

    public async Task InstallAndStartAsync(AgentPaths paths, CancellationToken cancellationToken)
    {
        if (paths.LogDirectory is { } logs)
        {
            Directory.CreateDirectory(logs);
        }

        var readable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
        var plist = AgentRuntime.ReadResource("cloud.sacservices.itmas.agent.plist").Replace("{{EXECUTABLE}}", paths.ExecutablePath, StringComparison.Ordinal);
        await File.WriteAllTextAsync(PlistPath, plist, cancellationToken);
        File.SetUnixFileMode(PlistPath, readable);
        await File.WriteAllTextAsync(NewsyslogPath, AgentRuntime.ReadResource("itmas-agent.newsyslog.conf"), cancellationToken);
        File.SetUnixFileMode(NewsyslogPath, readable);

        await ServiceManagers.RunCheckedAsync(processes, Launchctl, ["bootstrap", "system", PlistPath], cancellationToken);
    }

    public async Task StopAndRemoveAsync(CancellationToken cancellationToken)
    {
        await processes.RunAsync(Launchctl, ["bootout", $"system/{AgentRuntime.LaunchdLabel}"], cancellationToken);
        File.Delete(PlistPath);
        File.Delete(NewsyslogPath);
    }

    public async Task<string> GetStatusAsync(CancellationToken cancellationToken)
    {
        var result = await processes.RunAsync(Launchctl, ["print", $"system/{AgentRuntime.LaunchdLabel}"], cancellationToken);
        return result.ExitCode != 0 ? "no instalado"
            : result.StandardOutput.Contains("state = running", StringComparison.Ordinal) ? "en ejecución"
            : "detenido";
    }
}
