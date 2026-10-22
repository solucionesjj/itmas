using System.Globalization;
using System.Text;
using Itmas3.Application.Agenda.ExportCalendar;
using Itmas3.Application.Agenda.Queries;
using Itmas3.Domain.Agenda;

namespace Itmas3.UnitTests.Application.Agenda;

/// <summary>
/// SPEC-0006 CA-19: the <c>.ics</c> is RFC 5545, has a stable <c>UID</c>, carries the type and the tags as <c>CATEGORIES</c>, escapes
/// line breaks, <c>;</c> and <c>,</c>, folds long lines to 75 octets and cannot be made to start a new property by what a person typed.
/// The writer is pure, so every test compares text against a fixture.
/// </summary>
public sealed class IcalendarWriterTests
{
    private static readonly Guid EventId = Guid.Parse("0196b7a0-0000-7000-8000-0000000000e1");
    private static readonly DateTimeOffset Stamp = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private const string TypeName = "Mantenimiento y parches";

    private static AgendaExportEvent Event(
        string title = "Parches de SO",
        string? description = "Aplicar los parches",
        string? scope = null,
        string? location = null,
        string typeName = TypeName,
        EventImpact impact = EventImpact.None,
        string[]? tags = null,
        int revision = 1,
        Guid? id = null) =>
        new(
            id ?? EventId,
            typeName,
            title,
            description,
            scope,
            location,
            impact,
            IsAllDay: false,
            StartLocal: new DateTime(2026, 11, 10, 9, 0, 0),
            EndLocal: new DateTime(2026, 11, 10, 11, 0, 0),
            StartUtc: new DateTimeOffset(2026, 11, 10, 14, 0, 0, TimeSpan.Zero),
            EndUtc: new DateTimeOffset(2026, 11, 10, 16, 0, 0, TimeSpan.Zero),
            Tags: tags ?? [],
            LastModifiedUtc: new DateTimeOffset(2026, 10, 10, 15, 0, 0, TimeSpan.Zero),
            Revision: revision);

    private static string Write(AgendaExportEvent[] events, string? publicUrl = null) => IcalendarWriter.Write(events, Stamp, publicUrl);

    /// <summary>The lines of the file as a calendar program reads them: folded lines joined, split at CRLF.</summary>
    private static string[] Unfolded(string ics) => [.. ics.Replace("\r\n ", string.Empty, StringComparison.Ordinal).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)];

    private static string Property(string ics, string name) =>
        Unfolded(ics).Single(line => line.StartsWith(name + ":", StringComparison.Ordinal) || line.StartsWith(name + ";", StringComparison.Ordinal));

    private static string Fixture(string text) => text.ReplaceLineEndings("\r\n");

    // ---- Fixtures: the complete text of a file --------------------------------------------------------------------------------------

    [Fact]
    public void CA19_a_timed_event_is_written_in_utc_with_its_type_tags_and_url()
    {
        var ics = Write([Event(title: "Parches", description: "Mes de noviembre", tags: ["windows", "2026-t4"], revision: 3)], "https://itmas.example.com/");

        Assert.Equal(
            Fixture("""
                BEGIN:VCALENDAR
                VERSION:2.0
                PRODID:-//ITMas//Agenda de infraestructura//ES
                CALSCALE:GREGORIAN
                METHOD:PUBLISH
                BEGIN:VEVENT
                UID:0196b7a0-0000-7000-8000-0000000000e1@agenda.itmas
                DTSTAMP:20261018T120000Z
                LAST-MODIFIED:20261010T150000Z
                SEQUENCE:2
                DTSTART:20261110T140000Z
                DTEND:20261110T160000Z
                SUMMARY:[Mantenimiento y parches] Parches
                DESCRIPTION:Mes de noviembre\n\nImpacto: Ninguno
                CATEGORIES:Mantenimiento y parches,windows,2026-t4
                URL:https://itmas.example.com/agenda/events/0196b7a0-0000-7000-8000-0000000
                 000e1
                STATUS:CONFIRMED
                END:VEVENT
                END:VCALENDAR

                """),
            ics);
    }

    [Fact]
    public void CA19_an_all_day_event_uses_dates_with_an_exclusive_end_and_has_no_url_without_a_public_address()
    {
        var allDay = Event(title: "Congelamiento", description: null, location: "Todas las sedes", typeName: "Congelamiento de cambios") with
        {
            IsAllDay = true,
            StartLocal = new DateTime(2026, 12, 24, 0, 0, 0),
            EndLocal = new DateTime(2026, 12, 26, 0, 0, 0), // the last day is the 25th: the end is the day after (P-24)
            Impact = EventImpact.Outage,
        };

        var ics = Write([allDay]);

        Assert.Equal(
            Fixture("""
                BEGIN:VCALENDAR
                VERSION:2.0
                PRODID:-//ITMas//Agenda de infraestructura//ES
                CALSCALE:GREGORIAN
                METHOD:PUBLISH
                BEGIN:VEVENT
                UID:0196b7a0-0000-7000-8000-0000000000e1@agenda.itmas
                DTSTAMP:20261018T120000Z
                LAST-MODIFIED:20261010T150000Z
                SEQUENCE:0
                DTSTART;VALUE=DATE:20261224
                DTEND;VALUE=DATE:20261226
                SUMMARY:[Congelamiento de cambios] Congelamiento
                DESCRIPTION:Impacto: Indisponibilidad
                LOCATION:Todas las sedes
                CATEGORIES:Congelamiento de cambios
                STATUS:CONFIRMED
                END:VEVENT
                END:VCALENDAR

                """),
            ics);
    }

    [Fact]
    public void CA19_events_are_written_in_the_order_given_and_each_one_is_its_own_vevent()
    {
        var other = Guid.Parse("0196b7a0-0000-7000-8000-0000000000e2");

        var lines = Unfolded(Write([Event(title: "Primera"), Event(title: "Segunda", id: other)]));

        Assert.Equal(2, lines.Count(line => line == "BEGIN:VEVENT"));
        Assert.Equal(2, lines.Count(line => line == "END:VEVENT"));
        Assert.Equal(["SUMMARY:[Mantenimiento y parches] Primera", "SUMMARY:[Mantenimiento y parches] Segunda"], lines.Where(line => line.StartsWith("SUMMARY", StringComparison.Ordinal)));
        Assert.Equal("BEGIN:VCALENDAR", lines[0]);
        Assert.Equal("END:VCALENDAR", lines[^1]);
    }

    [Fact]
    public void CA19_a_range_without_events_is_a_calendar_without_components()
    {
        var ics = Write([]);

        Assert.Equal(
            Fixture("""
                BEGIN:VCALENDAR
                VERSION:2.0
                PRODID:-//ITMas//Agenda de infraestructura//ES
                CALSCALE:GREGORIAN
                METHOD:PUBLISH
                END:VCALENDAR

                """),
            ics);
    }

    // ---- Escaping --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void CA19_semicolons_commas_backslashes_and_line_breaks_are_escaped()
    {
        var item = Event(
            title: "Cambio; de, clave\\ y más",
            description: "Línea uno\nLínea dos\r\nLínea tres\rcuatro",
            scope: "Servidores; red, y \\ otros",
            location: "Sala A; piso 2, edificio B");

        var ics = Write([item]);

        Assert.Equal("SUMMARY:[Mantenimiento y parches] Cambio\\; de\\, clave\\\\ y más", Property(ics, "SUMMARY"));
        Assert.Equal(
            "DESCRIPTION:Línea uno\\nLínea dos\\nLínea tres\\ncuatro\\n\\nAlcance: Servidores\\; red\\, y \\\\ otros\\n\\nImpacto: Ninguno",
            Property(ics, "DESCRIPTION"));
        Assert.Equal("LOCATION:Sala A\\; piso 2\\, edificio B", Property(ics, "LOCATION"));
    }

    [Theory]
    [InlineData("a\r\nb", "a\\nb")] // CRLF is one break, not two
    [InlineData("a\nb", "a\\nb")]
    [InlineData("a\rb", "a\\nb")]
    [InlineData("a\r\n\r\nb", "a\\n\\nb")]
    [InlineData("a\u0085b", "a\\nb")] // NEL
    [InlineData("a\u2028b", "a\\nb")] // line separator
    [InlineData("a\u2029b", "a\\nb")] // paragraph separator
    [InlineData("a\u000Bb", "ab")] // vertical tab: dropped, never a break
    [InlineData("a\u000Cb", "ab")] // form feed
    [InlineData("a\0b", "ab")]
    [InlineData("a\u007Fb", "ab")]
    [InlineData("a\tb", "a\tb")] // a tab is allowed in TEXT
    [InlineData("a\\nb", "a\\\\nb")] // an escape written by a person is not an escape
    [InlineData("", "")]
    [InlineData(null, "")]
    public void CA19_escape_text_cases(string? raw, string expected) => Assert.Equal(expected, IcalendarWriter.EscapeText(raw));

    [Fact]
    public void CA19_a_lone_surrogate_becomes_the_replacement_character_so_the_file_is_valid_utf8()
    {
        var escaped = IcalendarWriter.EscapeText("a\uD800b\uDC00c");

        Assert.Equal("a\uFFFDb\uFFFDc", escaped);
        Assert.Equal(escaped, new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(Encoding.UTF8.GetBytes(escaped)));
    }

    // ---- Injection (§14.1: "Inyección de propiedades en el .ics") ---------------------------------------------------------------------

    private const string Attack = "Reunión\r\nATTENDEE;CN=Eve:mailto:eve@example.com\nDTSTART:20300101T000000Z\rEND:VEVENT\u2028BEGIN:VEVENT\u0085ORGANIZER:mailto:eve@example.com\r\nEND:VCALENDAR";

    public static TheoryData<string> Fields => ["title", "description", "scope", "location", "type", "tag"];

    [Theory]
    [MemberData(nameof(Fields))]
    public void CA19_a_value_that_tries_to_inject_properties_stays_inside_its_own_line(string field)
    {
        var item = field switch
        {
            "title" => Event(title: Attack),
            "description" => Event(description: Attack),
            "scope" => Event(scope: Attack),
            "location" => Event(location: Attack),
            "type" => Event(typeName: Attack),
            _ => Event(tags: [Attack]),
        };

        var ics = Write([item], "https://itmas.example.com");
        var lines = Unfolded(ics);

        // Nothing but CRLF ends a line: no stray CR, LF, NEL or separator remains, so there is no extra property.
        var withoutBreaks = ics.Replace("\r\n", string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', withoutBreaks);
        Assert.DoesNotContain('\n', withoutBreaks);
        Assert.DoesNotContain('\u0085', ics);
        Assert.DoesNotContain('\u2028', ics);
        Assert.DoesNotContain('\u2029', ics);

        // The structure is exactly the one of a single event: one start and one end of each component, one start date, no attendee or organizer.
        Assert.Equal(1, lines.Count(line => line == "BEGIN:VEVENT"));
        Assert.Equal(1, lines.Count(line => line == "END:VEVENT"));
        Assert.Equal(1, lines.Count(line => line == "END:VCALENDAR"));
        Assert.Equal(1, lines.Count(line => line.StartsWith("DTSTART", StringComparison.Ordinal)));
        Assert.DoesNotContain(lines, line => line.StartsWith("ATTENDEE", StringComparison.Ordinal) || line.StartsWith("ORGANIZER", StringComparison.Ordinal));
        Assert.Equal("DTSTART:20261110T140000Z", Property(ics, "DTSTART"));

        // The attack text is still there, as text: escaped, in the line of the property it came in.
        var owner = field switch { "title" or "type" => "SUMMARY", "description" => "DESCRIPTION", "scope" => "DESCRIPTION", "location" => "LOCATION", _ => "CATEGORIES" };
        var line = Property(ics, owner);
        Assert.Contains("ATTENDEE\\;CN=Eve:mailto:eve@example.com\\nDTSTART:20300101T000000Z\\nEND:VEVENT\\nBEGIN:VEVENT\\nORGANIZER:mailto:eve@example.com\\nEND:VCALENDAR", line, StringComparison.Ordinal);
    }

    [Fact]
    public void CA19_the_file_carries_no_addresses_neither_organizer_nor_attendees()
    {
        var ics = Write([Event(tags: ["windows"], description: "Aviso a todos")], "https://itmas.example.com");

        Assert.DoesNotContain("ATTENDEE", ics, StringComparison.Ordinal);
        Assert.DoesNotContain("ORGANIZER", ics, StringComparison.Ordinal);
        Assert.DoesNotContain("mailto:", ics, StringComparison.Ordinal);
        Assert.DoesNotContain("@example.com", ics, StringComparison.Ordinal);
    }

    // ---- CATEGORIES ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void CA19_categories_are_the_type_and_the_tags_each_one_escaped_on_its_own()
    {
        var ics = Write([Event(typeName: "Revisión, de accesos", tags: ["windows", "producción", "a,b", "x;y", "Windows", "revisión, de accesos"])]);

        // "Windows" repeats "windows" and the last one repeats the type: each category appears once.
        Assert.Equal("CATEGORIES:Revisión\\, de accesos,windows,producción,a\\,b,x\\;y", Property(ics, "CATEGORIES"));
    }

    [Fact]
    public void CA19_a_category_that_is_empty_once_escaped_is_dropped_and_no_category_means_no_property()
    {
        var withBlank = Write([Event(tags: ["\u0001\u0002", "windows"])]);
        var none = Write([Event(typeName: "\u0001", tags: [" "])]);

        Assert.Equal("CATEGORIES:Mantenimiento y parches,windows", Property(withBlank, "CATEGORIES"));
        Assert.DoesNotContain(Unfolded(none), line => line.StartsWith("CATEGORIES", StringComparison.Ordinal));
    }

    // ---- UID -------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void CA19_the_uid_depends_only_on_the_event_so_exporting_again_updates_instead_of_duplicating()
    {
        var first = Write([Event(title: "Antes", revision: 1)]);
        var later = IcalendarWriter.Write([Event(title: "Después", revision: 5, tags: ["nueva"])], Stamp.AddDays(30), "https://itmas.example.com");
        var other = Write([Event(id: Guid.Parse("0196b7a0-0000-7000-8000-0000000000e2"))]);

        Assert.Equal("UID:0196b7a0-0000-7000-8000-0000000000e1@agenda.itmas", Property(first, "UID"));
        Assert.Equal(Property(first, "UID"), Property(later, "UID"));
        Assert.NotEqual(Property(first, "UID"), Property(other, "UID"));
        Assert.Equal("0196b7a0-0000-7000-8000-0000000000e1@agenda.itmas", IcalendarWriter.UidOf(EventId));
    }

    [Theory]
    [InlineData(1, "SEQUENCE:0")]
    [InlineData(2, "SEQUENCE:1")]
    [InlineData(7, "SEQUENCE:6")]
    [InlineData(0, "SEQUENCE:0")]
    public void CA19_the_sequence_grows_with_the_revision_so_a_client_applies_the_update(int revision, string expected) =>
        Assert.Equal(expected, Property(Write([Event(revision: revision)]), "SEQUENCE"));

    // ---- URL (P-27) ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P27_the_url_points_to_the_activity_when_there_is_a_public_address()
    {
        Assert.Equal(
            "URL:https://itmas.example.com/agenda/events/0196b7a0-0000-7000-8000-0000000000e1",
            Property(Write([Event()], "  https://itmas.example.com/  "), "URL"));
        Assert.Equal(
            "URL:http://itmas.local:8088/agenda/events/0196b7a0-0000-7000-8000-0000000000e1",
            Property(Write([Event()], "http://itmas.local:8088"), "URL"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("itmas.example.com")] // not absolute
    [InlineData("ftp://itmas.example.com")] // not http or https
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:secret@itmas.example.com")] // credentials never go in a file
    public void P27_without_a_safe_public_address_the_url_property_is_left_out(string? publicUrl)
    {
        var ics = Write([Event()], publicUrl);

        Assert.DoesNotContain(Unfolded(ics), line => line.StartsWith("URL", StringComparison.Ordinal));
    }

    // ---- Folding (§3.1) --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void CA19_a_content_line_is_folded_at_75_octets_with_a_space_at_the_start_of_each_continuation()
    {
        var builder = new StringBuilder();
        IcalendarWriter.Line(builder, "X-TEST:" + new string('a', 193)); // 200 octets

        var physical = builder.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal([75, 75, 52], physical.Select(line => Encoding.UTF8.GetByteCount(line))); // 75, then " " + 74, then " " + 51
        Assert.All(physical.Skip(1), line => Assert.StartsWith(" ", line, StringComparison.Ordinal));
        Assert.False(physical[1].StartsWith("  ", StringComparison.Ordinal));
        Assert.EndsWith("\r\n", builder.ToString(), StringComparison.Ordinal);
        Assert.Equal("X-TEST:" + new string('a', 193), string.Concat(physical.Select((line, index) => index == 0 ? line : line[1..])));
    }

    [Fact]
    public void CA19_a_line_of_exactly_75_octets_is_not_folded_and_one_of_76_is()
    {
        var exact = new StringBuilder();
        IcalendarWriter.Line(exact, "X-TEST:" + new string('a', 68));
        var over = new StringBuilder();
        IcalendarWriter.Line(over, "X-TEST:" + new string('a', 69));

        Assert.Equal("X-TEST:" + new string('a', 68) + "\r\n", exact.ToString());
        Assert.Equal("X-TEST:" + new string('a', 68) + "\r\n a\r\n", over.ToString());
    }

    [Theory]
    [InlineData("ñ")] // 2 octets
    [InlineData("€")] // 3 octets
    [InlineData("😀")] // 4 octets (a surrogate pair)
    [InlineData("é")] // 2 octets
    public void CA19_folding_counts_octets_and_never_splits_a_utf8_sequence_or_a_surrogate_pair(string unit)
    {
        var title = string.Concat(Enumerable.Repeat(unit, 120));

        var ics = Write([Event(title: title)]);

        foreach (var physical in ics.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.True(Encoding.UTF8.GetByteCount(physical) <= IcalendarWriter.MaxLineOctets, $"{physical.Length} chars, {Encoding.UTF8.GetByteCount(physical)} octets");
        }

        // The bytes are valid UTF-8 as a whole and each physical line on its own (a split sequence would not decode strictly).
        var strict = new UTF8Encoding(false, throwOnInvalidBytes: true);
        foreach (var physical in ics.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.Equal(physical, strict.GetString(Encoding.UTF8.GetBytes(physical)));
        }

        Assert.Equal($"SUMMARY:[{TypeName}] {title}", Property(ics, "SUMMARY"));
    }

    [Fact]
    public void CA19_a_long_value_with_escapes_still_unfolds_to_the_same_escaped_text()
    {
        var description = string.Join("\n", Enumerable.Range(1, 40).Select(number => $"Paso {number}; revisar, anotar"));

        var ics = Write([Event(description: description)]);

        Assert.Equal(
            "DESCRIPTION:" + IcalendarWriter.EscapeText(description) + "\\n\\nImpacto: Ninguno",
            Property(ics, "DESCRIPTION"));
        Assert.All(ics.Split("\r\n", StringSplitOptions.RemoveEmptyEntries), line => Assert.True(Encoding.UTF8.GetByteCount(line) <= IcalendarWriter.MaxLineOctets));
    }

    [Fact]
    public void CA19_every_line_ends_with_crlf_and_none_with_a_bare_lf()
    {
        var ics = Write([Event(description: "uno\ndos"), Event(title: "Otra")]);

        Assert.EndsWith("\r\n", ics, StringComparison.Ordinal);
        Assert.Equal(ics.Count(c => c == '\n'), ics.Count(c => c == '\r'));
        Assert.DoesNotContain("\n\n", ics, StringComparison.Ordinal);
        Assert.DoesNotContain("\r\r", ics, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", ics.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void CA19_the_location_is_left_out_when_the_event_has_none_and_the_description_always_has_the_impact()
    {
        var lines = Unfolded(Write([Event(description: null, location: "  ")]));

        Assert.DoesNotContain(lines, line => line.StartsWith("LOCATION", StringComparison.Ordinal));
        Assert.Contains("DESCRIPTION:Impacto: Ninguno", lines);
    }

    [Fact]
    public void CA19_the_writer_is_deterministic_and_does_not_depend_on_the_culture()
    {
        // ar-SA writes Hijri years and Arabic-Indic digits and th-TH a Buddhist year: a number or date formatted with the current culture would show it.
        CultureInfo arabic;
        CultureInfo thai;
        try
        {
            arabic = new CultureInfo("ar-SA");
            thai = new CultureInfo("th-TH");
        }
        catch (CultureNotFoundException)
        {
            Assert.Skip("This environment has no culture data (invariant globalization).");
            return;
        }

        var original = CultureInfo.CurrentCulture;
        try
        {
            var events = new[] { Event(tags: ["windows"], revision: 12) };
            CultureInfo.CurrentCulture = arabic;
            var underArabic = Write(events, "https://itmas.example.com");
            CultureInfo.CurrentCulture = thai;
            var underThai = Write(events, "https://itmas.example.com");

            Assert.Equal(underArabic, underThai);
            Assert.Contains("DTSTART:20261110T140000Z", underArabic, StringComparison.Ordinal);
            Assert.Contains("DTSTAMP:20261018T120000Z", underArabic, StringComparison.Ordinal);
            Assert.Contains("SEQUENCE:11", underArabic, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
