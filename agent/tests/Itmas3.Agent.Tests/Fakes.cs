using System.Net;
using Itmas3.Agent.Api;
using Itmas3.Agent.Collectors;
using Itmas3.Agent.Collectors.Common;
using Itmas3.Agent.Collectors.Model;
using Itmas3.Agent.Collectors.Processes;
using Itmas3.Agent.Configuration;
using Itmas3.Agent.Platform;

namespace Itmas3.Agent.Tests;

internal sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakePlatform : IPlatformInventorySource
{
    public Exception? FirewallFailure { get; init; }

    public Exception? UsersFailure { get; init; }

    public string OsFamily => OsFamilies.Linux;

    public Task<string> GetHardwareIdAsync(CancellationToken cancellationToken) => Task.FromResult("linux-0123456789abcdef");

    public Task<SystemInfo> GetSystemAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SystemInfo("srv-app-01", OsFamilies.Linux, "Ubuntu", "24.04", "6.8.0-45-generic", "x64"));

    public Task<long> GetMemoryBytesAsync(CancellationToken cancellationToken) => Task.FromResult(17179869184L);

    public Task<IReadOnlyList<CpuInfo>> GetCpusAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CpuInfo>>([new CpuInfo("Intel(R) Xeon(R)", 8, 16)]);

    public Task<IReadOnlyList<DiskInfo>> GetDisksAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DiskInfo>>([new DiskInfo("nvme0n1", "Samsung", 1000204886016, MediaTypes.Ssd, BusTypes.Nvme)]);

    public Task<IReadOnlyList<VolumeInfo>> GetVolumesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<VolumeInfo>>([new VolumeInfo("/", "ext4", 100, 50, "nvme0n1", true, new EncryptionInfo(EncryptionStatus.Encrypted, EncryptionMethods.Luks))]);

    public Task<IReadOnlyList<string>> GetApplicationsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(["nginx", "openssh-server", "nginx", "  ", "Bad\u0007Name"]);

    public Task<IReadOnlyList<UserAccountInfo>> GetUsersAsync(CancellationToken cancellationToken) =>
        UsersFailure is null
            ? Task.FromResult<IReadOnlyList<UserAccountInfo>>([new UserAccountInfo("root", true, true, ["root"])])
            : Task.FromException<IReadOnlyList<UserAccountInfo>>(UsersFailure);

    public Task<FirewallInfo> GetFirewallAsync(CancellationToken cancellationToken) =>
        FirewallFailure is null
            ? Task.FromResult(new FirewallInfo(FirewallStatus.Active, [new FirewallDetail("ufw", true)]))
            : Task.FromException<FirewallInfo>(FirewallFailure);
}

internal sealed class FakeInterfaces : INetworkInterfaceSource
{
    public IReadOnlyList<NetworkInterfaceInfo> GetInterfaces() => [new NetworkInterfaceInfo("eth0", ["10.0.0.15"], [])];
}

internal sealed class FakePublicNetwork(Exception? failure = null) : IPublicNetworkSource
{
    public Task<PublicNetworkInfo> GetAsync(CancellationToken cancellationToken) =>
        failure is null
            ? Task.FromResult(new PublicNetworkInfo("203.0.113.25", "CO", "Bogota D.C.", "Bogotá", "AS64500 Example"))
            : Task.FromException<PublicNetworkInfo>(failure);
}

internal sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> Bodies { get; } = [];

    public static StubHandler Returning(HttpStatusCode status, string body = "{}") =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") }));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        return await respond(request, cancellationToken);
    }
}

internal sealed class InMemoryStore : IAgentStore
{
    public AgentConfiguration? Configuration { get; set; }

    public AgentState State { get; set; } = new();

    public string? ApiKey { get; set; }

    public int Writes { get; private set; }

    public AgentConfiguration? LoadConfiguration() => Configuration;

    public void SaveConfiguration(AgentConfiguration configuration)
    {
        Configuration = configuration;
        Writes++;
    }

    public AgentState LoadState() => State;

    public void SaveState(AgentState state)
    {
        State = state;
        Writes++;
    }

    public string? LoadApiKey() => ApiKey;

    public void SaveApiKey(string apiKey)
    {
        ApiKey = apiKey;
        Writes++;
    }

    public string GetOrCreateInstallationId() => "generated-installation-id";
}

internal sealed class RecordingApiClient : IItmasApiClient
{
    public Exception? EnrollFailure { get; init; }

    public SubmitOutcome SubmitResult { get; init; } = SubmitOutcome.Accepted;

    public string? ServerProblem { get; init; }

    public List<Uri> CheckedServers { get; } = [];

    public Task<string?> CheckServerAsync(Uri serverUrl, CancellationToken cancellationToken)
    {
        CheckedServers.Add(serverUrl);
        return Task.FromResult(ServerProblem);
    }

    public List<(string Code, EnrollmentRequest Request)> Enrollments { get; } = [];

    public List<InventoryReport> Submitted { get; } = [];

    public Task<EnrollmentResponse> EnrollAsync(Uri serverUrl, string enrollmentCode, EnrollmentRequest request, CancellationToken cancellationToken)
    {
        Enrollments.Add((enrollmentCode, request));
        return EnrollFailure is null
            ? Task.FromResult(new EnrollmentResponse(Guid.NewGuid(), "device-api-key-placeholder"))
            : Task.FromException<EnrollmentResponse>(EnrollFailure);
    }

    public Task<(SubmitOutcome Outcome, string Detail)> SubmitAsync(Uri serverUrl, string apiKey, InventoryReport report, CancellationToken cancellationToken)
    {
        Submitted.Add(report);
        return Task.FromResult((SubmitResult, SubmitResult.ToString()));
    }
}

internal sealed class RecordingServiceManager : IServiceManager
{
    public bool Installed { get; private set; }

    public Task InstallAndStartAsync(AgentPaths paths, CancellationToken cancellationToken)
    {
        Installed = true;
        return Task.CompletedTask;
    }

    public Task StopAndRemoveAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<string> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult("en ejecución");
}

internal sealed class ScriptedConsole(params string?[] inputs) : Cli.IAgentConsole
{
    private readonly Queue<string?> _inputs = new(inputs);

    public List<string> Output { get; } = [];

    public int SecretReads { get; private set; }

    public bool IsInteractive { get; init; } = true;

    public void WriteLine(string text = "") => Output.Add(text);

    public void WriteError(string text) => Output.Add(text);

    public string? ReadLine() => _inputs.TryDequeue(out var value) ? value : null;

    public string? ReadSecret()
    {
        SecretReads++;
        return _inputs.TryDequeue(out var value) ? value : null;
    }
}

internal sealed class FixtureProcessRunner : IProcessRunner
{
    private readonly Dictionary<string, ProcessResult> _results = new(StringComparer.Ordinal);

    public FixtureProcessRunner Returns(string path, string[] arguments, string output, int exitCode = 0, string error = "")
    {
        _results[Key(path, arguments)] = new ProcessResult(exitCode, output, error);
        return this;
    }

    public bool Exists(string executablePath) => _results.Keys.Any(key => key.StartsWith(executablePath + " ", StringComparison.Ordinal) || key == executablePath);

    public Task<ProcessResult> RunAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        _results.TryGetValue(Key(executablePath, arguments), out var result)
            ? Task.FromResult(result)
            : throw new CollectorException(UnavailableReasons.NotSupported, $"Unexpected command {Key(executablePath, arguments)}");

    private static string Key(string path, IReadOnlyList<string> arguments) => arguments.Count == 0 ? path : $"{path} {string.Join(' ', arguments)}";
}
