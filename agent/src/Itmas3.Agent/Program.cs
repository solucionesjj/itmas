using Itmas3.Agent.Api;
using Itmas3.Agent.Cli;
using Itmas3.Agent.Collectors;
using Itmas3.Agent.Collectors.Common;
using Itmas3.Agent.Collectors.Model;
using Itmas3.Agent.Collectors.Processes;
using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Collectors.SqlAudit.SqlServer;
using Itmas3.Agent.Configuration;
using Itmas3.Agent.Platform;
using Itmas3.Agent.Service;
using Itmas3.Agent.SqlAudit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// ITMas inventory agent (SPEC-0002, ADR-0010) with the SQL audit module (SPEC-0005, ADR-0014). Read-only: collects and
// sends; never accepts commands from the server. Engine connections are configured only here, on the device.
var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
var options = args.Skip(1).ToArray();
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var console = new AgentConsole();
var paths = AgentPaths.ForCurrentPlatform();
var processes = new ProcessRunner();
var files = new SystemFiles();
var store = new AgentStore(paths);

switch (command)
{
    case "run":
        await RunServiceAsync(args);
        return 0;

    case "collect":
        return await CollectAsync(cancellation.Token);

    case "status":
        return await new StatusCommand(console, ServiceManagers.ForCurrentPlatform(processes), store).RunAsync(cancellation.Token);

    case "sql":
        if (!Elevation.IsElevated())
        {
            console.WriteError(OperatingSystem.IsWindows()
                ? "Ejecute este comando en una consola abierta como administrador."
                : "Ejecute este comando con sudo.");
            return 1;
        }

        return await new SqlCommand(
                console,
                new SqlConnectionStore(paths),
                SqlEngineFactory.Create,
                SqlServerCertificateFetcher.FetchAsync,
                Environment.GetEnvironmentVariable,
                TimeProvider.System)
            .RunAsync(options, cancellation.Token);

    case "install":
    case "uninstall":
        if (!Elevation.IsElevated())
        {
            console.WriteError(OperatingSystem.IsWindows()
                ? "Ejecute este comando en una consola abierta como administrador."
                : "Ejecute este comando con sudo.");
            return 1;
        }

        if (command == "uninstall")
        {
            return await new UninstallCommand(console, ServiceManagers.ForCurrentPlatform(processes), paths).RunAsync(cancellation.Token);
        }

        if (ParseInstallOptions(options, console) is not { } installOptions)
        {
            return 2;
        }

        using (var http = ItmasApiClient.CreateHttpClient(AgentRuntime.Version))
        {
            return await new InstallCommand(
                    console,
                    new ItmasApiClient(http),
                    store,
                    ServiceManagers.ForCurrentPlatform(processes),
                    PlatformSources.ForCurrentPlatform(processes, files, TimeProvider.System),
                    paths,
                    TimeProvider.System,
                    Environment.GetEnvironmentVariable)
                .RunAsync(installOptions, cancellation.Token);
        }

    case "--version":
    case "version":
        console.WriteLine(AgentRuntime.Version);
        return 0;

    default:
        console.WriteLine($"""
            Agente de inventario ITMas {AgentRuntime.Version}

            Uso:
              itmas-agent install [--server-url https://…] [--accept-terms] [--enrollment-code-file <ruta>]
                  Muestra los términos, pide la URL del servidor (por defecto {AgentConfiguration.DefaultServerUrl}),
                  comprueba la conexión, pide el código de autorización, enrola el equipo e instala el servicio.
                  También se pueden dar en las variables de entorno {InstallCommand.ServerUrlVariable} y {InstallCommand.EnrollmentCodeVariable}.
              itmas-agent uninstall    Detiene y elimina el servicio y los archivos del agente.
              itmas-agent status       Estado del servicio y del último envío.
              itmas-agent collect      Muestra el inventario de este equipo sin enviarlo.
              itmas-agent sql …        Conexiones de la auditoría SQL: add, update, list, test y remove ('itmas-agent sql' para la ayuda).
              itmas-agent version      Versión del agente.

            install, uninstall y sql requieren privilegios de administrador (sudo en Linux y macOS).
            """);
        return command == "help" ? 0 : 2;
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
static void AddWindowsEventLog(ILoggingBuilder logging) =>
    logging.AddEventLog(new Microsoft.Extensions.Logging.EventLog.EventLogSettings { SourceName = AgentRuntime.EventLogSource });

static InstallOptions? ParseInstallOptions(string[] options, IAgentConsole console)
{
    string? serverUrl = null;
    var acceptTerms = false;
    string? codeFile = null;
    for (var index = 0; index < options.Length; index++)
    {
        switch (options[index])
        {
            case "--server-url" when index + 1 < options.Length:
                serverUrl = options[++index];
                break;
            case "--accept-terms":
                acceptTerms = true;
                break;
            case "--enrollment-code-file" when index + 1 < options.Length:
                codeFile = options[++index];
                break;
            case "--enrollment-code" or "--code":
                // Secrets are never accepted as arguments (constitution §4.5): they would end up in shell history and process lists.
                console.WriteError($"Por seguridad el código no se acepta como argumento. Use la solicitud interactiva, --enrollment-code-file o la variable {InstallCommand.EnrollmentCodeVariable}.");
                return null;
            default:
                console.WriteError($"Opción no reconocida: {options[index]}");
                return null;
        }
    }

    return new InstallOptions(serverUrl, acceptTerms, codeFile);
}

async Task<int> CollectAsync(CancellationToken cancellationToken)
{
    if (!Elevation.IsElevated())
    {
        console.WriteError("Aviso: sin privilegios de administrador algunas secciones aparecerán como no disponibles.");
    }

    using var http = ItmasApiClient.CreateHttpClient(AgentRuntime.Version);
    var collector = new InventoryCollector(
        PlatformSources.ForCurrentPlatform(processes, files, TimeProvider.System),
        new NetworkInterfaceSource(),
        new PublicNetworkSource(http),
        TimeProvider.System,
        NullLogger<InventoryCollector>.Instance);
    var report = await collector.CollectAsync(AgentRuntime.Version, cancellationToken);
    console.WriteLine(InventoryJson.Serialize(report, indented: true));
    return 0;
}

async Task RunServiceAsync(string[] arguments)
{
    var builder = Host.CreateApplicationBuilder(arguments);
    builder.Services.AddWindowsService(options => options.ServiceName = AgentRuntime.WindowsServiceName);
    builder.Services.AddSystemd();
    if (OperatingSystem.IsWindows())
    {
        AddWindowsEventLog(builder.Logging);
    }

    builder.Logging.SetMinimumLevel(LogLevel.Information);
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton(Random.Shared);
    builder.Services.AddSingleton(paths);
    builder.Services.AddSingleton<IAgentStore>(store);
    builder.Services.AddSingleton<IProcessRunner>(processes);
    builder.Services.AddSingleton<ISystemFiles>(files);
    builder.Services.AddSingleton(_ => ItmasApiClient.CreateHttpClient(AgentRuntime.Version));
    builder.Services.AddSingleton<IItmasApiClient, ItmasApiClient>();
    builder.Services.AddSingleton(provider => PlatformSources.ForCurrentPlatform(processes, files, provider.GetRequiredService<TimeProvider>()));
    builder.Services.AddSingleton<INetworkInterfaceSource, NetworkInterfaceSource>();
    builder.Services.AddSingleton<IPublicNetworkSource, PublicNetworkSource>();
    builder.Services.AddSingleton<InventoryCollector>();
    builder.Services.AddSingleton<InventoryCycle>();
    builder.Services.AddSingleton<ISqlConnectionStore>(new SqlConnectionStore(paths));
    builder.Services.AddSingleton<Func<string, ISqlEngine>>(SqlEngineFactory.Create);
    builder.Services.AddSingleton<ISqlAuditCollector, SqlAuditCollector>();
    builder.Services.AddSingleton<SqlAuditCycle>();
    builder.Services.AddHostedService<AgentWorker>();

    await builder.Build().RunAsync();
}
