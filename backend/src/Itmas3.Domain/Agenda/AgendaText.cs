using Itmas3.Domain.Common;

namespace Itmas3.Domain.Agenda;

/// <summary>Text validation shared by the agenda entities. Messages are shown to API clients (HTTP 422).</summary>
internal static class AgendaText
{
    /// <summary>Required single-line text: trimmed, 1..<paramref name="maxLength"/> characters, no control characters.</summary>
    public static string RequiredLine(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException($"{field} es obligatorio.");
        }

        return Line(trimmed, maxLength, field)!;
    }

    /// <summary>Optional single-line text: null when empty, otherwise trimmed and without control characters.</summary>
    public static string? OptionalLine(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : Line(trimmed, maxLength, field);
    }

    /// <summary>Optional multi-line text (description, notes): line breaks and tabs are allowed, other control characters are not.</summary>
    public static string? OptionalText(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{field} supera la longitud máxima de {maxLength} caracteres.");
        }

        return trimmed.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))
            ? throw new DomainException($"{field} contiene caracteres no permitidos.")
            : trimmed;
    }

    private static string? Line(string trimmed, int maxLength, string field)
    {
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{field} supera la longitud máxima de {maxLength} caracteres.");
        }

        return trimmed.Any(char.IsControl) ? throw new DomainException($"{field} contiene caracteres no permitidos.") : trimmed;
    }
}
