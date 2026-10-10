using System.Globalization;

namespace Itmas3.Api.Controllers;

/// <summary>Strong ETags carrying an integer revision, for optimistic concurrency with <c>If-Match</c>.</summary>
internal static class EntityTags
{
    public static string Format(int revision) => $"\"{revision.ToString(CultureInfo.InvariantCulture)}\"";

    public static bool TryParse(string? value, out int revision)
    {
        revision = 0;
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        if (trimmed.StartsWith("W/", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..];
        }

        return int.TryParse(trimmed.Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out revision);
    }
}
