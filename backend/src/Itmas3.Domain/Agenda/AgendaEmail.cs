using System.Text.RegularExpressions;

namespace Itmas3.Domain.Agenda;

/// <summary>
/// The strict address rule of the agenda (SPEC-0008 RF-2b, ADR-0023 decision 4). An address that passes it can be written as a
/// MIME mailbox and as an iCalendar <c>CAL-ADDRESS</c> (<c>mailto:</c>) without escaping:
/// <list type="bullet">
/// <item>local part: <c>[a-z0-9._+-]</c>, no dot at the start, at the end or twice in a row (a subset of the RFC 5321
/// <i>dot-atom</i> without the characters that mean something in a <c>mailto:</c> URI, RFC 6068);</item>
/// <item>domain: ASCII LDH labels, at least two;</item>
/// <item>normalized: trimmed and lowercase.</item>
/// <item>anchored with <c>\z</c>, not <c>$</c>: in .NET <c>$</c> also matches before a final line feed, which would let
/// <c>"ana\n@example.com"</c> through.</item>
/// </list>
/// </summary>
public static partial class AgendaEmail
{
    public const int MaxLength = 254;

    private const int MaxLocalLength = 64;

    /// <summary>The normalized address, or null when it does not follow the rule.</summary>
    public static string? Normalize(string? address)
    {
        var value = address?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return null;
        }

        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at > MaxLocalLength || at != value.LastIndexOf('@'))
        {
            return null;
        }

        return LocalPattern().IsMatch(value[..at]) && DomainPattern().IsMatch(value[(at + 1)..]) ? value : null;
    }

    /// <summary>Whether <paramref name="address"/> follows the rule.</summary>
    public static bool IsValid(string? address) => Normalize(address) is not null;

    /// <summary>The domain of a normalized address.</summary>
    public static string DomainOf(string normalizedAddress)
    {
        ArgumentNullException.ThrowIfNull(normalizedAddress);
        return normalizedAddress[(normalizedAddress.LastIndexOf('@') + 1)..];
    }

    [GeneratedRegex(@"^[a-z0-9_+-]+(\.[a-z0-9_+-]+)*\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex LocalPattern();

    [GeneratedRegex(@"^(?=.{1,253}\z)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex DomainPattern();
}
