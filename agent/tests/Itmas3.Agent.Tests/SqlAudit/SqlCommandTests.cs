using System.Security.Cryptography.X509Certificates;
using Itmas3.Agent.Cli;
using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.Configuration;
using Itmas3.Agent.SqlAudit;

namespace Itmas3.Agent.Tests.SqlAudit;

/// <summary>
/// <c>itmas-agent sql …</c> (SPEC-0005 RF-2, RF-3; CA-3, CA-4, CA-19): the password is never an argument, never shown and
/// never stored outside its protected file; nothing is saved when the test fails; unverified needs the express
/// confirmation and is never chosen by the agent.
/// </summary>
public sealed class SqlCommandTests : IDisposable
{
    private const string Secret = "s3cr3t placeholder-not-a-secret";
    private const string PasswordVariable = "ITMAS_TEST_SQL_PASSWORD";

    private readonly string _root = Directory.CreateTempSubdirectory("itmas-agent-sql").FullName;
    private readonly FakeSqlEngine _engine = new();
    private readonly SqlConnectionStore _store;
    private X509Certificate2? _presented;

    public SqlCommandTests()
    {
        _store = new SqlConnectionStore(new AgentPaths(Path.Combine(_root, "install"), Path.Combine(_root, "install", "itmas-agent"), Path.Combine(_root, "data"), Path.Combine(_root, "data"), null));
    }

    private string SqlDirectory => Path.Combine(_root, "data", "sql");

    public void Dispose()
    {
        _presented?.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData("add", "--alias", "x", "--password", Secret)]
    [InlineData("add", "--password=" + Secret)]
    [InlineData("add", "-p", Secret)]
    [InlineData("add", "--passwd", Secret)]
    [InlineData("update", "ventas", "--pwd", Secret)]
    public async Task CA03_the_password_is_never_accepted_as_an_argument(params string[] arguments)
    {
        var console = new ScriptedConsole { IsInteractive = false };

        Assert.Equal(2, await Command(console).RunAsync(arguments, Fixtures.Ct));

        Assert.Empty(_engine.Probes);
        Assert.Empty(_store.List());
        Assert.Contains(console.Output, line => line.Contains("no se acepta como argumento", StringComparison.Ordinal));
        Assert.DoesNotContain(console.Output, line => line.Contains(Secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Free_form_connection_strings_do_not_exist()
    {
        var console = new ScriptedConsole { IsInteractive = false };

        Assert.Equal(2, await Command(console).RunAsync(["add", "--connection-string", "Server=x;Encrypt=False"], Fixtures.Ct));
        Assert.Contains(console.Output, line => line.Contains("No se admiten cadenas de conexión", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CA03_non_interactive_add_tests_then_stores_the_password_protected_and_nowhere_else()
    {
        var console = new ScriptedConsole { IsInteractive = false };

        var exit = await Command(console).RunAsync(Add("ventas-prod", SqlEngines.SqlServer, "--password-env", PasswordVariable), Fixtures.Ct);

        Assert.Equal(0, exit);
        var (settings, password) = Assert.Single(_engine.Probes);
        Assert.Equal((SqlTlsModes.Verify, Secret, 1433), (settings.TlsMode, password, settings.Port));
        var saved = Assert.Single(_store.List());
        Assert.Equal(("ventas-prod", SqlEngines.SqlServer, "sql01.example.test", "itmas_agent"), (saved.Alias, saved.Engine, saved.Host, saved.User));
        Assert.Equal(Secret, _store.LoadPassword(saved));
        Assert.DoesNotContain(Secret, await File.ReadAllTextAsync(Path.Combine(SqlDirectory, "connections.json"), Fixtures.Ct), StringComparison.Ordinal);
        Assert.DoesNotContain(console.Output, line => line.Contains(Secret, StringComparison.Ordinal));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(SqlDirectory, "credentials", saved.Id.ToString("N"))));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Path.Combine(SqlDirectory, "credentials")));
        }

        var list = new ScriptedConsole { IsInteractive = false };
        Assert.Equal(0, await Command(list).RunAsync(["list"], Fixtures.Ct));
        Assert.Contains(list.Output, line => line.Contains("ventas-prod", StringComparison.Ordinal) && line.Contains("pendiente", StringComparison.Ordinal));
        Assert.DoesNotContain(list.Output, line => line.Contains(Secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_password_file_is_read_exactly_except_the_final_newline()
    {
        var file = Path.Combine(_root, "password.txt");
        await File.WriteAllTextAsync(file, " con espacios placeholder \n", Fixtures.Ct);

        Assert.Equal(0, await Command(new ScriptedConsole { IsInteractive = false }).RunAsync(Add("pg", SqlEngines.PostgreSql, "--password-file", file), Fixtures.Ct));

        Assert.Equal(" con espacios placeholder ", _store.LoadPassword(Assert.Single(_store.List())));
        Assert.Equal(SqlConnectionSettings.DefaultPostgreSqlDatabase, _engine.Probes[0].Settings.Database);
    }

    [Fact]
    public async Task CA03_interactive_add_asks_each_value_and_reads_the_password_hidden()
    {
        var console = new ScriptedConsole("pg-prod", "postgresql", "db.example.test", "", "inventario", "itmas_agent", Secret, "");

        Assert.Equal(0, await Command(console).RunAsync(["add"], Fixtures.Ct));

        Assert.Equal(1, console.SecretReads);
        var saved = Assert.Single(_store.List());
        Assert.Equal((5432, "inventario", SqlTlsModes.Verify), (saved.Port, saved.Database, saved.Tls));
        Assert.DoesNotContain(console.Output, line => line.Contains(Secret, StringComparison.Ordinal));
        Assert.DoesNotContain(console.Output, line => line.Contains("TÉRMINOS", StringComparison.Ordinal)); // CA-19: no terms here
    }

    [Theory]
    [InlineData(SqlFailureKind.Certificate, "certificado TLS del motor no es válido")]
    [InlineData(SqlFailureKind.TlsNotSupported, "no acepta conexiones cifradas")]
    [InlineData(SqlFailureKind.Authentication, "rechazó el usuario o la contraseña")]
    [InlineData(SqlFailureKind.Permission, "permisos mínimos")]
    [InlineData(SqlFailureKind.Dns, "resolver el nombre")]
    public async Task CA03_a_failed_test_explains_the_cause_and_saves_nothing(SqlFailureKind kind, string explanation)
    {
        _engine.ProbeFailure = new SqlAuditException(kind, "fixed detail");
        var console = new ScriptedConsole { IsInteractive = false };

        Assert.Equal(4, await Command(console).RunAsync(Add("ventas", SqlEngines.MySql, "--password-env", PasswordVariable), Fixtures.Ct));

        Assert.Contains(console.Output, line => line.Contains(explanation, StringComparison.Ordinal));
        Assert.Contains(console.Output, line => line.Contains("No se guardó ningún cambio", StringComparison.Ordinal));
        Assert.Empty(_store.List());
        Assert.False(Directory.Exists(Path.Combine(SqlDirectory, "credentials")) && Directory.EnumerateFiles(Path.Combine(SqlDirectory, "credentials")).Any());
    }

    [Fact]
    public async Task CA04_a_failed_verify_is_never_retried_in_a_weaker_mode()
    {
        _engine.ProbeFailure = new SqlAuditException(SqlFailureKind.Certificate, "fixed detail");
        var console = new ScriptedConsole { IsInteractive = false };

        await Command(console).RunAsync(Add("ventas", SqlEngines.PostgreSql, "--password-env", PasswordVariable), Fixtures.Ct);

        Assert.Equal([SqlTlsModes.Verify], _engine.Probes.Select(probe => probe.Settings.TlsMode));
        Assert.Contains(console.Output, line => line.Contains("no cambia de modo TLS por su cuenta", StringComparison.Ordinal) && line.Contains("--tls pinned", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("acepto el riesgo")]
    [InlineData("ACEPTO")]
    [InlineData("")]
    public async Task CA04_unverified_without_the_exact_confirmation_cancels_without_changes(string answer)
    {
        var console = new ScriptedConsole("mysql-legacy", "mysql", "db.example.test", "", "itmas_agent", Secret, "unverified", answer);

        Assert.Equal(3, await Command(console).RunAsync(["add"], Fixtures.Ct));

        Assert.Empty(_engine.Probes);
        Assert.Empty(_store.List());
        Assert.Contains(console.Output, line => line.Contains("pinned", StringComparison.Ordinal) && line.Contains("certificate-file", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CA04_unverified_with_the_exact_confirmation_is_saved_and_flagged()
    {
        var console = new ScriptedConsole("mysql-legacy", "mysql", "db.example.test", "", "itmas_agent", Secret, "unverified", SqlCommand.RiskConfirmation);

        Assert.Equal(0, await Command(console).RunAsync(["add"], Fixtures.Ct));

        Assert.Equal(SqlTlsModes.Unverified, Assert.Single(_store.List()).Tls);
        Assert.Equal(SqlTlsModes.Unverified, Assert.Single(_engine.Probes).Settings.TlsMode);
        Assert.Contains(console.Output, line => line.Contains("suplantar el motor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CA04_without_a_console_unverified_requires_the_explicit_option()
    {
        var console = new ScriptedConsole { IsInteractive = false };

        Assert.Equal(0, await Command(console).RunAsync(Add("legacy", SqlEngines.MariaDb, "--password-env", PasswordVariable, "--tls", "unverified"), Fixtures.Ct));

        Assert.Equal(SqlTlsModes.Unverified, Assert.Single(_store.List()).Tls);
        Assert.Contains(console.Output, line => line.Contains("no autenticará al servidor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CA04_pinned_with_a_certificate_file_pins_its_sha256()
    {
        using var certificate = SqlTlsTests.SelfSigned();
        var file = Path.Combine(_root, "server.pem");
        await File.WriteAllTextAsync(file, certificate.ExportCertificatePem(), Fixtures.Ct);

        var exit = await Command(new ScriptedConsole { IsInteractive = false })
            .RunAsync(Add("pg", SqlEngines.PostgreSql, "--password-env", PasswordVariable, "--tls", "pinned", "--certificate-file", file), Fixtures.Ct);

        Assert.Equal(0, exit);
        var saved = Assert.Single(_store.List());
        Assert.Equal((SqlTlsModes.Pinned, SqlTls.Thumbprint(certificate)), (saved.Tls, saved.PinnedSha256));
        Assert.Equal(SqlTls.Thumbprint(certificate), _engine.Probes[0].Settings.PinnedSha256);
        Assert.False(File.Exists(_store.CertificatePath(saved.Id))); // only SQL Server needs the file
    }

    [Fact]
    public async Task CA04_sql_server_pinned_by_fingerprint_stores_the_certificate_the_server_presents()
    {
        _presented = SqlTlsTests.SelfSigned();
        _engine.OnProbe = settings => Assert.True(File.Exists(settings.PinnedCertificatePath));

        var exit = await Command(new ScriptedConsole { IsInteractive = false })
            .RunAsync(Add("mssql", SqlEngines.SqlServer, "--password-env", PasswordVariable, "--tls", "pinned", "--certificate-sha256", SqlTls.Thumbprint(_presented).ToLowerInvariant()), Fixtures.Ct);

        Assert.Equal(0, exit);
        var saved = Assert.Single(_store.List());
        Assert.Equal(_presented.RawData, await File.ReadAllBytesAsync(_store.CertificatePath(saved.Id), Fixtures.Ct));
        Assert.False(File.Exists(_store.PendingCertificatePath(saved.Id)));
    }

    [Fact]
    public async Task CA04_a_fingerprint_that_does_not_match_the_server_saves_nothing()
    {
        _presented = SqlTlsTests.SelfSigned();
        var console = new ScriptedConsole { IsInteractive = false };

        var exit = await Command(console).RunAsync(Add("mssql", SqlEngines.SqlServer, "--password-env", PasswordVariable, "--tls", "pinned", "--certificate-sha256", new string('A', 64)), Fixtures.Ct);

        Assert.Equal(2, exit);
        Assert.Empty(_engine.Probes);
        Assert.Empty(_store.List());
        Assert.Contains(console.Output, line => line.Contains("No coincide con la huella", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(SqlDirectory, "certificates")) && Directory.EnumerateFiles(Path.Combine(SqlDirectory, "certificates")).Any());
    }

    [Fact]
    public async Task A_failed_test_of_a_pinned_sql_server_discards_the_staged_certificate()
    {
        using var certificate = SqlTlsTests.SelfSigned();
        var file = Path.Combine(_root, "server.cer");
        await File.WriteAllBytesAsync(file, certificate.RawData, Fixtures.Ct);
        _engine.ProbeFailure = new SqlAuditException(SqlFailureKind.Certificate, "fixed detail");

        Assert.Equal(4, await Command(new ScriptedConsole { IsInteractive = false })
            .RunAsync(Add("mssql", SqlEngines.SqlServer, "--password-env", PasswordVariable, "--tls", "pinned", "--certificate-file", file), Fixtures.Ct));

        Assert.Empty(Directory.EnumerateFiles(Path.Combine(SqlDirectory, "certificates")));
    }

    [Fact]
    public async Task Update_retests_with_the_stored_password_and_keeps_the_old_definition_on_failure()
    {
        await Command(new ScriptedConsole { IsInteractive = false }).RunAsync(Add("ventas", SqlEngines.MySql, "--password-env", PasswordVariable), Fixtures.Ct);
        var original = Assert.Single(_store.List());

        Assert.Equal(0, await Command(new ScriptedConsole { IsInteractive = false }).RunAsync(["update", "ventas", "--port", "3307"], Fixtures.Ct));
        Assert.Equal((3307, Secret), (_engine.Probes[^1].Settings.Port, _engine.Probes[^1].Password));
        var updated = Assert.Single(_store.List());
        Assert.Equal((original.Id, original.CreatedAt, 3307), (updated.Id, updated.CreatedAt, updated.Port));
        Assert.Equal(Secret, _store.LoadPassword(updated));

        _engine.ProbeFailure = new SqlAuditException(SqlFailureKind.Connection, "fixed detail");
        Assert.Equal(4, await Command(new ScriptedConsole { IsInteractive = false }).RunAsync(["update", "ventas", "--port", "3308"], Fixtures.Ct));
        Assert.Equal(3307, Assert.Single(_store.List()).Port);
    }

    [Fact]
    public async Task Update_rotates_the_password_from_the_hidden_prompt()
    {
        await Command(new ScriptedConsole { IsInteractive = false }).RunAsync(Add("ventas", SqlEngines.MySql, "--password-env", PasswordVariable), Fixtures.Ct);
        var console = new ScriptedConsole("", "", "", "nueva-placeholder", "");

        Assert.Equal(0, await Command(console).RunAsync(["update", "ventas"], Fixtures.Ct));

        Assert.Equal("nueva-placeholder", _store.LoadPassword(Assert.Single(_store.List())));
        Assert.Equal(1, console.SecretReads);
    }

    [Fact]
    public async Task Test_warns_about_excess_privileges_and_remove_deletes_the_credential()
    {
        await Command(new ScriptedConsole { IsInteractive = false }).RunAsync(Add("ventas", SqlEngines.SqlServer, "--password-env", PasswordVariable), Fixtures.Ct);
        var saved = Assert.Single(_store.List());
        _engine.ExcessPrivileges = ["permiso de servidor CONTROL SERVER"];
        var console = new ScriptedConsole { IsInteractive = false };

        Assert.Equal(0, await Command(console).RunAsync(["test", "ventas"], Fixtures.Ct));
        Assert.Contains(console.Output, line => line.Contains("más privilegios de los necesarios", StringComparison.Ordinal));
        Assert.Contains(console.Output, line => line.Contains("CONTROL SERVER", StringComparison.Ordinal));

        Assert.Equal(0, await Command(new ScriptedConsole { IsInteractive = false }).RunAsync(["remove", "ventas"], Fixtures.Ct));
        Assert.Empty(_store.List());
        Assert.Null(_store.LoadPassword(saved));
        Assert.Equal(2, await Command(new ScriptedConsole { IsInteractive = false }).RunAsync(["test", "ventas"], Fixtures.Ct));
    }

    [Fact]
    public async Task Aliases_are_unique_and_at_most_fifty_connections_are_allowed()
    {
        for (var index = 0; index < SqlConnectionSettings.MaxConnections; index++)
        {
            Assert.Equal(0, await Command(new ScriptedConsole { IsInteractive = false }).RunAsync(Add($"c{index}", SqlEngines.MySql, "--password-env", PasswordVariable), Fixtures.Ct));
        }

        var duplicate = new ScriptedConsole { IsInteractive = false };
        _store.Remove("c0");
        await Command(new ScriptedConsole { IsInteractive = false }).RunAsync(Add("c0", SqlEngines.MySql, "--password-env", PasswordVariable), Fixtures.Ct);
        Assert.Equal(2, await Command(duplicate).RunAsync(Add("C0", SqlEngines.MySql, "--password-env", PasswordVariable), Fixtures.Ct));
        Assert.Contains(duplicate.Output, line => line.Contains("máximo", StringComparison.Ordinal) || line.Contains("Ya existe", StringComparison.Ordinal));
        Assert.Equal(SqlConnectionSettings.MaxConnections, _store.List().Count);
    }

    [Theory]
    [InlineData("--alias", "-mal")]
    [InlineData("--host", "host;Encrypt=False")]
    [InlineData("--port", "99999")]
    [InlineData("--engine", "oracle")]
    [InlineData("--tls", "plain")]
    public async Task Invalid_values_are_rejected_before_connecting(string option, string value)
    {
        var arguments = Add("valido", SqlEngines.MySql, "--password-env", PasswordVariable).ToList();
        var index = arguments.IndexOf(option);
        if (index >= 0)
        {
            arguments[index + 1] = value;
        }
        else
        {
            arguments.AddRange([option, value]);
        }

        Assert.Equal(2, await Command(new ScriptedConsole { IsInteractive = false }).RunAsync(arguments, Fixtures.Ct));
        Assert.Empty(_engine.Probes);
    }

    private static string[] Add(string alias, string engine, params string[] extra) =>
        ["add", "--alias", alias, "--engine", engine, "--host", "sql01.example.test", "--user", "itmas_agent", .. extra];

    private SqlCommand Command(ScriptedConsole console) => new(
        console,
        _store,
        _ => _engine,
        (_, _, _, _) => Task.FromResult(X509CertificateLoader.LoadCertificate(_presented!.RawData)),
        name => name == PasswordVariable ? Secret : null,
        TimeProvider.System);
}
