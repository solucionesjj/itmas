namespace Itmas3.Domain.Agenda;

/// <summary>Lifecycle of an agenda event (SPEC-0006 RF-8, RN-7). "Overdue" is not a state: see <see cref="AgendaEvent.IsOverdue"/>.</summary>
public enum EventStatus
{
    /// <summary>Exists only in ITMas: nobody has been notified.</summary>
    Draft,

    /// <summary>Confirmed. When Outlook is active the event is published and the team has been invited.</summary>
    Scheduled,

    /// <summary>Executed (or closed as not performed), with its execution record.</summary>
    Completed,

    /// <summary>Discarded before execution, with a reason. Terminal.</summary>
    Cancelled,
}

/// <summary>Effect an activity is expected to have on the services (SPEC-0006 RF-3).</summary>
public enum EventImpact
{
    None,
    Degradation,
    Outage,
}

/// <summary>Outcome recorded when an activity is closed (SPEC-0006 RF-11).</summary>
public enum CompletionResult
{
    Succeeded,
    Partial,
    Failed,
    NotPerformed,
}

/// <summary>State of the publication of an event in the shared Outlook calendar (SPEC-0006 RF-18).</summary>
public enum OutlookStatus
{
    NotPublished,
    Pending,
    Published,
    Error,
}

/// <summary>Unit of the repetition rule of a series (SPEC-0006 RF-4).</summary>
public enum RecurrenceUnit
{
    Day,
    Week,
    Month,
    Year,
}

/// <summary>How a monthly or yearly series picks its day (SPEC-0006 RF-4).</summary>
public enum RecurrenceAnchorKind
{
    /// <summary>A day number of the month (30 and 31 fall on the last day of shorter months, RN-6).</summary>
    DayOfMonth,

    /// <summary>The n-th weekday of the month, e.g. the second Tuesday.</summary>
    NthWeekday,
}

/// <summary>Kind of non-blocking warning returned with an event (SPEC-0006 RN-11).</summary>
public enum AgendaWarningKind
{
    /// <summary>The activity overlaps a scheduled change freeze.</summary>
    FreezeOverlap,

    /// <summary>The activity has an impact and overlaps another activity that also has one.</summary>
    ImpactOverlap,
}
