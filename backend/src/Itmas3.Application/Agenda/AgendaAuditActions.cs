using Itmas3.Domain.Agenda;

namespace Itmas3.Application.Agenda;

/// <summary>Audit actions of the agenda (SPEC-0006 §12.2), written to the shared audit log.</summary>
public static class AgendaAuditActions
{
    public const string EventCreated = "agenda.event.created";
    public const string EventUpdated = "agenda.event.updated";
    public const string EventDeleted = "agenda.event.deleted";
    public const string EventScheduled = "agenda.event.scheduled";
    public const string EventCancelled = "agenda.event.cancelled";
    public const string EventCompleted = "agenda.event.completed";
    public const string EventCompletionUpdated = "agenda.event.completion-updated";
    public const string EventTagsUpdated = "agenda.event.tags-updated";
    public const string SeriesCreated = "agenda.series.created";
    public const string SeriesUpdated = "agenda.series.updated";
    public const string EventTypeCreated = "agenda.event-type.created";
    public const string EventTypeUpdated = "agenda.event-type.updated";
    public const string EventOutlookPublished = "agenda.event.outlook-published";
    public const string EventOutlookFailed = "agenda.event.outlook-failed";

    public const string EventResourceType = "agenda-event";
    public const string SeriesResourceType = "agenda-series";
    public const string EventTypeResourceType = "agenda-event-type";
}

/// <summary>
/// Audited views (§12.2): the fields of the event, type or series. They never contain secrets, tokens or the raw response of
/// Graph (CA-21); the publication state is only its code and, on failure, the normalized error code.
/// </summary>
internal static class AgendaAuditViews
{
    public static object Of(AgendaEvent agendaEvent) => new
    {
        agendaEvent.SeriesId,
        agendaEvent.TypeId,
        agendaEvent.Title,
        agendaEvent.Description,
        Status = AgendaCodes.ToCode(agendaEvent.Status),
        agendaEvent.IsAllDay,
        Start = agendaEvent.StartsAtLocal,
        End = agendaEvent.EndsAtLocal,
        TimeZone = agendaEvent.TimeZoneId,
        agendaEvent.ResponsibleUserId,
        agendaEvent.Participants,
        agendaEvent.NotifyTeam,
        Impact = AgendaCodes.ToCode(agendaEvent.Impact),
        agendaEvent.Scope,
        agendaEvent.Location,
        agendaEvent.ReminderMinutes,
        Tags = TagNames(agendaEvent),
        agendaEvent.Revision,
    };

    public static object OfCancellation(AgendaEvent agendaEvent) => new
    {
        agendaEvent.Id,
        Status = AgendaCodes.ToCode(agendaEvent.Status),
        agendaEvent.CancelReason,
        agendaEvent.Revision,
    };

    public static object OfCompletion(AgendaEvent agendaEvent) => new
    {
        agendaEvent.Id,
        Status = AgendaCodes.ToCode(agendaEvent.Status),
        Result = agendaEvent.CompletionResult is { } result ? AgendaCodes.ToCode(result) : null,
        agendaEvent.ExecutedAt,
        agendaEvent.CompletionNotes,
        EvidenceLinks = agendaEvent.EvidenceLinks.Select(link => new { link.Label, link.Url }),
        agendaEvent.Revision,
    };

    public static object OfTags(IEnumerable<string> names) => new { Tags = names.ToList() };

    public static object OfType(EventType type) => new
    {
        type.Name,
        type.Description,
        type.DefaultDurationMinutes,
        type.DefaultReminderMinutes,
        type.IsFreeze,
        type.IsActive,
        type.Revision,
    };

    public static object OfSeries(EventSeries series, IReadOnlyCollection<AgendaEvent> occurrences) => new
    {
        series.TypeId,
        Unit = AgendaCodes.ToCode(series.Pattern.Unit),
        series.Pattern.Interval,
        Anchor = series.Pattern.Anchor is { } anchor
            ? new { Kind = AgendaCodes.ToCode(anchor.Kind), anchor.DayOfMonth, anchor.Nth, Weekday = anchor.Weekday.ToString() }
            : null,
        StartsOn = series.Pattern.StartsOn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        EndsOn = series.Pattern.EndsOn?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        series.Pattern.Count,
        EventIds = occurrences.Select(occurrence => occurrence.Id),
    };

    public static object OfOutlook(Guid eventId, string operation, int revision, string? errorCode) => new
    {
        EventId = eventId,
        Operation = operation,
        Revision = revision,
        ErrorCode = errorCode,
    };

    public static IReadOnlyList<string> TagNames(AgendaEvent agendaEvent) =>
        [.. agendaEvent.Tags.Select(tag => tag.Name).Order(StringComparer.OrdinalIgnoreCase)];
}
