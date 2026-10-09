using Itmas3.Agent.Configuration;
using Itmas3.Agent.Platform;

namespace Itmas3.Agent.Cli;

/// <summary><c>itmas-agent uninstall</c> (RF-5): removes the service, the API key and the agent files.</summary>
public sealed class UninstallCommand(IAgentConsole console, IServiceManager serviceManager, AgentPaths paths)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await serviceManager.StopAndRemoveAsync(cancellationToken);

        foreach (var directory in new[] { paths.DataDirectory, paths.ConfigDirectory, paths.InstallDirectory }.Distinct(StringComparer.Ordinal))
        {
            TryDelete(directory);
        }

        console.WriteLine("El agente se desinstaló. Los inventarios ya enviados se conservan en ITMas.");
        return 0;
    }

    private void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            console.WriteError($"No se pudo eliminar {directory}: {exception.Message}. Elimínelo manualmente.");
        }
    }
}
