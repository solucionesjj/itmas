using Itmas3.Domain.Agenda;

namespace Itmas3.Application.Agenda;

/// <summary>State of the Outlook publication of an event as returned to the caller (SPEC-0006 RF-18).</summary>
public sealed record OutlookState(OutlookStatus Status, string? ErrorCode = null)
{
    public static OutlookState Of(AgendaEvent agendaEvent) => new(agendaEvent.OutlookStatus, agendaEvent.OutlookErrorCode);
}

/// <summary>Result of an operation on one event: its new revision (for the ETag), the warnings of RN-11 and the Outlook state.</summary>
public sealed record AgendaCommandResult(
    Guid Id,
    int Revision,
    EventStatus Status,
    IReadOnlyList<AgendaWarning> Warnings,
    OutlookState Outlook);

/// <summary>Individual result of scheduling an event in a batch (RF-9, CA-7). <see cref="Error"/> is set when it was rejected.</summary>
public sealed record ScheduleEventResult(
    Guid Id,
    EventStatus? Status,
    IReadOnlyList<AgendaWarning> Warnings,
    OutlookState Outlook,
    string? Error);

/// <summary>An occurrence a series would generate, with its warnings (RF-4 preview).</summary>
public sealed record SeriesOccurrencePreview(EventSchedule Schedule, IReadOnlyList<AgendaWarning> Warnings);

/// <summary>Result of creating a series: the occurrences and, unless it was a preview, the ids of what was saved.</summary>
public sealed record CreateSeriesResult(IReadOnlyList<SeriesOccurrencePreview> Occurrences, Guid? SeriesId, IReadOnlyList<Guid> EventIds);

/// <summary>Result of changing the common fields of a series (RF-6).</summary>
public sealed record UpdateSeriesResult(int Updated, IReadOnlyList<Guid> EventIds);

/// <summary>Result of a "sync again" of one event (RF-19) or of a batch.</summary>
public sealed record SyncResult(Guid Id, OutlookState Outlook);
