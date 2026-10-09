using System.Globalization;
using Itmas3.Agent.Configuration;
using Itmas3.Agent.Platform;
using Itmas3.Agent.Service;

namespace Itmas3.Agent.Cli;

/// <summary><c>itmas-agent status</c> (RF-6). Never shows the API key.</summary>
public sealed class StatusCommand(IAgentConsole console, IServiceManager serviceManager, IAgentStore store)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var state = store.LoadState();
        var configuration = store.LoadConfiguration();

        console.WriteLine($"Agente ITMas {AgentRuntime.Version}");
        string service;
        try
        {
            service = await serviceManager.GetStatusAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is Collectors.CollectorException or InvalidOperationException or IOException)
        {
            service = "no disponible (no se encontró el gestor de servicios)";
        }

        console.WriteLine($"Servicio:          {service}");
        console.WriteLine($"Servidor:          {configuration?.ServerUrl ?? "(no enrolado)"}");
        console.WriteLine($"Enrolado:          {(store.LoadApiKey() is null ? "no" : "sí")}");
        console.WriteLine($"Último envío:      {Format(state.LastSuccessAt)}");
        console.WriteLine($"Próximo envío:     {Format(state.NextRunAt)}");
        console.WriteLine($"Último error:      {state.LastError ?? "-"}");
        return 0;
    }

    private static string Format(DateTimeOffset? value) =>
        value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) ?? "-";
}
