using Itmas3.Agent.Api;
using Itmas3.Agent.Cli;
using Itmas3.Agent.Configuration;

namespace Itmas3.Agent.Tests;

public sealed class InstallCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("itmas-agent-test").FullName;
    private readonly InMemoryStore _store = new();
    private readonly RecordingServiceManager _services = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task CA02_rejecting_the_terms_changes_nothing()
    {
        var api = new RecordingApiClient();
        var console = new ScriptedConsole("no");

        var exit = await Command(console, api).RunAsync(Options(), Fixtures.Ct);

        Assert.Equal(3, exit);
        Assert.Empty(api.Enrollments);
        Assert.Equal(0, _store.Writes);
        Assert.False(_services.Installed);
        Assert.Equal(0, console.SecretReads);
        Assert.False(Directory.Exists(Path.Combine(_root, "install")));
        Assert.Contains(console.Output, line => line.Contains("TÉRMINOS Y CONDICIONES", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CA03_code_is_read_hidden_and_only_the_device_key_is_stored()
    {
        var api = new RecordingApiClient();
        var console = new ScriptedConsole(InstallCommand.AcceptanceWord, "  code-placeholder-not-a-secret  ");

        var exit = await Command(console, api).RunAsync(Options(), Fixtures.Ct);

        Assert.Equal(0, exit);
        Assert.Equal(1, console.SecretReads);
        var (code, request) = Assert.Single(api.Enrollments);
        Assert.Equal("code-placeholder-not-a-secret", code);
        Assert.Equal("1", request.TermsVersion);
        Assert.Equal("linux-0123456789abcdef", request.HardwareId);
        Assert.Equal("device-api-key-placeholder", _store.ApiKey);
        Assert.Equal("https://itmas.example.test", _store.Configuration!.ServerUrl);
        Assert.True(_services.Installed);
        Assert.DoesNotContain(console.Output, line => line.Contains("code-placeholder-not-a-secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Code_can_come_from_a_file_or_the_environment_but_not_from_arguments()
    {
        var file = Path.Combine(_root, "code.txt");
        await File.WriteAllTextAsync(file, "from-file-placeholder\n", Fixtures.Ct);
        var api = new RecordingApiClient();

        await Command(new ScriptedConsole(), api).RunAsync(Options(acceptTerms: true, codeFile: file), Fixtures.Ct);
        await Command(new ScriptedConsole(), api, name => name == InstallCommand.EnrollmentCodeVariable ? "from-env-placeholder" : null)
            .RunAsync(Options(acceptTerms: true), Fixtures.Ct);

        Assert.Equal(["from-file-placeholder", "from-env-placeholder"], api.Enrollments.Select(item => item.Code));
    }

    [Fact]
    public async Task Failed_enrollment_stores_nothing_and_installs_nothing()
    {
        var api = new RecordingApiClient { EnrollFailure = new EnrollmentException("El código de autorización no es válido.") };
        var console = new ScriptedConsole(InstallCommand.AcceptanceWord, "wrong-placeholder");

        var exit = await Command(console, api).RunAsync(Options(), Fixtures.Ct);

        Assert.Equal(4, exit);
        Assert.Null(_store.ApiKey);
        Assert.False(_services.Installed);
        Assert.Contains(console.Output, line => line.Contains("no es válido", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Server_url_question_accepts_the_default_with_enter()
    {
        var api = new RecordingApiClient();

        Assert.Equal(0, await Command(new ScriptedConsole(InstallCommand.AcceptanceWord, "", "code-placeholder"), api).RunAsync(Options(serverUrl: null), Fixtures.Ct));

        Assert.Equal(AgentConfiguration.DefaultServerUrl, _store.Configuration!.ServerUrl);
        Assert.Equal(new Uri("https://itmas.sacservices.cloud/"), Assert.Single(api.CheckedServers));
    }

    [Fact]
    public async Task Server_url_question_repeats_until_a_valid_https_url_is_given()
    {
        var console = new ScriptedConsole(InstallCommand.AcceptanceWord, "http://itmas.local.test", "https://itmas.local.test/itmas/", "code-placeholder");

        Assert.Equal(0, await Command(console, new RecordingApiClient()).RunAsync(Options(serverUrl: null), Fixtures.Ct));

        Assert.Equal("https://itmas.local.test/itmas", _store.Configuration!.ServerUrl);
        Assert.Contains(console.Output, line => line.Contains("debe empezar por https://", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reinstallation_proposes_the_configured_server()
    {
        _store.Configuration = new AgentConfiguration("https://itmas.previous.test");
        var console = new ScriptedConsole(InstallCommand.AcceptanceWord, "", "code-placeholder");

        await Command(console, new RecordingApiClient()).RunAsync(Options(serverUrl: null), Fixtures.Ct);

        Assert.Contains(console.Output, line => line.Contains("[https://itmas.previous.test]", StringComparison.Ordinal));
        Assert.Equal("https://itmas.previous.test", _store.Configuration!.ServerUrl);
    }

    [Fact]
    public async Task Server_url_can_come_from_the_environment_and_scripts_use_the_default()
    {
        var api = new RecordingApiClient();
        await Command(new ScriptedConsole(), api, name => name switch
        {
            InstallCommand.ServerUrlVariable => "https://itmas.env.test",
            InstallCommand.EnrollmentCodeVariable => "code-placeholder",
            _ => null,
        }).RunAsync(Options(serverUrl: null, acceptTerms: true), Fixtures.Ct);
        Assert.Equal("https://itmas.env.test", _store.Configuration!.ServerUrl);

        _store.Configuration = null;
        await Command(new ScriptedConsole { IsInteractive = false }, api, name => name == InstallCommand.EnrollmentCodeVariable ? "code-placeholder" : null)
            .RunAsync(Options(serverUrl: null, acceptTerms: true), Fixtures.Ct);
        Assert.Equal(AgentConfiguration.DefaultServerUrl, _store.Configuration!.ServerUrl);
    }

    [Fact]
    public async Task An_unreachable_or_untrusted_server_cancels_before_asking_for_the_code()
    {
        var api = new RecordingApiClient { ServerProblem = "el certificado HTTPS del servidor no es válido para esa dirección o no es de confianza." };
        var console = new ScriptedConsole(InstallCommand.AcceptanceWord, "code-placeholder");

        Assert.Equal(4, await Command(console, api).RunAsync(Options(), Fixtures.Ct));

        Assert.Equal(0, console.SecretReads);
        Assert.Empty(api.Enrollments);
        Assert.Equal(0, _store.Writes);
        Assert.Contains(console.Output, line => line.Contains("certificado HTTPS", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Service_installation_failures_are_reported_without_a_crash()
    {
        var console = new ScriptedConsole(InstallCommand.AcceptanceWord, "code-placeholder");
        var paths = new AgentPaths(Path.Combine(_root, "install"), Path.Combine(_root, "install", "itmas-agent"), Path.Combine(_root, "data"), Path.Combine(_root, "data"), null);
        var command = new InstallCommand(console, new RecordingApiClient(), _store, new FailingServiceManager(), new FakePlatform(), paths, TimeProvider.System, _ => null);

        Assert.Equal(5, await command.RunAsync(Options(), Fixtures.Ct));
        Assert.Contains(console.Output, line => line.Contains("no se pudo instalar el servicio", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("http://itmas.example.test")]
    [InlineData("https://user:pass@itmas.example.test")]
    [InlineData("ftp://itmas.example.test")]
    [InlineData("itmas.example.test")]
    public async Task Only_https_server_urls_are_accepted(string url)
    {
        var api = new RecordingApiClient();

        Assert.Equal(2, await Command(new ScriptedConsole(InstallCommand.AcceptanceWord), api).RunAsync(Options(serverUrl: url), Fixtures.Ct));
        Assert.Empty(api.Enrollments);
    }

    private InstallCommand Command(ScriptedConsole console, RecordingApiClient api, Func<string, string?>? environment = null)
    {
        var install = Path.Combine(_root, "install");
        var paths = new AgentPaths(install, Path.Combine(install, "itmas-agent"), Path.Combine(_root, "data"), Path.Combine(_root, "data"), null);
        return new InstallCommand(console, api, _store, _services, new FakePlatform(), paths, TimeProvider.System, environment ?? (_ => null));
    }

    private static InstallOptions Options(string? serverUrl = "https://itmas.example.test", bool acceptTerms = false, string? codeFile = null) =>
        new(serverUrl, acceptTerms, codeFile);

    private sealed class FailingServiceManager : Itmas3.Agent.Platform.IServiceManager
    {
        public Task InstallAndStartAsync(AgentPaths paths, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("systemctl daemon-reload failed (1)");

        public Task StopAndRemoveAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult("no instalado");
    }
}
