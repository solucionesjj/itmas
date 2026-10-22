using Itmas3.Domain.Common;

namespace Itmas3.Domain.Agenda;

/// <summary>
/// A link to the evidence of an execution (SPEC-0006 RN-10): a label and an <c>http</c> or <c>https</c> URL of up to 500
/// characters without embedded credentials. Files are never stored, only links.
/// </summary>
public sealed record EvidenceLink(string Label, string Url)
{
    public const int MaxLabelLength = 100;
    public const int MaxUrlLength = 500;
    public const int MaxPerEvent = 10;

    /// <summary>Validates and normalizes (trims) a link. Throws <see cref="DomainException"/> when it breaks RN-10.</summary>
    public static EvidenceLink Create(string? label, string? url)
    {
        var cleanLabel = AgendaText.RequiredLine(label, MaxLabelLength, "La etiqueta del enlace de evidencia");
        var cleanUrl = url?.Trim();
        if (string.IsNullOrEmpty(cleanUrl))
        {
            throw new DomainException("La URL del enlace de evidencia es obligatoria.");
        }

        if (cleanUrl.Length > MaxUrlLength)
        {
            throw new DomainException($"La URL del enlace de evidencia supera los {MaxUrlLength} caracteres.");
        }

        if (cleanUrl.Any(c => char.IsControl(c) || char.IsWhiteSpace(c))
            || !Uri.TryCreate(cleanUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrEmpty(uri.Host))
        {
            throw new DomainException("El enlace de evidencia debe ser una URL http o https válida.");
        }

        if (uri.UserInfo.Length > 0 || HasUserInfoMarker(cleanUrl))
        {
            throw new DomainException("El enlace de evidencia no puede incluir usuario ni contraseña.");
        }

        return new EvidenceLink(cleanLabel, cleanUrl);
    }

    /// <summary>Validates the set of links of an execution record: at most <see cref="MaxPerEvent"/>.</summary>
    public static IReadOnlyList<EvidenceLink> CreateSet(IEnumerable<(string? Label, string? Url)>? links)
    {
        var result = (links ?? []).Select(link => Create(link.Label, link.Url)).ToList();
        return result.Count <= MaxPerEvent
            ? result
            : throw new DomainException($"Máximo {MaxPerEvent} enlaces de evidencia por actividad.");
    }

    // An '@' in the authority (before the first '/', '?' or '#') means credentials, even when Uri leaves UserInfo empty.
    private static bool HasUserInfoMarker(string url)
    {
        var start = url.IndexOf("://", StringComparison.Ordinal) + 3;
        var end = url.IndexOfAny(['/', '?', '#'], start);
        var authority = end < 0 ? url[start..] : url[start..end];
        return authority.Contains('@', StringComparison.Ordinal);
    }
}
