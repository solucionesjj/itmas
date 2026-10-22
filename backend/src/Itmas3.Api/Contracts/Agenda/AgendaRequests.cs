using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Events;
using Itmas3.Application.Agenda.Series;
using Itmas3.Application.Agenda.Types;
using Itmas3.Domain.Agenda;

namespace Itmas3.Api.Contracts.Agenda;

/// <summary>
/// Shared rules of the agenda contract (SPEC-0006 §12.1). The contract only rejects what is malformed (400): the business rules
/// of RN-1 to RN-10 (lengths, limits, domains, dates in the past) are enforced by the domain and answer 422.
/// </summary>
public static class AgendaContract
{
    public const string ImpactPattern = "^(none|degradation|outage)$";
    public const string StatusFilterPattern = "^(draft|scheduled|completed|cancelled|overdue)$";
    public const string ResultPattern = "^(succeeded|partial|failed|not-performed)$";
    public const string UnitPattern = "^(day|week|month|year)$";
    public const string AnchorKindPattern = "^(day-of-month|nth-weekday)$";
    public const string WeekdayPattern = "^(monday|tuesday|wednesday|thursday|friday|saturday|sunday)$";

    /// <summary>A local date, or a local date and time to the minute (seconds optional), without offset.</summary>
    public const string DateTimePattern = @"^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2}(:\d{2})?)?$";

    public const string DatePattern = @"^\d{4}-\d{2}-\d{2}$";

    /// <summary>A generous bound to keep payloads small; the real limits are the business rules of the domain (422).</summary>
    public const int MaxTextBytes = 10_000;

    public const int MaxListItems = 1_000;
}

/// <summary>
/// POST /api/v1/agenda-events and PUT /api/v1/agenda-events/{id} (RF-3). A timed event gives <c>start</c> and <c>end</c> as
/// local date-times (<c>2026-10-24T09:00</c>); an all-day event gives dates (<c>2026-10-26</c>), where <c>end</c> is the last
/// day, inclusive (RN-2, P-24). A missing end takes the default duration of the type (or one day). A missing reminder takes
/// the one of the type; "notify the team" is on by default.
/// </summary>
public sealed record AgendaEventRequest(
    Guid TypeId,
    [StringLength(AgendaContract.MaxTextBytes)] string? Title,
    [StringLength(AgendaContract.MaxTextBytes)] string? Description,
    Guid ResponsibleUserId,
    [MaxLength(AgendaContract.MaxListItems)] string[]? Participants,
    bool? NotifyTeam,
    [RegularExpression(AgendaContract.ImpactPattern)] string? Impact,
    [StringLength(AgendaContract.MaxTextBytes)] string? Scope,
    [StringLength(AgendaContract.MaxTextBytes)] string? Location,
    int? ReminderMinutes,
    [MaxLength(AgendaContract.MaxListItems)] string[]? Tags,
    bool IsAllDay,
    [RegularExpression(AgendaContract.DateTimePattern)] string? Start,
    [RegularExpression(AgendaContract.DateTimePattern)] string? End) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AgendaTimeParsing.TryTiming(IsAllDay, Start, End, out _, out var error))
        {
            yield break;
        }

        yield return new ValidationResult(error, [nameof(Start), nameof(End)]);
    }

    internal EventInput ToInput() =>
        new(
            TypeId,
            Title,
            Description,
            ResponsibleUserId,
            Participants,
            NotifyTeam ?? true,
            AgendaCodes.ParseImpact(Impact) ?? EventImpact.None,
            Scope,
            Location,
            ReminderMinutes,
            Tags,
            AgendaTimeParsing.TryTiming(IsAllDay, Start, End, out var timing, out _) ? timing! : new EventTimingInput(IsAllDay, null, null, null, null));
}

/// <summary>PUT /api/v1/agenda-events/{id}/tags (RF-24): the whole list of tags, replacing the current one.</summary>
public sealed record SetTagsRequest([MaxLength(AgendaContract.MaxListItems)] string[]? Tags);

/// <summary>POST /api/v1/agenda-events/schedule (RF-9): up to 50 drafts at once (CA-7).</summary>
public sealed record ScheduleEventsRequest(
    [Required, MinLength(1), MaxLength(ScheduleEventsCommand.MaxBatch)] Guid[] Ids);

/// <summary>POST /api/v1/agenda-events/{id}/cancel (RF-10). The reason (1 to 500 characters, RN-8) is checked by the domain.</summary>
public sealed record CancelEventRequest([StringLength(AgendaContract.MaxTextBytes)] string? Reason);

/// <summary>An evidence link (RN-10): a label and an http or https URL, checked by the domain.</summary>
public sealed record EvidenceLinkRequest(
    [StringLength(AgendaContract.MaxTextBytes)] string? Label,
    [StringLength(AgendaContract.MaxTextBytes)] string? Url);

/// <summary>PUT /api/v1/agenda-events/{id}/completion (RF-11). <c>executedAt</c> defaults to the moment of the record.</summary>
public sealed record CompleteEventRequest(
    [Required, RegularExpression(AgendaContract.ResultPattern)] string Result,
    DateTimeOffset? ExecutedAt,
    [StringLength(AgendaContract.MaxTextBytes)] string? Notes,
    [MaxLength(AgendaContract.MaxListItems)] EvidenceLinkRequest[]? EvidenceLinks);

/// <summary>The monthly or yearly anchor of a series (RF-4): a day of the month, or the n-th weekday of the month.</summary>
public sealed record SeriesAnchorRequest(
    [Required, RegularExpression(AgendaContract.AnchorKindPattern)] string Kind,
    int? DayOfMonth,
    int? Nth,
    [RegularExpression(AgendaContract.WeekdayPattern)] string? Weekday);

/// <summary>The repetition rule (RF-4): every <c>interval</c> days, weeks, months or years, until a date, a count, or both.</summary>
public sealed record SeriesRuleRequest(
    [Required, RegularExpression(AgendaContract.UnitPattern)] string Unit,
    int Interval,
    SeriesAnchorRequest? Anchor,
    [RegularExpression(AgendaContract.DatePattern)] string? EndsOn,
    int? Count) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndsOn is not null && !DateOnly.TryParseExact(EndsOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            yield return new ValidationResult("La fecha final no es una fecha válida.", [nameof(EndsOn)]);
        }

        if (Anchor is { Kind: "day-of-month", DayOfMonth: null })
        {
            yield return new ValidationResult("El día del mes es obligatorio.", [nameof(Anchor)]);
        }

        if (Anchor is { Kind: "nth-weekday" } nth && (nth.Nth is null || nth.Weekday is null))
        {
            yield return new ValidationResult("El n-ésimo día de la semana necesita su número y su día.", [nameof(Anchor)]);
        }
    }

    internal SeriesRuleInput ToInput()
    {
        var anchor = Anchor is null
            ? null
            : Anchor.Kind == "nth-weekday"
                ? RecurrenceAnchor.OnNthWeekday(Anchor.Nth!.Value, AgendaTimeParsing.Weekday(Anchor.Weekday!))
                : RecurrenceAnchor.OnDay(Anchor.DayOfMonth!.Value);
        return new SeriesRuleInput(
            AgendaCodes.ParseUnit(Unit) ?? RecurrenceUnit.Day,
            Interval,
            anchor,
            EndsOn is null ? null : DateOnly.ParseExact(EndsOn, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Count);
    }
}

/// <summary>POST /api/v1/agenda-series (RF-4): the data of the first occurrence and the rule. With <c>?dryRun=true</c> nothing is saved.</summary>
public sealed record AgendaSeriesRequest([Required] AgendaEventRequest Event, [Required] SeriesRuleRequest Rule);

/// <summary>
/// PUT /api/v1/agenda-series/{id} (RF-6): the common fields, without dates or "notify the team". A missing <c>tags</c> keeps the
/// tags of the occurrences; a list (even an empty one) replaces them.
/// </summary>
public sealed record UpdateSeriesRequest(
    Guid TypeId,
    [StringLength(AgendaContract.MaxTextBytes)] string? Title,
    [StringLength(AgendaContract.MaxTextBytes)] string? Description,
    Guid ResponsibleUserId,
    [MaxLength(AgendaContract.MaxListItems)] string[]? Participants,
    [RegularExpression(AgendaContract.ImpactPattern)] string? Impact,
    [StringLength(AgendaContract.MaxTextBytes)] string? Scope,
    [StringLength(AgendaContract.MaxTextBytes)] string? Location,
    int? ReminderMinutes,
    [MaxLength(AgendaContract.MaxListItems)] string[]? Tags)
{
    internal SeriesCommonInput ToInput() =>
        new(TypeId, Title, Description, ResponsibleUserId, Participants, AgendaCodes.ParseImpact(Impact) ?? EventImpact.None, Scope, Location, ReminderMinutes, Tags);
}

/// <summary>POST and PUT /api/v1/agenda-event-types (RF-1, RF-2). PUT also carries <c>isActive</c> (default true) and <c>If-Match</c>.</summary>
public sealed record EventTypeRequest(
    [StringLength(AgendaContract.MaxTextBytes)] string? Name,
    [StringLength(AgendaContract.MaxTextBytes)] string? Description,
    int DefaultDurationMinutes,
    int DefaultReminderMinutes,
    bool IsFreeze,
    bool? IsActive)
{
    internal EventTypeInput ToInput() => new(Name, Description, DefaultDurationMinutes, DefaultReminderMinutes, IsFreeze);
}

/// <summary>Parsing of the local dates and times of the contract. They carry no offset: they are wall-clock times of the organization zone.</summary>
internal static class AgendaTimeParsing
{
    private static readonly string[] DateTimeFormats = ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss"];

    public static bool TryTiming(bool isAllDay, string? start, string? end, out EventTimingInput? timing, out string error)
    {
        timing = null;
        error = string.Empty;
        if (isAllDay)
        {
            if (!TryDate(start, out var first))
            {
                error = "El primer día debe ser una fecha (aaaa-mm-dd).";
                return false;
            }

            DateOnly? last = null;
            if (end is not null)
            {
                if (!TryDate(end, out var parsedLast))
                {
                    error = "El último día debe ser una fecha (aaaa-mm-dd).";
                    return false;
                }

                last = parsedLast;
            }

            timing = new EventTimingInput(true, null, null, first, last);
            return true;
        }

        if (!TryDateTime(start, out var startLocal))
        {
            error = "El inicio debe ser una fecha y hora local (aaaa-mm-ddThh:mm).";
            return false;
        }

        DateTime? endLocal = null;
        if (end is not null)
        {
            if (!TryDateTime(end, out var parsedEnd))
            {
                error = "El fin debe ser una fecha y hora local (aaaa-mm-ddThh:mm).";
                return false;
            }

            endLocal = parsedEnd;
        }

        timing = new EventTimingInput(false, startLocal, endLocal, null, null);
        return true;
    }

    public static bool TryDate(string? value, out DateOnly date)
    {
        date = default;
        return value is not null && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    public static bool TryDateTime(string? value, out DateTime dateTime)
    {
        dateTime = default;
        return value is not null
            && DateTime.TryParseExact(value, DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime);
    }

    public static DayOfWeek Weekday(string name) => name switch
    {
        "monday" => DayOfWeek.Monday,
        "tuesday" => DayOfWeek.Tuesday,
        "wednesday" => DayOfWeek.Wednesday,
        "thursday" => DayOfWeek.Thursday,
        "friday" => DayOfWeek.Friday,
        "saturday" => DayOfWeek.Saturday,
        _ => DayOfWeek.Sunday,
    };

    public static string WeekdayCode(DayOfWeek day) => day.ToString().ToLowerInvariant();
}
