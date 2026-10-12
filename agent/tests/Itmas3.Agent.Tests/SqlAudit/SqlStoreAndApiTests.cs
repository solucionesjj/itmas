using System.Net;
using System.Text.Json.Nodes;
using Itmas3.Agent.Api;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.Configuration;
using Itmas3.Agent.Service;
using Itmas3.Agent.SqlAudit;

namespace Itmas3.Agent.Tests.SqlAudit;

/// <summary>Connection store protection (RF-2, ADR-0014 §4), snapshot submission (§8.2) and terms v2 (CA-19).</summary>
public sealed class SqlStoreAndApiTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("itmas-agent-sql-store").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Passwords_round_trip_exactly_and_are_removed_with_their_connection()
    {
        var store = Store();
        var connection = Connection("uno");
        store.Save(connection, "  espacios y ;'\" placeholder  ");
        store.Save(Connection("dos"), "otra-placeholder");

        Assert.Equal("  espacios y ;'\" placeholder  ", store.LoadPassword(connection));
        Assert.Equal(["dos", "uno"], store.List().Select(saved => saved.Alias));
        Assert.Null(store.Find("tres"));
        Assert.NotNull(store.Find("UNO"));

        Assert.True(store.Remove("uno"));
        Assert.Null(store.LoadPassword(connection));
        Assert.False(store.Remove("uno"));
    }

    [Fact]
    public void Store_files_are_readable_only_by_their_owner()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // DPAPI and the ACL of the data directory protect them (SecureFiles).
        }

        var store = Store();
        var connection = Connection("uno");
        store.Save(connection, "placeholder");
        store.SaveResult(connection.Id, new SqlConnectionResult(LastOutcome: SqlOutcomes.Complete));

        foreach (var file in Directory.EnumerateFiles(Path.Combine(_root, "sql"), "*", SearchOption.AllDirectories))
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }

        foreach (var directory in Directory.EnumerateDirectories(Path.Combine(_root, "sql"), "*", SearchOption.AllDirectories).Append(Path.Combine(_root, "sql")))
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(directory));
        }
    }

    [Fact]
    public void Results_of_removed_connections_are_dropped_and_a_corrupt_file_means_never_collected()
    {
        var store = Store();
        var kept = Connection("uno");
        var removed = Connection("dos");
        store.Save(kept, "placeholder");
        store.Save(removed, "placeholder");
        store.SaveResult(removed.Id, new SqlConnectionResult(LastOutcome: SqlOutcomes.Failed));
        store.Remove("dos");
        store.SaveResult(kept.Id, new SqlConnectionResult(LastOutcome: SqlOutcomes.Complete));

        Assert.Equal([kept.Id], store.LoadResults().Keys);

        File.WriteAllText(Path.Combine(_root, "sql", "results.json"), "{ corrupt");
        Assert.Empty(store.LoadResults());
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted, SubmitOutcome.Accepted)]
    [InlineData(HttpStatusCode.Unauthorized, SubmitOutcome.CredentialRejected)]
    [InlineData(HttpStatusCode.TooManyRequests, SubmitOutcome.TransientFailure)]
    [InlineData(HttpStatusCode.ServiceUnavailable, SubmitOutcome.TransientFailure)]
    [InlineData(HttpStatusCode.BadRequest, SubmitOutcome.Failed)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, SubmitOutcome.Failed)]
    public async Task Snapshots_are_posted_with_the_device_key_and_the_csrf_header(HttpStatusCode status, SubmitOutcome expected)
    {
        var handler = StubHandler.Returning(status);
        using var http = new HttpClient(handler);
        var snapshot = SqlAuditJson.Deserialize(Fixtures.Read("contract/sql-audit.v1.json"))!;

        var (outcome, _) = await new ItmasApiClient(http).SubmitSqlAuditAsync(new Uri("https://itmas.example.test/"), "device-key-placeholder", snapshot, Fixtures.Ct);

        Assert.Equal(expected, outcome);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://itmas.example.test/api/v1/sql-audit-snapshots"), request.RequestUri);
        Assert.Equal(("Device", "device-key-placeholder"), (request.Headers.Authorization!.Scheme, request.Headers.Authorization.Parameter));
        Assert.Equal("1", Assert.Single(request.Headers.GetValues("X-CSRF")));
        Assert.Equal("6f1c2b9e-4d3a-4f6b-9a8e-2c1d0e5f7a90", JsonNode.Parse(handler.Bodies[0])!["submissionId"]!.GetValue<string>());
    }

    [Fact]
    public void CA19_new_installations_show_and_send_terms_version_2()
    {
        var terms = AgentRuntime.ReadResource($"terms-v{AgentRuntime.TermsVersion}.es.txt");

        Assert.Equal("2", AgentRuntime.TermsVersion);
        Assert.StartsWith("TÉRMINOS Y CONDICIONES DE USO DEL AGENTE ITMas (versión 2)", terms, StringComparison.Ordinal);
        Assert.Contains("Auditoría de motores de bases de datos", terms, StringComparison.Ordinal);
        Assert.Contains("itmas-agent uninstall", terms, StringComparison.Ordinal);
    }

    private SqlConnectionStore Store() => new(new AgentPaths(_root, Path.Combine(_root, "itmas-agent"), _root, _root, null));

    private static SqlConnectionDefinition Connection(string alias) =>
        new(Guid.NewGuid(), alias, SqlEngines.PostgreSql, "db.example.test", 5432, null, "postgres", "itmas_agent", SqlTlsModes.Verify, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
}
