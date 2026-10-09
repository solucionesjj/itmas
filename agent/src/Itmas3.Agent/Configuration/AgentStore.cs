using System.Text.Json;

namespace Itmas3.Agent.Configuration;

/// <summary>Reads and writes the agent configuration, state and API key under <see cref="AgentPaths"/>.</summary>
public interface IAgentStore
{
    AgentConfiguration? LoadConfiguration();

    void SaveConfiguration(AgentConfiguration configuration);

    AgentState LoadState();

    void SaveState(AgentState state);

    string? LoadApiKey();

    void SaveApiKey(string apiKey);

    /// <summary>
    /// Random identifier persisted on first use. Fallback hardware id when the OS exposes none (e.g. containers
    /// without /etc/machine-id), so re-installations on the same machine still relink (RN-4).
    /// </summary>
    string GetOrCreateInstallationId();
}

public sealed class AgentStore(AgentPaths paths) : IAgentStore
{
    public AgentConfiguration? LoadConfiguration() =>
        File.Exists(paths.ConfigPath)
            ? JsonSerializer.Deserialize(File.ReadAllText(paths.ConfigPath), AgentJsonContext.Default.AgentConfiguration)
            : null;

    public void SaveConfiguration(AgentConfiguration configuration)
    {
        SecureFiles.EnsureDirectory(paths.ConfigDirectory);
        SecureFiles.WriteText(paths.ConfigPath, JsonSerializer.Serialize(configuration, AgentJsonContext.Default.AgentConfiguration));
    }

    public AgentState LoadState()
    {
        try
        {
            return File.Exists(paths.StatePath)
                ? JsonSerializer.Deserialize(File.ReadAllText(paths.StatePath), AgentJsonContext.Default.AgentState) ?? new AgentState()
                : new AgentState();
        }
        catch (JsonException)
        {
            // A corrupt state only means "never sent": the next run starts immediately.
            return new AgentState();
        }
    }

    public void SaveState(AgentState state)
    {
        SecureFiles.EnsureDirectory(paths.DataDirectory);
        SecureFiles.WriteText(paths.StatePath, JsonSerializer.Serialize(state, AgentJsonContext.Default.AgentState));
    }

    public string? LoadApiKey() => SecureFiles.ReadSecret(paths.CredentialPath);

    public void SaveApiKey(string apiKey)
    {
        SecureFiles.EnsureDirectory(paths.DataDirectory);
        SecureFiles.WriteSecret(paths.CredentialPath, apiKey);
    }

    public string GetOrCreateInstallationId()
    {
        var path = Path.Combine(paths.DataDirectory, "installation-id");
        if (File.Exists(path) && File.ReadAllText(path).Trim() is { Length: >= 8 } existing)
        {
            return existing;
        }

        var id = $"generated-{Guid.NewGuid():N}";
        SecureFiles.EnsureDirectory(paths.DataDirectory);
        SecureFiles.WriteText(path, id);
        return id;
    }
}
