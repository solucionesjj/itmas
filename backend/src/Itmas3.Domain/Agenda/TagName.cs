using System.Text;
using Itmas3.Domain.Common;

namespace Itmas3.Domain.Agenda;

/// <summary>
/// Normalization of event tags (SPEC-0006 RN-15). Tags are free text: trimmed, with repeated spaces collapsed into one,
/// 1 to 30 characters and no control characters. Two tags that differ only in case are the same tag (<see cref="Key"/>);
/// the spelling of the first use is kept.
/// </summary>
public static class TagName
{
    public const int MaxLength = 30;
    public const int MaxPerEvent = 10;

    /// <summary>Normalized tag name (trimmed, single spaces). Throws when it is empty, too long or has control characters.</summary>
    public static string Normalize(string? raw)
    {
        var builder = new StringBuilder(raw?.Length ?? 0);
        var pendingSpace = false;
        foreach (var c in raw ?? string.Empty)
        {
            if (char.IsControl(c))
            {
                throw new DomainException("Las etiquetas no pueden contener caracteres de control.");
            }

            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        if (builder.Length == 0)
        {
            throw new DomainException("Una etiqueta no puede estar vacía.");
        }

        return builder.Length <= MaxLength
            ? builder.ToString()
            : throw new DomainException($"Una etiqueta admite como máximo {MaxLength} caracteres.");
    }

    /// <summary>
    /// Case-insensitive identity of a tag (the database enforces it with <c>lower(name)</c>). The invariant culture leaves the capital
    /// dotted I (U+0130) as it is while PostgreSQL lowers it to <c>i</c>: it is mapped here so that two spellings the database sees as
    /// the same tag are also the same key, instead of colliding on the unique index.
    /// </summary>
    public static string Key(string name) => name.ToLowerInvariant().Replace('\u0130', 'i');

    /// <summary>
    /// Normalizes the tags of an event: each one is normalized, repeated tags (ignoring case) are merged keeping the first
    /// spelling, and at most <see cref="MaxPerEvent"/> remain (CA-25).
    /// </summary>
    public static IReadOnlyList<string> NormalizeSet(IEnumerable<string>? raw)
    {
        var result = new List<string>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in raw ?? [])
        {
            var name = Normalize(item);
            if (keys.Add(Key(name)))
            {
                result.Add(name);
            }
        }

        return result.Count <= MaxPerEvent
            ? result
            : throw new DomainException($"Máximo {MaxPerEvent} etiquetas por actividad.");
    }
}
