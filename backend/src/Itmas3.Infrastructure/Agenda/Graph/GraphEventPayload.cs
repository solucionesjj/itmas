using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Itmas3.Application.Agenda.Outlook;

namespace Itmas3.Infrastructure.Agenda.Graph;

/// <summary>
/// The JSON bodies sent to Graph (SPEC-0006 RF-16, ADR-0016 §6), written by hand so that exactly these properties travel and
/// nothing else: the tags of ITMas, for instance, are never part of them (P-19). Text is sent as plain text (RNF-7).
/// </summary>
internal static class GraphEventPayload
{
    // Graph reads this shape: seven fractional digits, no offset (the zone travels next to it).
    private const string LocalDateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff";

    /// <summary>
    /// The event to create (with <paramref name="transactionId"/>) or to update (without it: Graph does not allow changing it
    /// once the event exists). An update carries the whole state, so what was removed in ITMas disappears in Outlook as well.
    /// </summary>
    public static byte[] ForEvent(OutlookEventData data, string? transactionId)
    {
        ArgumentNullException.ThrowIfNull(data);
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("subject", data.Subject);
            json.WriteStartObject("body");
            json.WriteString("contentType", "text");
            json.WriteString("content", data.BodyText);
            json.WriteEndObject();

            var (start, end) = Bounds(data);
            WriteMoment(json, "start", start, data.TimeZoneId);
            WriteMoment(json, "end", end, data.TimeZoneId);
            json.WriteBoolean("isAllDay", data.IsAllDay);

            // An empty name clears the location of an event that had one.
            json.WriteStartObject("location");
            json.WriteString("displayName", data.Location ?? string.Empty);
            json.WriteEndObject();

            json.WriteStartArray("categories");
            json.WriteStringValue(data.Category);
            json.WriteEndArray();

            json.WriteBoolean("isReminderOn", true);
            json.WriteNumber("reminderMinutesBeforeStart", data.ReminderMinutes);

            json.WriteStartArray("attendees");
            WriteAttendees(json, data.RequiredAttendees, "required");
            WriteAttendees(json, data.OptionalAttendees, "optional");
            json.WriteEndArray();

            if (transactionId is not null)
            {
                json.WriteString("transactionId", transactionId);
            }

            json.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>The cancellation notice: the reason goes to the invitees as the comment of the cancellation (ADR-0016 §5).</summary>
    public static byte[] ForCancellation(string reason)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("comment", reason);
            json.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// All-day events are whole days with an exclusive end (P-24): both bounds at midnight and the end after the start, which is what
    /// Graph demands of <c>isAllDay</c>. Timed events go as they are.
    /// </summary>
    private static (DateTime Start, DateTime End) Bounds(OutlookEventData data)
    {
        if (!data.IsAllDay)
        {
            return (data.StartLocal, data.EndLocal);
        }

        var start = data.StartLocal.Date;
        var end = data.EndLocal.Date;
        return (start, end > start ? end : start.AddDays(1));
    }

    private static void WriteMoment(Utf8JsonWriter json, string name, DateTime local, string timeZoneId)
    {
        json.WriteStartObject(name);
        json.WriteString("dateTime", local.ToString(LocalDateTimeFormat, CultureInfo.InvariantCulture));
        json.WriteString("timeZone", timeZoneId);
        json.WriteEndObject();
    }

    private static void WriteAttendees(Utf8JsonWriter json, IReadOnlyList<string> addresses, string type)
    {
        foreach (var address in addresses)
        {
            json.WriteStartObject();
            json.WriteStartObject("emailAddress");
            json.WriteString("address", address);
            json.WriteEndObject();
            json.WriteString("type", type);
            json.WriteEndObject();
        }
    }
}
