using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Itmas3.Api.Contracts.SqlAudit;

namespace Itmas3.UnitTests.Api.SqlAudit;

/// <summary>
/// SPEC-0005 §12.2: the shared contract example is valid, and cross-field rules (references, identities, securable
/// shape, unavailable sections) are enforced. HTTP-level cases are in the integration tests (CA-15).
/// </summary>
public sealed class SqlAuditSnapshotValidatorTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    [Fact]
    public void The_shared_contract_example_is_valid()
    {
        Assert.Empty(Validate(Fixture()));
    }

    [Theory]
    [InlineData("membership-to-non-role")]
    [InlineData("self-membership")]
    [InlineData("login-ref-to-database-account")]
    [InlineData("owner-ref-unknown")]
    [InlineData("database-level-without-database")]
    [InlineData("unknown-account-type")]
    [InlineData("unknown-object-type")]
    [InlineData("lowercase-permission")]
    [InlineData("unavailable-unknown-database")]
    [InlineData("unavailable-repeated")]
    [InlineData("connection-failed-with-data")]
    [InlineData("duplicate-ref")]
    public void Cross_field_rules_are_enforced(string defect)
    {
        var snapshot = Fixture();
        var accounts = snapshot["accounts"]!.AsArray();
        switch (defect)
        {
            case "membership-to-non-role": snapshot["roleMemberships"]![0]!["roleRef"] = "a2"; break;
            case "self-membership": snapshot["roleMemberships"]![0]!["memberRef"] = "a4"; break;
            case "login-ref-to-database-account": accounts[4]!["loginRef"] = "a6"; break;
            case "owner-ref-unknown": snapshot["databases"]![0]!["ownerRef"] = "zz"; break;
            case "database-level-without-database": accounts[4]!["database"] = null; break;
            case "unknown-account-type": accounts[0]!["type"] = "superuser"; break;
            case "unknown-object-type": snapshot["permissions"]![4]!["securable"]!["objectType"] = "galaxy"; break;
            case "lowercase-permission": snapshot["permissions"]![4]!["permission"] = "select"; break;
            case "unavailable-unknown-database": snapshot["unavailable"]![0]!["section"] = "database:Nada"; break;
            case "unavailable-repeated": snapshot["unavailable"]!.AsArray().Add(snapshot["unavailable"]![0]!.DeepClone()); break;
            case "connection-failed-with-data":
                snapshot["instance"] = null;
                snapshot["unavailable"] = new JsonArray(new JsonObject { ["section"] = "instance", ["reason"] = "timeout" });
                break;
            case "duplicate-ref": accounts[1]!["ref"] = "a1"; break;
        }

        Assert.NotEmpty(Validate(snapshot));
    }

    [Fact]
    public void A_failed_connection_without_data_is_valid()
    {
        var snapshot = Fixture();
        snapshot["instance"] = null;
        snapshot["databases"] = new JsonArray();
        snapshot["accounts"] = new JsonArray();
        snapshot["roleMemberships"] = new JsonArray();
        snapshot["permissions"] = new JsonArray();
        snapshot["unavailable"] = new JsonArray(new JsonObject { ["section"] = "instance", ["reason"] = "certificate-invalid" });

        Assert.Empty(Validate(snapshot));
    }

    [Fact]
    public void Errors_are_capped()
    {
        var snapshot = Fixture();
        foreach (var permission in snapshot["permissions"]!.AsArray())
        {
            permission!["permission"] = "bad";
        }

        var permissions = snapshot["permissions"]!.AsArray();
        for (var i = 0; i < 50; i++)
        {
            permissions.Add(permissions[0]!.DeepClone());
        }

        Assert.InRange(Validate(snapshot).Count, 1, SqlAuditContract.MaxReportedErrors);
    }

    private static List<ValidationResult> Validate(JsonNode snapshot)
    {
        var request = snapshot.Deserialize<SqlAuditSnapshotRequest>(Options)!;
        return [.. request.Validate(new ValidationContext(request))];
    }

    private static JsonNode Fixture()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "agent", "tests", "fixtures", "contract", "sql-audit.v1.json");
            if (File.Exists(path))
            {
                return JsonNode.Parse(File.ReadAllText(path))!;
            }
        }

        throw new InvalidOperationException("Contract fixture not found.");
    }
}
