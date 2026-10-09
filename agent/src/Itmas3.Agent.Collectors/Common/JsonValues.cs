using System.Globalization;
using System.Text.Json;

namespace Itmas3.Agent.Collectors.Common;

/// <summary>Tolerant readers for tool JSON whose value types vary across versions (e.g. lsblk "1" vs true).</summary>
internal static class JsonValues
{
    public static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public static long? Long(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
            _ => null,
        };
    }

    public static bool? Bool(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => value.GetString() switch { "1" or "true" => true, "0" or "false" => false, _ => null },
            JsonValueKind.Number => value.GetInt32() != 0,
            _ => null,
        };
    }
}
