namespace Itmas3.Agent.Collectors.Common;

/// <summary>Normalises text taken from the system or the network to what the contract accepts (§12.2).</summary>
public static class TextSanitizer
{
    public const int MaxLength = 256;

    /// <summary>Removes control characters, trims and truncates. Returns null for empty values.</summary>
    public static string? Clean(string? value, int maxLength = MaxLength)
    {
        if (value is null)
        {
            return null;
        }

        var cleaned = new string(value.Where(character => !char.IsControl(character)).ToArray()).Trim();
        if (cleaned.Length == 0)
        {
            return null;
        }

        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength].TrimEnd();
    }

    /// <summary>Cleaned, distinct (case-insensitive) and sorted values, at most <paramref name="maxCount"/>.</summary>
    public static IReadOnlyList<string> DistinctSorted(IEnumerable<string?> values, int maxCount)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values
            .Select(value => Clean(value))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToList();
    }
}
