using System.Reflection;

namespace Itmas3.Agent.Service;

public static class AgentRuntime
{
    public const string WindowsServiceName = "ITMasAgent";
    public const string SystemdUnitName = "itmas-agent";
    public const string LaunchdLabel = "cloud.sacservices.itmas.agent";
    public const string EventLogSource = "ITMas Agent";
    public const string TermsVersion = "1";

    public static string Version { get; } =
        typeof(AgentRuntime).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public static string ReadResource(string name)
    {
        using var stream = typeof(AgentRuntime).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource {name} not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
