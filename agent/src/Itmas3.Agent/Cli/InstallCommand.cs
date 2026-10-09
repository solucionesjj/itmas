using Itmas3.Agent.Api;
using Itmas3.Agent.Collectors;
using Itmas3.Agent.Configuration;
using Itmas3.Agent.Platform;
using Itmas3.Agent.Service;

namespace Itmas3.Agent.Cli;

/// <param name="ServerUrl">From --server-url; null when not given.</param>
public sealed record InstallOptions(string? ServerUrl, bool AcceptTerms, string? EnrollmentCodeFile);

/// <summary>
/// <c>itmas-agent install</c> (SPEC-0002 RF-2, RF-3): terms → server URL (argument, environment or question with a
/// default) → HTTPS check of the server → authorization code (hidden, file or environment, never an argument) →
/// enrollment → protected API key → copy to the install directory → service. Rejecting the terms leaves the machine
/// untouched (CA-2); an unreachable or untrusted server is detected before the code is requested.
/// </summary>
public sealed class InstallCommand(
    IAgentConsole console,
    IItmasApiClient apiClient,
    IAgentStore store,
    IServiceManager serviceManager,
    IPlatformInventorySource platform,
    AgentPaths paths,
    TimeProvider timeProvider,
    Func<string, string?> environment)
{
    public const string AcceptanceWord = "ACEPTO";
    public const string EnrollmentCodeVariable = "ITMAS_ENROLLMENT_CODE";
    public const string ServerUrlVariable = "ITMAS_SERVER_URL";
    private const int MaxUrlAttempts = 3;

    public async Task<int> RunAsync(InstallOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var givenUrl = options.ServerUrl ?? environment(ServerUrlVariable);
        if (givenUrl is not null && !AgentConfiguration.IsValidServerUrl(givenUrl))
        {
            console.WriteError("La URL del servidor debe ser una dirección https:// válida (sin usuario, parámetros ni fragmento).");
            return 2;
        }

        console.WriteLine(AgentRuntime.ReadResource($"terms-v{AgentRuntime.TermsVersion}.es.txt"));
        if (!options.AcceptTerms)
        {
            console.WriteLine($"¿Acepta los términos y condiciones? Escriba {AcceptanceWord} para continuar:");
            if (!string.Equals(console.ReadLine()?.Trim(), AcceptanceWord, StringComparison.Ordinal))
            {
                console.WriteLine("No se aceptaron los términos. La instalación se canceló y no se realizó ningún cambio.");
                return 3;
            }
        }

        var termsAcceptedAt = timeProvider.GetUtcNow();
        if ((givenUrl ?? AskServerUrl()) is not { } serverUrl)
        {
            console.WriteError("No se indicó una URL https:// válida. La instalación se canceló.");
            return 2;
        }

        var server = AgentConfiguration.BaseUri(serverUrl);
        console.WriteLine($"Comprobando la conexión con {server} …");
        if (await apiClient.CheckServerAsync(server, cancellationToken) is { } problem)
        {
            console.WriteError($"No se pudo conectar de forma segura con {server}: {problem}");
            console.WriteError("La instalación se canceló y no se realizó ningún cambio.");
            return 4;
        }

        var code = await ReadEnrollmentCodeAsync(options, cancellationToken);
        if (string.IsNullOrWhiteSpace(code))
        {
            console.WriteError("No se indicó el código de autorización. La instalación se canceló.");
            return 2;
        }

        var system = await platform.GetSystemAsync(cancellationToken);
        var request = new EnrollmentRequest(
            await HardwareIdAsync(cancellationToken),
            system.Hostname,
            platform.OsFamily,
            AgentRuntime.Version,
            AgentRuntime.TermsVersion,
            termsAcceptedAt);

        EnrollmentResponse enrollment;
        try
        {
            enrollment = await apiClient.EnrollAsync(server, code.Trim(), request, cancellationToken);
        }
        catch (EnrollmentException exception)
        {
            console.WriteError($"No se pudo enrolar el equipo: {exception.Message}");
            return 4;
        }
        catch (HttpRequestException exception)
        {
            console.WriteError($"No se pudo enrolar el equipo: {ItmasApiClient.DescribeConnectionError(exception)}");
            return 4;
        }
        finally
        {
            code = null;
        }

        // The API key is stored protected; the authorization code is not kept anywhere (RF-2.3).
        store.SaveConfiguration(new AgentConfiguration(AgentConfiguration.Normalize(serverUrl)));
        store.SaveApiKey(enrollment.ApiKey);
        store.SaveState(new AgentState());

        try
        {
            await CopyExecutableAsync(cancellationToken);
            await serviceManager.InstallAndStartAsync(paths, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or Collectors.CollectorException)
        {
            console.WriteError($"El equipo quedó enrolado, pero no se pudo instalar el servicio: {exception.Message}");
            console.WriteError("Corrija el problema y vuelva a ejecutar 'itmas-agent install' (el equipo se volverá a vincular).");
            return 5;
        }

        console.WriteLine($"Equipo enrolado (dispositivo {enrollment.DeviceId}). El servicio está instalado y enviará el primer inventario en unos instantes.");
        return 0;
    }

    /// <summary>
    /// Asks for the server URL, proposing the current one (re-installation) or the default; Enter accepts it.
    /// Without an interactive console the default is used.
    /// </summary>
    private string? AskServerUrl()
    {
        var proposed = store.LoadConfiguration()?.ServerUrl is { } current && AgentConfiguration.IsValidServerUrl(current)
            ? current
            : AgentConfiguration.DefaultServerUrl;
        if (!console.IsInteractive)
        {
            return proposed;
        }

        for (var attempt = 0; attempt < MaxUrlAttempts; attempt++)
        {
            console.WriteLine($"URL del servidor ITMas al que se enviará el inventario [{proposed}] (Enter para aceptar):");
            var answer = console.ReadLine()?.Trim();
            var candidate = string.IsNullOrEmpty(answer) ? proposed : answer;
            if (AgentConfiguration.IsValidServerUrl(candidate))
            {
                return candidate;
            }

            console.WriteError("La URL debe empezar por https:// y no llevar usuario, parámetros ni fragmento.");
        }

        return null;
    }

    private async Task<string> HardwareIdAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await platform.GetHardwareIdAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is Collectors.CollectorException or UnauthorizedAccessException or IOException)
        {
            return store.GetOrCreateInstallationId();
        }
    }

    private async Task<string?> ReadEnrollmentCodeAsync(InstallOptions options, CancellationToken cancellationToken)
    {
        if (options.EnrollmentCodeFile is { } file)
        {
            return File.Exists(file) ? (await File.ReadAllTextAsync(file, cancellationToken)).Trim() : null;
        }

        if (environment(EnrollmentCodeVariable) is { Length: > 0 } fromEnvironment)
        {
            return fromEnvironment;
        }

        console.WriteLine("Código de autorización (no se mostrará al escribirlo):");
        return console.ReadSecret();
    }

    private async Task CopyExecutableAsync(CancellationToken cancellationToken)
    {
        var current = Environment.ProcessPath;
        if (current is null || string.Equals(Path.GetFullPath(current), Path.GetFullPath(paths.ExecutablePath), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Directory.CreateDirectory(paths.InstallDirectory);
        await using (var source = File.OpenRead(current))
        await using (var target = File.Create(paths.ExecutablePath))
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        if (!OperatingSystem.IsWindows())
        {
            // root-owned, writable only by root (constitution §13.4).
            File.SetUnixFileMode(paths.ExecutablePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute);
        }
    }
}
