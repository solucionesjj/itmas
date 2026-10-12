using System.Text.Json;
using System.Text.Json.Nodes;
using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.Collectors.SqlAudit.SqlServer;

namespace Itmas3.Agent.Tests.SqlAudit;

/// <summary>
/// The agent must emit exactly contract sql-audit v1 (SPEC-0005 §12.2), the same document the backend integration tests
/// send (agent/tests/fixtures/contract/sql-audit.v1.json). The API rejects unknown or missing properties.
/// </summary>
public sealed class SqlAuditContractTests
{
    private const string FixturePath = "contract/sql-audit.v1.json";

    [Fact]
    public void Shared_fixture_round_trips_through_the_agent_model_unchanged()
    {
        var fixture = JsonNode.Parse(Fixtures.Read(FixturePath));
        var snapshot = SqlAuditJson.Deserialize(Fixtures.Read(FixturePath));

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.SchemaVersion);
        Assert.Equal("1.2.0", snapshot.AgentVersion);
        Assert.True(JsonNode.DeepEquals(fixture, JsonNode.Parse(SqlAuditJson.Serialize(snapshot))), SqlAuditJson.Serialize(snapshot, indented: true));
    }

    [Fact]
    public void Collected_snapshots_have_exactly_the_properties_of_the_contract()
    {
        var snapshot = SqlFixtures.Snapshot(SqlEngines.SqlServer, SqlServerMapper.Map(SqlFixtures.Rows<SqlServerRows>("sqlserver.rows.json")));

        var expected = PropertyPaths(JsonNode.Parse(Fixtures.Read(FixturePath)));
        var actual = PropertyPaths(JsonNode.Parse(SqlAuditJson.Serialize(snapshot)));

        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Unreadable_instances_serialize_every_property_with_a_null_instance()
    {
        var snapshot = SqlSnapshotAssembler.Unavailable(SqlFixtures.Connection(SqlEngines.PostgreSql), Guid.NewGuid(), SqlFixtures.CollectedAt, "1.2.0", SqlUnavailableReasons.CertificateInvalid);

        var json = JsonNode.Parse(SqlAuditJson.Serialize(snapshot))!.AsObject();

        Assert.Equal(
            ["schemaVersion", "submissionId", "collectedAt", "agentVersion", "connection", "instance", "databases", "accounts", "roleMemberships", "permissions", "omitted", "unavailable"],
            json.Select(pair => pair.Key));
        Assert.Null(json["instance"]);
        Assert.Equal("certificate-invalid", json["unavailable"]![0]!["reason"]!.GetValue<string>());
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", json["submissionId"]!.GetValue<string>());
    }

    [Fact]
    public void Unknown_or_missing_properties_are_rejected_by_the_model_too()
    {
        var unknown = JsonNode.Parse(Fixtures.Read(FixturePath))!.AsObject();
        unknown["extra"] = 1;
        Assert.Throws<JsonException>(() => SqlAuditJson.Deserialize(unknown.ToJsonString()));

        var missing = JsonNode.Parse(Fixtures.Read(FixturePath))!.AsObject();
        missing.Remove("omitted");
        Assert.Throws<JsonException>(() => SqlAuditJson.Deserialize(missing.ToJsonString()));
    }

    [Fact]
    public void Snapshots_never_contain_password_material()
    {
        var json = SqlAuditJson.Serialize(SqlFixtures.Snapshot(SqlEngines.SqlServer, SqlServerMapper.Map(SqlFixtures.Rows<SqlServerRows>("sqlserver.rows.json"))));

        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authentication_string", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"sid\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AA01", json, StringComparison.Ordinal); // SIDs only join users with logins
    }

    /// <summary>Set of JSON property paths; array elements are merged ("[]").</summary>
    private static HashSet<string> PropertyPaths(JsonNode? node, string prefix = "")
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj)
                {
                    var path = prefix.Length == 0 ? name : $"{prefix}.{name}";
                    paths.Add(path);
                    paths.UnionWith(PropertyPaths(value, path));
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    paths.UnionWith(PropertyPaths(item, prefix + "[]"));
                }

                break;
        }

        return paths;
    }
}
