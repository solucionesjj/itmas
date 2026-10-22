using System.Globalization;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Queries;
using Itmas3.Domain.Agenda;

namespace Itmas3.Api.Contracts.Agenda;

public sealed record AgendaTypeRefResponse(Guid Id, string Name, bool IsFreeze);

public sealed record AgendaPersonResponse(Guid Id, string Name, bool IsActive);

/// <summary>A non-blocking warning (SPEC-0006 RN-11): <c>freeze-overlap</c> or <c>impact-overlap</c>, with the other activity.</summary>
public sealed record AgendaWarningResponse(string Kind, Guid EventId, string Title);

public sealed record OutlookStateResponse(string Status, string? ErrorCode);

/// <summary>
/// An event in a list or calendar (§12.1). <c>start</c> and <c>end</c> are local to the organization zone (RNF-9): date-times for
/// timed events and dates for all-day ones, where <c>end</c> is the last day, inclusive (P-24). The UTC instants are included.
/// </summary>
public sealed record AgendaEventSummaryResponse(
    Guid Id,
    Guid? SeriesId,
    AgendaTypeRefResponse Type,
    string Title,
    string Start,
    string End,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool IsAllDay,
    string Status,
    bool IsOverdue,
    AgendaPersonResponse Responsible,
    string Impact,
    IReadOnlyList<string> Tags,
    bool HasWarnings,
    OutlookSummaryResponse Outlook);

public sealed record OutlookSummaryResponse(string Status);

/// <summary><c>outlookEnabled</c> tells the screens whether scheduling publishes to Outlook (§8.4); the rest of the screens do not need to guess it.</summary>
public sealed record AgendaEventListResponse(IReadOnlyList<AgendaEventSummaryResponse> Items, bool Truncated, string TimeZone, bool OutlookEnabled);

/// <summary>A person that can be the responsible of an activity (P-31): id and name, never an e-mail; <c>isMe</c> marks the caller.</summary>
public sealed record AgendaPersonOptionResponse(Guid Id, string Name, bool IsMe);

public sealed record AgendaPeopleResponse(IReadOnlyList<AgendaPersonOptionResponse> Items);

public sealed record AgendaMonthResponse(int Month, int Draft, int Scheduled, int Completed, int Cancelled, int Overdue);

public sealed record AgendaTypeCountResponse(Guid TypeId, int Count);

public sealed record AgendaSummaryResponse(int Year, IReadOnlyList<AgendaMonthResponse> Months, IReadOnlyList<AgendaTypeCountResponse> ByType);

public sealed record AgendaTagResponse(string Name, int Count);

public sealed record AgendaTagListResponse(IReadOnlyList<AgendaTagResponse> Items);

public sealed record AgendaEventTypeResponse(
    Guid Id,
    string Name,
    string? Description,
    int DefaultDurationMinutes,
    int DefaultReminderMinutes,
    bool IsFreeze,
    bool IsActive,
    int Revision);

public sealed record AgendaEventTypeListResponse(IReadOnlyList<AgendaEventTypeResponse> Items);

public sealed record EvidenceLinkResponse(string Label, string Url);

public sealed record CompletionResponse(
    string Result,
    DateTimeOffset ExecutedAt,
    DateTimeOffset CompletedAt,
    string? CompletedBy,
    string? Notes,
    IReadOnlyList<EvidenceLinkResponse> EvidenceLinks);

public sealed record OutlookDetailResponse(string Status, string? ErrorCode, DateTimeOffset? StatusAt, string? WebLink);

public sealed record SeriesAnchorResponse(string Kind, int? DayOfMonth, int? Nth, string? Weekday);

/// <summary>The rule of the series an occurrence belongs to, only to describe it ("every month, the second Tuesday", RN-12).</summary>
public sealed record SeriesPatternResponse(string Unit, int Interval, SeriesAnchorResponse? Anchor, string StartsOn, string? EndsOn, int? Count);

/// <summary>The full event (RF-14). <c>revision</c> is also the <c>ETag</c>; <c>warnings</c> are calculated at the time of the request.</summary>
public sealed record AgendaEventResponse(
    Guid Id,
    Guid? SeriesId,
    SeriesPatternResponse? Series,
    AgendaTypeRefResponse Type,
    string Title,
    string? Description,
    string Status,
    bool IsOverdue,
    bool IsAllDay,
    string Start,
    string End,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string TimeZone,
    AgendaPersonResponse Responsible,
    IReadOnlyList<string> Participants,
    bool NotifyTeam,
    string Impact,
    string? Scope,
    string? Location,
    int ReminderMinutes,
    IReadOnlyList<string> Tags,
    string? CancelReason,
    CompletionResponse? Completion,
    OutlookDetailResponse Outlook,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    int Revision,
    IReadOnlyList<AgendaWarningResponse> Warnings,
    bool OutlookEnabled);

public sealed record SetTagsResponse(IReadOnlyList<string> Tags);

public sealed record ScheduleEventResultResponse(
    Guid Id,
    string? Status,
    IReadOnlyList<AgendaWarningResponse> Warnings,
    OutlookStateResponse Outlook,
    string? Error);

public sealed record ScheduleEventsResponse(IReadOnlyList<ScheduleEventResultResponse> Results);

public sealed record OutlookSyncResponse(OutlookStateResponse Outlook);

public sealed record OutlookSyncResultResponse(Guid Id, OutlookStateResponse Outlook);

public sealed record OutlookSyncAllResponse(IReadOnlyList<OutlookSyncResultResponse> Results);

public sealed record SeriesOccurrenceResponse(string Start, string End, bool IsAllDay, IReadOnlyList<AgendaWarningResponse> Warnings);

public sealed record SeriesPreviewResponse(IReadOnlyList<SeriesOccurrenceResponse> Occurrences);

public sealed record SeriesCreatedResponse(Guid SeriesId, IReadOnlyList<Guid> EventIds);

public sealed record SeriesUpdatedResponse(int Updated);

/// <summary>Conversion of the application models to the contract: wire codes and the date format (P-24).</summary>
internal static class AgendaMapping
{
    public static string Local(DateTime local) => local.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    public static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Start as the contract shows it: a date for an all-day event, a local date-time otherwise.</summary>
    public static string Start(bool isAllDay, DateTime startLocal) => isAllDay ? Day(DateOnly.FromDateTime(startLocal)) : Local(startLocal);

    /// <summary>End as the contract shows it: the last day (inclusive) for an all-day event, a local date-time otherwise (P-24).</summary>
    public static string End(bool isAllDay, DateTime endLocal) =>
        isAllDay ? Day(DateOnly.FromDateTime(endLocal.AddDays(-1))) : Local(endLocal);

    public static AgendaWarningResponse Of(AgendaWarning warning) => new(AgendaCodes.ToCode(warning.Kind), warning.EventId, warning.EventTitle);

    public static IReadOnlyList<AgendaWarningResponse> Of(IEnumerable<AgendaWarning> warnings) => [.. warnings.Select(Of)];

    public static OutlookStateResponse Of(OutlookState state) => new(AgendaCodes.ToCode(state.Status), state.ErrorCode);

    public static AgendaEventTypeResponse Of(AgendaEventTypeView type) =>
        new(type.Id, type.Name, type.Description, type.DefaultDurationMinutes, type.DefaultReminderMinutes, type.IsFreeze, type.IsActive, type.Revision);

    public static AgendaEventSummaryResponse Of(AgendaEventSummary item) =>
        new(
            item.Id,
            item.SeriesId,
            new AgendaTypeRefResponse(item.Type.Id, item.Type.Name, item.Type.IsFreeze),
            item.Title,
            Start(item.IsAllDay, item.StartLocal),
            End(item.IsAllDay, item.EndLocal),
            item.StartUtc,
            item.EndUtc,
            item.IsAllDay,
            AgendaCodes.ToCode(item.Status),
            item.IsOverdue,
            new AgendaPersonResponse(item.Responsible.Id, item.Responsible.Name, item.Responsible.IsActive),
            AgendaCodes.ToCode(item.Impact),
            item.Tags,
            item.HasWarnings,
            new OutlookSummaryResponse(AgendaCodes.ToCode(item.OutlookStatus)));

    public static AgendaEventResponse Of(AgendaEventDetail detail) =>
        new(
            detail.Id,
            detail.SeriesId,
            detail.SeriesPattern is { } pattern ? Of(pattern) : null,
            new AgendaTypeRefResponse(detail.Type.Id, detail.Type.Name, detail.Type.IsFreeze),
            detail.Title,
            detail.Description,
            AgendaCodes.ToCode(detail.Status),
            detail.IsOverdue,
            detail.IsAllDay,
            Start(detail.IsAllDay, detail.StartLocal),
            End(detail.IsAllDay, detail.EndLocal),
            detail.StartUtc,
            detail.EndUtc,
            detail.TimeZone,
            new AgendaPersonResponse(detail.Responsible.Id, detail.Responsible.Name, detail.Responsible.IsActive),
            detail.Participants,
            detail.NotifyTeam,
            AgendaCodes.ToCode(detail.Impact),
            detail.Scope,
            detail.Location,
            detail.ReminderMinutes,
            detail.Tags,
            detail.CancelReason,
            detail.Completion is { } completion
                ? new CompletionResponse(
                    AgendaCodes.ToCode(completion.Result),
                    completion.ExecutedAt,
                    completion.CompletedAt,
                    completion.CompletedByName,
                    completion.Notes,
                    [.. completion.EvidenceLinks.Select(link => new EvidenceLinkResponse(link.Label, link.Url))])
                : null,
            new OutlookDetailResponse(AgendaCodes.ToCode(detail.Outlook.Status), detail.Outlook.ErrorCode, detail.Outlook.StatusAt, detail.Outlook.WebLink),
            detail.CreatedAt,
            detail.CreatedByName,
            detail.UpdatedAt,
            detail.UpdatedByName,
            detail.Revision,
            Of(detail.Warnings),
            detail.OutlookEnabled);

    public static SeriesPatternResponse Of(RecurrencePattern pattern) =>
        new(
            AgendaCodes.ToCode(pattern.Unit),
            pattern.Interval,
            pattern.Anchor is { } anchor
                ? new SeriesAnchorResponse(
                    AgendaCodes.ToCode(anchor.Kind),
                    anchor.Kind == RecurrenceAnchorKind.DayOfMonth ? anchor.DayOfMonth : null,
                    anchor.Kind == RecurrenceAnchorKind.NthWeekday ? anchor.Nth : null,
                    anchor.Kind == RecurrenceAnchorKind.NthWeekday ? AgendaTimeParsing.WeekdayCode(anchor.Weekday) : null)
                : null,
            Day(pattern.StartsOn),
            pattern.EndsOn is { } endsOn ? Day(endsOn) : null,
            pattern.Count);

    public static ScheduleEventResultResponse Of(ScheduleEventResult result) =>
        new(result.Id, result.Status is { } status ? AgendaCodes.ToCode(status) : null, Of(result.Warnings), Of(result.Outlook), result.Error);
}
