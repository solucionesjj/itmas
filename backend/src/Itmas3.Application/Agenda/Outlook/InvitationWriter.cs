using System.Globalization;
using System.Text;
using Itmas3.Application.Agenda.ExportCalendar;
using Itmas3.Domain.Agenda;

namespace Itmas3.Application.Agenda.Outlook;

/// <summary>The iTIP method of an invitation (RFC 5546): a new or updated meeting, or its cancellation.</summary>
public enum InvitationMethod
{
    Request,
    Cancel,
}

/// <summary>
/// Writes the iCalendar part of an SMTP invitation (SPEC-0008 RF-2, RF-3, RF-3b; ADR-0023 decision 4). It is a pure function, next to
/// <see cref="IcalendarWriter"/>, whose escaping and folding it shares: every value that comes from a person is a <c>TEXT</c> value
/// escaped by <see cref="IcalendarWriter.EscapeText"/>, and every address must follow <see cref="AgendaEmail"/>, so a <c>CAL-ADDRESS</c>
/// is written as <c>mailto:</c> plus the address with nothing to escape and no <c>CN</c>. An address that breaks the rule is left out.
/// </summary>
public static class InvitationWriter
{
    /// <summary>Domain part of the <c>UID</c> of the invitations: different from the export (<see cref="IcalendarWriter.UidDomain"/>).</summary>
    public const string UidDomain = "invite.agenda.itmas";

    /// <summary>Subject of the neutral cancellation to whoever was removed (SPEC-0008 RF-3b): nothing of the activity.</summary>
    public const string WithdrawalSubject = "Ya no estás invitado a una actividad de la agenda";

    /// <summary>Body of the neutral cancellation to whoever was removed.</summary>
    public const string WithdrawalText =
        "Ya no estás invitado a esta actividad de la agenda de infraestructura. La reunión se quitará de tu calendario.";

    /// <summary>The <c>UID</c> of the invitations of an event: always derived from its id, never from a stored value.</summary>
    public static string UidOf(Guid eventId) => $"{eventId:D}@{UidDomain}";

    /// <summary>The value of <c>METHOD</c> and of the <c>method</c> parameter of the MIME part.</summary>
    public static string MethodName(InvitationMethod method) => method == InvitationMethod.Cancel ? "CANCEL" : "REQUEST";

    /// <summary>
    /// The calendar with one <c>VEVENT</c> and CRLF line ends. <paramref name="required"/> and <paramref name="optional"/> are the
    /// attendees of this message (for the cancellation to whoever was removed, only them); <paramref name="stamp"/> is the
    /// <c>DTSTAMP</c>, the instant of the send.
    /// </summary>
    public static string Write(
        InvitationMethod method,
        OutlookEventData data,
        string organizer,
        IReadOnlyList<string> required,
        IReadOnlyList<string> optional,
        int sequence,
        DateTimeOffset stamp,
        string? publicUrl)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(required);
        ArgumentNullException.ThrowIfNull(optional);
        var organizerAddress = AgendaEmail.Normalize(organizer)
            ?? throw new ArgumentException("The organizer must follow the address rule.", nameof(organizer));

        var output = new StringBuilder();
        IcalendarWriter.Line(output, "BEGIN:VCALENDAR");
        IcalendarWriter.Line(output, "VERSION:2.0");
        IcalendarWriter.Line(output, $"PRODID:{IcalendarWriter.ProductId}");
        IcalendarWriter.Line(output, "CALSCALE:GREGORIAN");
        IcalendarWriter.Line(output, $"METHOD:{MethodName(method)}");

        IcalendarWriter.Line(output, "BEGIN:VEVENT");
        IcalendarWriter.Line(output, $"UID:{UidOf(data.EventId)}");
        IcalendarWriter.Line(output, $"SEQUENCE:{Math.Max(0, sequence).ToString(CultureInfo.InvariantCulture)}");
        IcalendarWriter.Line(output, $"DTSTAMP:{IcalendarWriter.UtcStamp(stamp)}");
        if (data.IsAllDay)
        {
            // Dates, with the end exclusive: the day after the last one (P-24).
            IcalendarWriter.Line(output, $"DTSTART;VALUE=DATE:{IcalendarWriter.DateStamp(data.StartLocal)}");
            IcalendarWriter.Line(output, $"DTEND;VALUE=DATE:{IcalendarWriter.DateStamp(data.EndLocal)}");
        }
        else
        {
            IcalendarWriter.Line(output, $"DTSTART:{IcalendarWriter.UtcStamp(data.StartUtc)}");
            IcalendarWriter.Line(output, $"DTEND:{IcalendarWriter.UtcStamp(data.EndUtc)}");
        }

        IcalendarWriter.Line(output, $"SUMMARY:{IcalendarWriter.EscapeText(data.Subject)}");
        IcalendarWriter.Line(output, $"DESCRIPTION:{IcalendarWriter.EscapeText(data.BodyText)}");
        if (!string.IsNullOrWhiteSpace(data.Location))
        {
            IcalendarWriter.Line(output, $"LOCATION:{IcalendarWriter.EscapeText(data.Location)}");
        }

        if (IcalendarWriter.EscapeText(data.Category) is { Length: > 0 } category)
        {
            IcalendarWriter.Line(output, $"CATEGORIES:{category}");
        }

        if (IcalendarWriter.EventUrl(publicUrl, data.EventId) is { } url)
        {
            IcalendarWriter.Line(output, $"URL:{url}");
        }

        IcalendarWriter.Line(output, $"ORGANIZER:mailto:{organizerAddress}");
        foreach (var address in Valid(required))
        {
            IcalendarWriter.Line(output, $"ATTENDEE;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=FALSE:mailto:{address}");
        }

        foreach (var address in Valid(optional))
        {
            IcalendarWriter.Line(output, $"ATTENDEE;ROLE=OPT-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=FALSE:mailto:{address}");
        }

        if (method == InvitationMethod.Cancel)
        {
            if (!string.IsNullOrWhiteSpace(data.CancelReason))
            {
                IcalendarWriter.Line(output, $"COMMENT:{IcalendarWriter.EscapeText(data.CancelReason)}");
            }

            IcalendarWriter.Line(output, "STATUS:CANCELLED");
        }
        else
        {
            IcalendarWriter.Line(output, "STATUS:CONFIRMED");
            IcalendarWriter.Line(output, "BEGIN:VALARM");
            IcalendarWriter.Line(output, "ACTION:DISPLAY");
            IcalendarWriter.Line(output, $"DESCRIPTION:{IcalendarWriter.EscapeText(data.Subject)}");
            IcalendarWriter.Line(output, $"TRIGGER:-PT{Math.Max(0, data.ReminderMinutes).ToString(CultureInfo.InvariantCulture)}M");
            IcalendarWriter.Line(output, "END:VALARM");
        }

        IcalendarWriter.Line(output, "END:VEVENT");
        IcalendarWriter.Line(output, "END:VCALENDAR");
        return output.ToString();
    }

    /// <summary>
    /// The neutral cancellation to whoever was removed from an activity (SPEC-0008 RF-3b). It carries only what iTIP needs to find and
    /// remove the meeting (<c>UID</c>, <c>SEQUENCE</c>, the dates, the organizer and these attendees) and a neutral summary: never the
    /// title, the description, the place, the category or the link of the activity as it is now, which the person removed must not see.
    /// </summary>
    public static string WriteWithdrawal(OutlookEventData data, string organizer, IReadOnlyList<string> removed, int sequence, DateTimeOffset stamp)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(removed);
        var organizerAddress = AgendaEmail.Normalize(organizer)
            ?? throw new ArgumentException("The organizer must follow the address rule.", nameof(organizer));

        var output = new StringBuilder();
        IcalendarWriter.Line(output, "BEGIN:VCALENDAR");
        IcalendarWriter.Line(output, "VERSION:2.0");
        IcalendarWriter.Line(output, $"PRODID:{IcalendarWriter.ProductId}");
        IcalendarWriter.Line(output, "CALSCALE:GREGORIAN");
        IcalendarWriter.Line(output, $"METHOD:{MethodName(InvitationMethod.Cancel)}");
        IcalendarWriter.Line(output, "BEGIN:VEVENT");
        IcalendarWriter.Line(output, $"UID:{UidOf(data.EventId)}");
        IcalendarWriter.Line(output, $"SEQUENCE:{Math.Max(0, sequence).ToString(CultureInfo.InvariantCulture)}");
        IcalendarWriter.Line(output, $"DTSTAMP:{IcalendarWriter.UtcStamp(stamp)}");
        if (data.IsAllDay)
        {
            IcalendarWriter.Line(output, $"DTSTART;VALUE=DATE:{IcalendarWriter.DateStamp(data.StartLocal)}");
            IcalendarWriter.Line(output, $"DTEND;VALUE=DATE:{IcalendarWriter.DateStamp(data.EndLocal)}");
        }
        else
        {
            IcalendarWriter.Line(output, $"DTSTART:{IcalendarWriter.UtcStamp(data.StartUtc)}");
            IcalendarWriter.Line(output, $"DTEND:{IcalendarWriter.UtcStamp(data.EndUtc)}");
        }

        IcalendarWriter.Line(output, $"SUMMARY:{IcalendarWriter.EscapeText(WithdrawalSubject)}");
        IcalendarWriter.Line(output, $"ORGANIZER:mailto:{organizerAddress}");
        foreach (var address in Valid(removed))
        {
            IcalendarWriter.Line(output, $"ATTENDEE;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=FALSE:mailto:{address}");
        }

        IcalendarWriter.Line(output, "STATUS:CANCELLED");
        IcalendarWriter.Line(output, "END:VEVENT");
        IcalendarWriter.Line(output, "END:VCALENDAR");
        return output.ToString();
    }

    /// <summary>The addresses that follow the rule, normalized and without repeats, in their order.</summary>
    private static IEnumerable<string> Valid(IEnumerable<string> addresses) =>
        addresses.Select(AgendaEmail.Normalize).OfType<string>().Distinct(StringComparer.Ordinal);
}
