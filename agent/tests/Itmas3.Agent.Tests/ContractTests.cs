using System.Text.Json;
using System.Text.Json.Nodes;
using Itmas3.Agent.Collectors;
using Itmas3.Agent.Collectors.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace Itmas3.Agent.Tests;

/// <summary>
/// The agent must emit exactly contract v1 (SPEC-0002 §12.2), the same document the backend integration tests send
/// (agent/tests/fixtures/contract/inventory-report.v1.json). The API rejects unknown or missing properties.
/// </summary>
public sealed class ContractTests
{
    [Fact]
    public void Shared_fixture_maps_to_the_agent_model_without_unknown_properties()
    {
        var report = InventoryJson.Deserialize(Fixtures.Read("contract/inventory-report.v1.json"));

        Assert.NotNull(report);
        Assert.Equal(1, report.SchemaVersion);
        Assert.Equal("srv-app-01", report.System.Hostname);
        Assert.Equal(EncryptionStatus.Encrypted, report.Hardware.Volumes![0].Encryption.Status);
    }

    [Fact]
    public async Task Collected_reports_have_exactly_the_properties_of_the_contract()
    {
        var report = await Collector(new FakePlatform()).CollectAsync("1.0.0", Fixtures.Ct);

        var expected = PropertyPaths(JsonNode.Parse(Fixtures.Read("contract/inventory-report.v1.json")));
        var actual = PropertyPaths(JsonNode.Parse(InventoryJson.Serialize(report)));

        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Unavailable_sections_are_serialized_as_null_and_declared()
    {
        var platform = new FakePlatform { FirewallFailure = new UnauthorizedAccessException() };
        var report = await Collector(platform).CollectAsync("1.0.0", Fixtures.Ct);

        var json = JsonNode.Parse(InventoryJson.Serialize(report))!;
        Assert.Null(json["security"]!["firewall"]);
        Assert.True(json["security"]!.AsObject().ContainsKey("firewall"));
        var unavailable = Assert.Single(json["unavailable"]!.AsArray());
        Assert.Equal("security.firewall", unavailable!["section"]!.GetValue<string>());
        Assert.Equal("permission-denied", unavailable["reason"]!.GetValue<string>());
    }

    internal static InventoryCollector Collector(FakePlatform platform, Exception? publicNetworkFailure = null) =>
        new(platform, new FakeInterfaces(), new FakePublicNetwork(publicNetworkFailure), TimeProvider.System, NullLogger<InventoryCollector>.Instance);

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
