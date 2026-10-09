using System.Text.Json;
using System.Text.Json.Serialization;

namespace Itmas3.Agent.Collectors.Model;

/// <summary>Source-generated JSON of the contract: camelCase, nulls written (the API requires every property).</summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = false)]
[JsonSerializable(typeof(InventoryReport))]
public sealed partial class InventoryJsonContext : JsonSerializerContext;

public static class InventoryJson
{
    private static readonly InventoryJsonContext Indented = new(new JsonSerializerOptions(InventoryJsonContext.Default.Options) { WriteIndented = true });

    public static string Serialize(InventoryReport report, bool indented = false) =>
        JsonSerializer.Serialize(report, indented ? Indented.InventoryReport : InventoryJsonContext.Default.InventoryReport);

    public static InventoryReport? Deserialize(string json) =>
        JsonSerializer.Deserialize(json, InventoryJsonContext.Default.InventoryReport);
}
