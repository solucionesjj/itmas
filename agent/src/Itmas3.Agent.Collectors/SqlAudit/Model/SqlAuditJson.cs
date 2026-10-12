using System.Text.Json;
using System.Text.Json.Serialization;

namespace Itmas3.Agent.Collectors.SqlAudit.Model;

/// <summary>
/// Source-generated JSON of the sql-audit v1 contract: camelCase, nulls written (the API requires every property),
/// strict on read (unknown and missing properties fail) so the contract test catches any drift.
/// </summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    WriteIndented = false)]
[JsonSerializable(typeof(SqlAuditSnapshot))]
public sealed partial class SqlAuditJsonContext : JsonSerializerContext;

public static class SqlAuditJson
{
    private static readonly SqlAuditJsonContext Indented = new(new JsonSerializerOptions(SqlAuditJsonContext.Default.Options) { WriteIndented = true });

    public static string Serialize(SqlAuditSnapshot snapshot, bool indented = false) =>
        JsonSerializer.Serialize(snapshot, indented ? Indented.SqlAuditSnapshot : SqlAuditJsonContext.Default.SqlAuditSnapshot);

    public static SqlAuditSnapshot? Deserialize(string json) =>
        JsonSerializer.Deserialize(json, SqlAuditJsonContext.Default.SqlAuditSnapshot);
}
