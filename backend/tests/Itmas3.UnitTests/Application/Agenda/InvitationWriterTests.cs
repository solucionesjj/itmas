using Itmas3.Application.Agenda.Outlook;

namespace Itmas3.UnitTests.Application.Agenda;

/// <summary>SPEC-0008 CA-8 ("escritor de invitaciones"): the iCalendar part of the SMTP invitations (ADR-0023 decision 4).</summary>
public sealed class InvitationWriterTests
{
    private static readonly Guid EventId = Guid.Parse("0196b7a0-0000-7000-8000-0000000000e1");
    private static readonly DateTimeOffset Stamp = new(2026, 11, 6, 15, 30, 0, TimeSpan.Zero);

    private static OutlookEventData Data(bool allDay = false, string subject = "[Mantenimiento] Parches", string? reason = null) =>
        new(
            EventId,
            subject,
            "Aplicar los parches del mes\n\nImpacto: Ninguno",
            allDay ? new DateTime(2026, 11, 20) : new DateTime(2026, 11, 20, 9, 0, 0),
            allDay ? new DateTime(2026, 11, 22) : new DateTime(2026, 11, 20, 11, 0, 0),
            "America/Bogota",
            allDay,
            "Remoto",
            60,
            "Mantenimiento",
            ["ana@example.com"],
            ["equipo@example.com"])
        {
            StartUtc = new DateTimeOffset(2026, 11, 20, 14, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 11, 20, 16, 0, 0, TimeSpan.Zero),
            CancelReason = reason,
        };

    /// <summary>The content lines, unfolded (RFC 5545 §3.1: a CRLF followed by a space continues the line).</summary>
    private static string[] Lines(string calendar) => calendar.Replace("\r\n ", string.Empty, StringComparison.Ordinal).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    private static string Write(InvitationMethod method, OutlookEventData data, IReadOnlyList<string>? required = null, IReadOnlyList<string>? optional = null, int sequence = 7) =>
        InvitationWriter.Write(method, data, "Agenda@Example.com", required ?? data.RequiredAttendees, optional ?? data.OptionalAttendees, sequence, Stamp, "https://itmas.example.com/");

    [Fact]
    public void A_request_carries_the_meeting_the_organizer_the_attendees_with_their_roles_and_the_reminder()
    {
        var lines = Lines(Write(InvitationMethod.Request, Data()));

        Assert.Contains("METHOD:REQUEST", lines);
        Assert.Contains($"UID:{EventId:D}@invite.agenda.itmas", lines);
        Assert.Contains("SEQUENCE:7", lines);
        Assert.Contains("DTSTAMP:20261106T153000Z", lines);
        Assert.Contains("DTSTART:20261120T140000Z", lines);
        Assert.Contains("DTEND:20261120T160000Z", lines);
        Assert.Contains("SUMMARY:[Mantenimiento] Parches", lines);
        Assert.Contains("LOCATION:Remoto", lines);
        Assert.Contains("CATEGORIES:Mantenimiento", lines);
        Assert.Contains($"URL:https://itmas.example.com/agenda/events/{EventId:D}", lines);
        Assert.Contains("ORGANIZER:mailto:agenda@example.com", lines);
        Assert.Contains("ATTENDEE;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=FALSE:mailto:ana@example.com", lines);
        Assert.Contains("ATTENDEE;ROLE=OPT-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=FALSE:mailto:equipo@example.com", lines);
        Assert.Contains("STATUS:CONFIRMED", lines);
        Assert.Contains("TRIGGER:-PT60M", lines);
        Assert.Single(lines, line => line == "BEGIN:VEVENT");
        Assert.DoesNotContain(lines, line => line.Contains("CN=", StringComparison.Ordinal));
    }

    [Fact]
    public void A_cancellation_carries_the_reason_and_no_reminder_and_only_the_given_attendees()
    {
        var lines = Lines(Write(InvitationMethod.Cancel, Data(reason: "Se movió a enero"), required: ["luis@example.com"], optional: [], sequence: 6));

        Assert.Contains("METHOD:CANCEL", lines);
        Assert.Contains("SEQUENCE:6", lines);
        Assert.Contains("STATUS:CANCELLED", lines);
        Assert.Contains("COMMENT:Se movió a enero", lines);
        Assert.Single(lines, line => line.StartsWith("ATTENDEE", StringComparison.Ordinal));
        Assert.Contains("ATTENDEE;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=FALSE:mailto:luis@example.com", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("BEGIN:VALARM", StringComparison.Ordinal));
    }

    [Fact]
    public void An_all_day_meeting_uses_dates_with_the_exclusive_end()
    {
        var lines = Lines(Write(InvitationMethod.Request, Data(allDay: true)));

        Assert.Contains("DTSTART;VALUE=DATE:20261120", lines);
        Assert.Contains("DTEND;VALUE=DATE:20261122", lines);
    }

    [Fact]
    public void The_uid_is_derived_from_the_event_and_differs_from_the_export()
    {
        Assert.Equal($"{EventId:D}@invite.agenda.itmas", InvitationWriter.UidOf(EventId));
        Assert.NotEqual(Itmas3.Application.Agenda.ExportCalendar.IcalendarWriter.UidOf(EventId), InvitationWriter.UidOf(EventId));
    }

    [Fact]
    public void Text_from_a_person_cannot_end_its_line_or_start_a_property()
    {
        var calendar = Write(InvitationMethod.Cancel, Data(subject: "Parches\r\nATTENDEE:mailto:evil@example.org", reason: "x\nMETHOD:PUBLISH;a,b"));
        var lines = Lines(calendar);

        Assert.DoesNotContain(lines, line => line.StartsWith("ATTENDEE:mailto:evil", StringComparison.Ordinal));
        Assert.Single(lines, line => line.StartsWith("METHOD:", StringComparison.Ordinal));
        Assert.Contains(@"SUMMARY:Parches\nATTENDEE:mailto:evil@example.org", lines);
        Assert.Contains(@"COMMENT:x\nMETHOD:PUBLISH\;a\,b", lines);
    }

    [Theory]
    [InlineData("evil@example.org;ROLE=CHAIR")]
    [InlineData("evil@example.org:x")]
    [InlineData("\"evil\"@example.org")]
    [InlineData("evil?x=y@example.org")]
    [InlineData("evil@example.org\r\nORGANIZER:mailto:x@example.org")]
    public void An_address_that_breaks_the_rule_is_left_out_and_never_becomes_a_parameter(string address)
    {
        var lines = Lines(Write(InvitationMethod.Request, Data(), required: [address, "ana@example.com"], optional: []));

        Assert.Single(lines, line => line.StartsWith("ATTENDEE", StringComparison.Ordinal));
        Assert.Single(lines, line => line.StartsWith("ORGANIZER", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("evil", StringComparison.Ordinal));
    }

    [Fact]
    public void Long_lines_are_folded_to_75_octets()
    {
        var calendar = Write(InvitationMethod.Request, Data(subject: new string('á', 200)));

        Assert.All(calendar.Split("\r\n"), line => Assert.True(System.Text.Encoding.UTF8.GetByteCount(line) <= 75));
    }

    [Fact]
    public void An_organizer_that_breaks_the_rule_is_refused() =>
        Assert.Throws<ArgumentException>(() =>
            InvitationWriter.Write(InvitationMethod.Request, Data(), "agenda;x@example.com", [], [], 1, Stamp, null));
}
