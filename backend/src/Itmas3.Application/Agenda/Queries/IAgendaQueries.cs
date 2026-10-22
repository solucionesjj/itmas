using Itmas3.Domain.Agenda;

namespace Itmas3.Application.Agenda.Queries;

/// <summary>
/// Read side of the agenda (SPEC-0006 RF-12 to RF-14, RF-26): read-only projections with no domain rules, ready for the
/// views. The names of the responsible and of the people who created or changed an event are resolved at query time.
/// Local date-times are those of the time zone of the organization (RNF-9).
/// </summary>
public interface IAgendaQueries
{
    /// <summary>
    /// Events that share time with the local dates <paramref name="from"/> (inclusive) to <paramref name="toExclusive"/>, in
    /// chronological order, at most <see cref="AgendaLimits.MaxResults"/>; the list says when more existed.
    /// </summary>
    Task<AgendaEventList> ListEventsAsync(DateOnly from, DateOnly toExclusive, AgendaFilter filter, CancellationToken cancellationToken);

    /// <summary>Per-month counts by status, and the total by type, of the events that start in <paramref name="year"/> (RF-13).</summary>
    Task<AgendaSummary> GetSummaryAsync(int year, AgendaFilter filter, CancellationToken cancellationToken);

    Task<AgendaEventDetail?> GetEventAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Existing tags with the number of events that have them (RF-23, RF-26), by count and then name.</summary>
    Task<IReadOnlyList<AgendaTagCount>> ListTagsAsync(string? search, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgendaEventTypeView>> ListEventTypesAsync(CancellationToken cancellationToken);

    Task<AgendaEventTypeView?> GetEventTypeAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The data the calendar export needs (RF-21) of the events that share time with the local dates <paramref name="from"/>
    /// (inclusive) to <paramref name="toExclusive"/> and pass <paramref name="filter"/>, in chronological order, at most
    /// <see cref="AgendaLimits.MaxResults"/>; the list says when more existed. It carries no people: not even the responsible.
    /// </summary>
    Task<AgendaExportList> ListExportEventsAsync(DateOnly from, DateOnly toExclusive, AgendaFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// Active ITMas users that can be the responsible of an activity, by name (P-31). Without a search the caller
    /// (<paramref name="meId"/>) is always included, so the form can propose them by default.
    /// </summary>
    Task<IReadOnlyList<AgendaPersonOption>> SearchPeopleAsync(string? search, int limit, Guid? meId, CancellationToken cancellationToken);
}

public static class AgendaLimits
{
    /// <summary>Most events a list returns (SPEC-0006 §12.1, §8.4).</summary>
    public const int MaxResults = 1000;

    /// <summary>Longest date range a list admits, in days (§12.1).</summary>
    public const int MaxRangeDays = 366;

    /// <summary>Most suggestions the tag list returns (§12.1).</summary>
    public const int MaxTagSuggestions = 50;

    /// <summary>Most people the responsible selector returns (P-31).</summary>
    public const int MaxPeople = 50;
}

/// <summary>A person that can be chosen as the responsible of an activity: id and name, never an e-mail address (P-31).</summary>
public sealed record AgendaPersonOption(Guid Id, string Name);

/// <summary>Status filter of the views: the four states plus "overdue", which is calculated (RF-8, RF-12).</summary>
public enum AgendaStatusFilter
{
    Draft,
    Scheduled,
    Completed,
    Cancelled,
    Overdue,
}

/// <summary>
/// Filters shared by every view (RF-12). <see cref="Tags"/> require the event to have <b>all</b> of them (RF-25).
/// <see cref="MineUserId"/> is the signed-in user when "My activities" is on. <see cref="Search"/> looks in titles and tags (P-18).
/// </summary>
public sealed record AgendaFilter(
    Guid? TypeId,
    AgendaStatusFilter? Status,
    Guid? ResponsibleId,
    Guid? MineUserId,
    IReadOnlyList<string> Tags,
    string? Search)
{
    public static AgendaFilter None { get; } = new(null, null, null, null, [], null);
}

public sealed record AgendaTypeRef(Guid Id, string Name, bool IsFreeze);

public sealed record AgendaPersonRef(Guid Id, string Name, bool IsActive);

/// <summary>
/// An event in a list or calendar (SPEC-0006 §12.1). For all-day events <see cref="EndLocal"/> is the exclusive end; the API
/// contract turns it into the inclusive last day (P-24).
/// </summary>
public sealed record AgendaEventSummary(
    Guid Id,
    Guid? SeriesId,
    AgendaTypeRef Type,
    string Title,
    DateTime StartLocal,
    DateTime EndLocal,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool IsAllDay,
    EventStatus Status,
    bool IsOverdue,
    AgendaPersonRef Responsible,
    EventImpact Impact,
    IReadOnlyList<string> Tags,
    bool HasWarnings,
    OutlookStatus OutlookStatus);

/// <summary>A list of events. <see cref="OutlookEnabled"/> tells the screens whether scheduling publishes to Outlook (§8.4).</summary>
public sealed record AgendaEventList(IReadOnlyList<AgendaEventSummary> Items, bool Truncated, string TimeZone, bool OutlookEnabled);

/// <summary>
/// An event as the calendar export writes it (RF-21). For all-day events <see cref="EndLocal"/> is the exclusive end (P-24), which
/// is what iCalendar expects. <see cref="LastModifiedUtc"/> is the last change of the event (or its creation) and
/// <see cref="Revision"/> its ETag, which the export turns into <c>SEQUENCE</c>.
/// </summary>
public sealed record AgendaExportEvent(
    Guid Id,
    string TypeName,
    string Title,
    string? Description,
    string? Scope,
    string? Location,
    EventImpact Impact,
    bool IsAllDay,
    DateTime StartLocal,
    DateTime EndLocal,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    IReadOnlyList<string> Tags,
    DateTimeOffset LastModifiedUtc,
    int Revision);

/// <summary>Events to export; <see cref="Truncated"/> says that more than <see cref="AgendaLimits.MaxResults"/> matched.</summary>
public sealed record AgendaExportList(IReadOnlyList<AgendaExportEvent> Items, bool Truncated);

public sealed record AgendaMonthSummary(int Month, int Draft, int Scheduled, int Completed, int Cancelled, int Overdue);

public sealed record AgendaTypeCount(Guid TypeId, int Count);

public sealed record AgendaSummary(int Year, IReadOnlyList<AgendaMonthSummary> Months, IReadOnlyList<AgendaTypeCount> ByType);

public sealed record AgendaTagCount(string Name, int Count);

public sealed record AgendaEventTypeView(
    Guid Id,
    string Name,
    string? Description,
    int DefaultDurationMinutes,
    int DefaultReminderMinutes,
    bool IsFreeze,
    bool IsActive,
    int Revision);

public sealed record AgendaEvidenceLinkView(string Label, string Url);

/// <summary>The execution record of a Completed event (RF-11).</summary>
public sealed record AgendaCompletionView(
    CompletionResult Result,
    DateTimeOffset ExecutedAt,
    DateTimeOffset CompletedAt,
    string? CompletedByName,
    string? Notes,
    IReadOnlyList<AgendaEvidenceLinkView> EvidenceLinks);

/// <summary>The Outlook publication of an event (RF-14, RF-18): the normalized code and the link to open it, never the raw detail.</summary>
public sealed record AgendaOutlookView(OutlookStatus Status, string? ErrorCode, DateTimeOffset? StatusAt, string? WebLink);

/// <summary>The full event with its warnings (SPEC-0006 RF-14). The revision is the ETag.</summary>
public sealed record AgendaEventDetail(
    Guid Id,
    Guid? SeriesId,
    RecurrencePattern? SeriesPattern,
    AgendaTypeRef Type,
    string Title,
    string? Description,
    EventStatus Status,
    bool IsOverdue,
    bool IsAllDay,
    DateTime StartLocal,
    DateTime EndLocal,
    string TimeZone,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    AgendaPersonRef Responsible,
    IReadOnlyList<string> Participants,
    bool NotifyTeam,
    EventImpact Impact,
    string? Scope,
    string? Location,
    int ReminderMinutes,
    IReadOnlyList<string> Tags,
    string? CancelReason,
    AgendaCompletionView? Completion,
    AgendaOutlookView Outlook,
    DateTimeOffset CreatedAt,
    string? CreatedByName,
    DateTimeOffset? UpdatedAt,
    string? UpdatedByName,
    int Revision,
    IReadOnlyList<AgendaWarning> Warnings,
    bool OutlookEnabled);
