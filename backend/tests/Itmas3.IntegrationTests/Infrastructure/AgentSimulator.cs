using System.Text;
using System.Text.Json.Nodes;

namespace Itmas3.IntegrationTests.Infrastructure;

/// <summary>HTTP calls of the inventory agent (SPEC-0002), as the real agent makes them.</summary>
public static class AgentSimulator
{
    public static readonly Uri Enrollments = new("/api/v1/device-enrollments", UriKind.Relative);
    public static readonly Uri Reports = new("/api/v1/inventory-reports", UriKind.Relative);
    public static readonly Uri SqlSnapshots = new("/api/v1/sql-audit-snapshots", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static Task<HttpResponseMessage> EnrollRawAsync(HttpClient client, string code, string hardwareId, string hostname = "srv-app-01") =>
        SendAsync(client, HttpMethod.Post, Enrollments, $"Enrollment {code}", EnrollmentBody(hardwareId, hostname).ToJsonString());

    public static Task<HttpResponseMessage> SubmitAsync(HttpClient client, string apiKey, JsonNode report) =>
        SendAsync(client, HttpMethod.Post, Reports, $"Device {apiKey}", report.ToJsonString());

    public static Task<HttpResponseMessage> SubmitSqlSnapshotAsync(HttpClient client, string apiKey, JsonNode snapshot) =>
        SendAsync(client, HttpMethod.Post, SqlSnapshots, $"Device {apiKey}", snapshot.ToJsonString());

    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, Uri uri, string authorization, string json)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = Json(json) };
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.Add("X-CSRF", "1");
        return await client.SendAsync(request, Ct);
    }

    public static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    public static JsonObject EnrollmentBody(string hardwareId, string hostname = "srv-app-01") => new()
    {
        ["hardwareId"] = hardwareId,
        ["hostname"] = hostname,
        ["osFamily"] = "linux",
        ["agentVersion"] = "1.0.0",
        ["termsVersion"] = "1",
        ["termsAcceptedAt"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"),
    };

    public static string NewHardwareId() => $"hw-{Guid.NewGuid():N}";

    /// <summary>Contract v1 example shared with the agent tests (agent/tests/fixtures/contract).</summary>
    public static JsonNode ContractFixture()
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "agent", "tests", "fixtures", "contract", "inventory-report.v1.json")))!;
        node["collectedAt"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");
        return node;
    }

    /// <summary>
    /// SQL audit contract v1 example shared with the agent tests (SPEC-0005 §12.2), with a new submission id and a
    /// recent collection date so each call is a new snapshot.
    /// </summary>
    public static JsonNode SqlAuditFixture(DateTimeOffset? collectedAt = null)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "agent", "tests", "fixtures", "contract", "sql-audit.v1.json")))!;
        node["submissionId"] = Guid.NewGuid().ToString();
        node["collectedAt"] = (collectedAt ?? DateTimeOffset.UtcNow.AddMinutes(-1)).ToString("O");
        return node;
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "constitution.md")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
