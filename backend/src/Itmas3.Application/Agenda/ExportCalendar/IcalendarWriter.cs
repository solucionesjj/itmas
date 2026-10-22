using System.Globalization;
using System.Text;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Application.Agenda.Queries;

namespace Itmas3.Application.Agenda.ExportCalendar;

/// <summary>
/// Writes agenda events as an iCalendar file (RFC 5545) for the export of SPEC-0006 RF-21. It is a pure function: the same events,
/// instant and public URL give the same text, with no clock, culture, zone or I/O.
/// <para>
/// Everything that comes from a person (title, description, scope, location, type and tags) is a <c>TEXT</c> value and is escaped by
/// <see cref="EscapeText"/>: backslash, <c>;</c> and <c>,</c> get a backslash, every kind of line break becomes <c>\n</c> and any other
/// control character is dropped, so a value can never end its line and start a new property (CA-19, §14.1). Property names and
/// parameters are constants. Lines are folded to 75 octets without splitting a UTF-8 sequence (§3.1).
/// </para>
/// <para>
/// Times are written in UTC (<c>Z</c>), which needs no <c>VTIMEZONE</c> and is unambiguous whatever the zone of the organization;
/// all-day events use dates with an exclusive <c>DTEND</c>, as stored (P-24). The file carries no e-mail addresses: no organizer, no
/// attendees (RF-21 is for people who are not invited).
/// </para>
/// </summary>
public static class IcalendarWriter
{
    /// <summary>Most octets of a content line, without its line break (RFC 5545 §3.1).</summary>
    public const int MaxLineOctets = 75;

    public const string ProductId = "-//ITMas//Agenda de infraestructura//ES";

    /// <summary>Domain part of every <c>UID</c>: the id of the event is already unique, and the domain must not change so that re-importing updates instead of duplicating.</summary>
    public const string UidDomain = "agenda.itmas";

    private const string LineBreak = "\r\n";

    /// <summary>The <c>UID</c> of an event: stable for the life of the event.</summary>
    public static string UidOf(Guid eventId) => $"{eventId:D}@{UidDomain}";

    /// <summary>
    /// The calendar with one <c>VEVENT</c> per event, with CRLF line ends. <paramref name="generatedAt"/> is the <c>DTSTAMP</c>.
    /// <paramref name="publicUrl"/> is the public address of ITMas for the <c>URL</c> property: without it the property is left out (P-27).
    /// </summary>
    public static string Write(IReadOnlyList<AgendaExportEvent> events, DateTimeOffset generatedAt, string? publicUrl)
    {
        ArgumentNullException.ThrowIfNull(events);
        var output = new StringBuilder();
        Line(output, "BEGIN:VCALENDAR");
        Line(output, "VERSION:2.0");
        Line(output, $"PRODID:{ProductId}");
        Line(output, "CALSCALE:GREGORIAN");
        Line(output, "METHOD:PUBLISH");

        var stamp = UtcStamp(generatedAt);
        foreach (var item in events)
        {
            WriteEvent(output, item, stamp, publicUrl);
        }

        Line(output, "END:VCALENDAR");
        return output.ToString();
    }

    /// <summary>Escapes a <c>TEXT</c> value (RFC 5545 §3.3.11) and removes whatever could end the line: see the type remarks.</summary>
    internal static string EscapeText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 8);
        var afterCarriageReturn = false;
        foreach (var rune in value.EnumerateRunes())
        {
            // CRLF is one line break, not two. Invalid surrogates were already replaced by U+FFFD by the enumeration.
            if (afterCarriageReturn && rune.Value == '\n')
            {
                afterCarriageReturn = false;
                continue;
            }

            afterCarriageReturn = rune.Value == '\r';
            switch (rune.Value)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case ';':
                    builder.Append("\\;");
                    break;
                case ',':
                    builder.Append("\\,");
                    break;
                case '\r' or '\n' or 0x0085 or 0x2028 or 0x2029:
                    builder.Append("\\n");
                    break;
                case '\t':
                    builder.Append('\t');
                    break;
                default:
                    if (!Rune.IsControl(rune))
                    {
                        builder.Append(rune);
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Appends a content line folded to <see cref="MaxLineOctets"/> octets, with its CRLF (RFC 5545 §3.1). A UTF-8 sequence is never split.</summary>
    internal static void Line(StringBuilder output, string line)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(line);

        var octets = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (octets + size > MaxLineOctets)
            {
                // The continuation line starts with one space, which counts as an octet.
                output.Append(LineBreak).Append(' ');
                octets = 1;
            }

            output.Append(rune);
            octets += size;
        }

        output.Append(LineBreak);
    }

    private static void WriteEvent(StringBuilder output, AgendaExportEvent item, string stamp, string? publicUrl)
    {
        Line(output, "BEGIN:VEVENT");
        Line(output, $"UID:{UidOf(item.Id)}");
        Line(output, $"DTSTAMP:{stamp}");
        Line(output, $"LAST-MODIFIED:{UtcStamp(item.LastModifiedUtc)}");
        Line(output, $"SEQUENCE:{Math.Max(0, item.Revision - 1).ToString(CultureInfo.InvariantCulture)}");

        if (item.IsAllDay)
        {
            // Dates, with the end exclusive: the day after the last one (P-24).
            Line(output, $"DTSTART;VALUE=DATE:{DateStamp(item.StartLocal)}");
            Line(output, $"DTEND;VALUE=DATE:{DateStamp(item.EndLocal)}");
        }
        else
        {
            Line(output, $"DTSTART:{UtcStamp(item.StartUtc)}");
            Line(output, $"DTEND:{UtcStamp(item.EndUtc)}");
        }

        Line(output, $"SUMMARY:{EscapeText($"[{item.TypeName}] {item.Title}")}");
        Line(output, $"DESCRIPTION:{EscapeText(Description(item))}");
        if (!string.IsNullOrWhiteSpace(item.Location))
        {
            Line(output, $"LOCATION:{EscapeText(item.Location)}");
        }

        if (Categories(item) is { Length: > 0 } categories)
        {
            Line(output, $"CATEGORIES:{categories}");
        }

        if (EventUrl(publicUrl, item.Id) is { } url)
        {
            Line(output, $"URL:{url}");
        }

        Line(output, "STATUS:CONFIRMED");
        Line(output, "END:VEVENT");
    }

    /// <summary>The same text the Outlook invitation carries, minus the link, which goes in <c>URL</c>: description, scope and impact (RF-16).</summary>
    private static string Description(AgendaExportEvent item)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            parts.Add(item.Description);
        }

        if (!string.IsNullOrWhiteSpace(item.Scope))
        {
            parts.Add($"Alcance: {item.Scope}");
        }

        parts.Add($"Impacto: {OutlookEventContent.ImpactLabel(item.Impact)}");
        return string.Join("\n\n", parts);
    }

    /// <summary>The type and the tags (RF-21, RF-26) as categories, each one escaped on its own so a comma inside a tag does not split it.</summary>
    private static string Categories(AgendaExportEvent item) =>
        string.Join(',', new[] { item.TypeName }.Concat(item.Tags)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Select(category => category.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(EscapeText)
            .Where(category => category.Length > 0));

    /// <summary>
    /// The address of the activity in ITMas, or null when ITMas has no public URL (P-27) or it cannot be written safely: a
    /// <c>URI</c> value is not escaped like text, so it is only used when it is an absolute http or https address with no
    /// credentials, spaces or control characters.
    /// </summary>
    private static string? EventUrl(string? publicUrl, Guid eventId)
    {
        if (string.IsNullOrWhiteSpace(publicUrl)
            || !Uri.TryCreate($"{publicUrl.Trim().TrimEnd('/')}/agenda/events/{eventId:D}", UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length > 0)
        {
            return null;
        }

        var absolute = uri.AbsoluteUri;
        return absolute.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)) ? null : absolute;
    }

    private static string UtcStamp(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string DateStamp(DateTime local) => local.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
}
