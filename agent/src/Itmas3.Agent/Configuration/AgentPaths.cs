namespace Itmas3.Agent.Configuration;

/// <summary>Installation, data and log locations per operating system (ADR-0010 §6).</summary>
public sealed record AgentPaths(
    string InstallDirectory,
    string ExecutablePath,
    string DataDirectory,
    string ConfigDirectory,
    string? LogDirectory)
{
    public string CredentialPath => Path.Combine(DataDirectory, "credential");

    public string StatePath => Path.Combine(DataDirectory, "state.json");

    public string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public static AgentPaths ForCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            var install = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ITMas", "Agent");
            var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ITMas", "Agent");
            return new AgentPaths(install, Path.Combine(install, "itmas-agent.exe"), data, data, null);
        }

        if (OperatingSystem.IsMacOS())
        {
            const string root = "/Library/Application Support/ITMas/Agent";
            return new AgentPaths(root, Path.Combine(root, "itmas-agent"), root, root, "/Library/Logs/ITMas");
        }

        return new AgentPaths("/opt/itmas-agent", "/opt/itmas-agent/itmas-agent", "/var/lib/itmas-agent", "/etc/itmas-agent", null);
    }
}
