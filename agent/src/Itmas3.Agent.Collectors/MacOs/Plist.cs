using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Itmas3.Agent.Collectors.MacOs;

/// <summary>
/// Minimal reader of XML property lists (<c>diskutil … -plist</c>). DTD processing is disabled (no external entities).
/// Values: dictionary, list, string, long, double, bool or null.
/// </summary>
public static class Plist
{
    public static object? Parse(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(xml), settings);
        var root = XDocument.Load(reader).Root?.Elements().FirstOrDefault();
        return root is null ? null : Value(root);
    }

    public static IReadOnlyDictionary<string, object?> ParseDictionary(string xml) =>
        Parse(xml) as IReadOnlyDictionary<string, object?> ?? throw new CollectorException(Model.UnavailableReasons.Error, "Property list is not a dictionary.");

    public static string? String(this IReadOnlyDictionary<string, object?> dictionary, string key) =>
        dictionary.TryGetValue(key, out var value) ? value as string : null;

    public static long? Long(this IReadOnlyDictionary<string, object?> dictionary, string key) =>
        dictionary.TryGetValue(key, out var value) && value is long number ? number : null;

    public static bool? Bool(this IReadOnlyDictionary<string, object?> dictionary, string key) =>
        dictionary.TryGetValue(key, out var value) && value is bool flag ? flag : null;

    public static IReadOnlyList<object?> List(this IReadOnlyDictionary<string, object?> dictionary, string key) =>
        dictionary.TryGetValue(key, out var value) && value is IReadOnlyList<object?> list ? list : [];

    private static object? Value(XElement element) => element.Name.LocalName switch
    {
        "dict" => Dictionary(element),
        "array" => element.Elements().Select(Value).ToList(),
        "string" or "date" or "data" => element.Value,
        "integer" => long.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null,
        "real" => double.TryParse(element.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) ? real : null,
        "true" => true,
        "false" => false,
        _ => null,
    };

    private static Dictionary<string, object?> Dictionary(XElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        string? key = null;
        foreach (var child in element.Elements())
        {
            if (child.Name.LocalName == "key")
            {
                key = child.Value;
            }
            else if (key is not null)
            {
                result[key] = Value(child);
                key = null;
            }
        }

        return result;
    }
}
