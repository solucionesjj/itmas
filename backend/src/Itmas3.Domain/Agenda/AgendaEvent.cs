using Itmas3.Domain.Common;

namespace Itmas3.Domain.Agenda;

/// <summary>
/// An activity of the infrastructure agenda (SPEC-0006): a one-off event or one occurrence of a series (RN-12). It moves
/// Draft → Scheduled → Completed or Cancelled (RN-7). ITMas is the source of truth: the Outlook event is only a notice
/// channel (RN-14), and its publication state is kept apart from the lifecycle (RF-20).
/// </summary>
public sealed class AgendaEvent : Entity<Guid>
{
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 4000;
    public const int MaxScopeLength = 500;
    public const int MaxLocationLength = 200;
    public const int MaxCancelReasonLength = 500;
    public const int MaxNotesLength = 4000;

    /// <summary>Reminder before the start: from 0 to 4 weeks, in minutes (P-25).</summary>
    public const int MaxReminderMinutes = 40_320;

    private readonly List<AgendaTag> _tags = [];

    private AgendaEvent(Guid id, Guid typeId, string title, Guid responsibleUserId, DateTimeOffset createdAt, Guid createdBy)
        : base(id)
    {
        TypeId = typeId;
        Title = title;
        ResponsibleUserId = responsibleUserId;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        Status = EventStatus.Draft;
        Revision = 1;
        TimeZoneId = string.Empty;
    }

    public Guid? SeriesId { get; private set; }

    public Guid TypeId { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public EventStatus Status { get; private set; }

    public bool IsAllDay { get; private set; }

    /// <summary>Start in the local time of <see cref="TimeZoneId"/>. For all-day events, 00:00 of the first day.</summary>
    public DateTime StartsAtLocal { get; private set; }

    /// <summary>End in local time, exclusive. For all-day events, 00:00 of the day after the last one (P-24).</summary>
    public DateTime EndsAtLocal { get; private set; }

    public string TimeZoneId { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    public DateTimeOffset EndsAtUtc { get; private set; }

    public Guid ResponsibleUserId { get; private set; }

    public IReadOnlyList<string> Participants { get; private set; } = [];

    public bool NotifyTeam { get; private set; }

    public EventImpact Impact { get; private set; }

    public string? Scope { get; private set; }

    public string? Location { get; private set; }

    public int ReminderMinutes { get; private set; }

    public string? CancelReason { get; private set; }

    public CompletionResult? CompletionResult { get; private set; }

    public DateTimeOffset? ExecutedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public Guid? CompletedBy { get; private set; }

    public string? CompletionNotes { get; private set; }

    public IReadOnlyList<EvidenceLink> EvidenceLinks { get; private set; } = [];

    public OutlookStatus OutlookStatus { get; private set; }

    /// <summary>Id of the event in the shared Outlook calendar; kept once published, even after it is cancelled there.</summary>
    public string? OutlookEventId { get; private set; }

    public string? OutlookWebLink { get; private set; }

    /// <summary>The <see cref="Revision"/> that Outlook last confirmed; when it is lower, Outlook is behind.</summary>
    public int? OutlookSyncedRevision { get; private set; }

    public string? OutlookErrorCode { get; private set; }

    /// <summary>When the last operation with Outlook ended, confirmed or failed (RF-18).</summary>
    public DateTimeOffset? OutlookStatusAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    /// <summary>
    /// Incremented by the changes of RF-5, RF-6, RF-9, RF-10 and RF-11; used for optimistic concurrency (ETag). Editing the
    /// tags (RF-24) or recording the answer of Outlook does not change it.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>Tags of the event. They are replaced apart from the rest of the data (<see cref="ReplaceTags"/>).</summary>
    public IReadOnlyCollection<AgendaTag> Tags => _tags;

    /// <summary>The schedule as an <see cref="EventSchedule"/> value.</summary>
    public EventSchedule Timing => new(StartsAtLocal, EndsAtLocal, StartsAtUtc, EndsAtUtc, TimeZoneId, IsAllDay);

    /// <summary>Overdue is not a state: a Draft or Scheduled event whose end has passed without being closed (RF-8).</summary>
    public bool IsOverdue(DateTimeOffset now) => Status is EventStatus.Draft or EventStatus.Scheduled && EndsAtUtc < now;

    /// <summary>Whether Outlook still has to be told about this event: scheduled and not confirmed at the current revision (RF-19).</summary>
    public bool NeedsOutlookSync => Status == EventStatus.Scheduled && !OutlookIsCurrent;

    /// <summary>Whether Outlook confirmed the current revision (a cancellation counts for a cancelled event).</summary>
    public bool OutlookIsCurrent => OutlookStatus == OutlookStatus.Published && OutlookSyncedRevision == Revision;

    /// <summary>A cancelled event that was published and whose cancellation Outlook has not confirmed yet (RF-10, RF-19).</summary>
    public bool NeedsOutlookCancellation => Status == EventStatus.Cancelled && OutlookEventId is not null && !OutlookIsCurrent;

    /// <summary>Creates an event in Draft state (RF-3, RN-7). The caller checks RN-16 (start not in the past) and that the type and responsible are valid.</summary>
    public static AgendaEvent Create(EventDetails details, EventSchedule schedule, DateTimeOffset now, Guid createdBy, Guid? seriesId = null)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(schedule);
        var agendaEvent = new AgendaEvent(Guid.CreateVersion7(now), details.TypeId, details.Title, details.ResponsibleUserId, now, createdBy)
        {
            SeriesId = seriesId,
        };
        agendaEvent.ApplyDetails(details);
        agendaEvent.ApplySchedule(schedule);
        return agendaEvent;
    }

    /// <summary>
    /// Changes the data and the schedule of a Draft or Scheduled event (RF-5). A Scheduled event cannot have its start moved
    /// into the past (RN-3). A Completed event only admits notes and links (<see cref="UpdateCompletion"/>) and a Cancelled
    /// one admits nothing (RN-9).
    /// </summary>
    public void Update(EventDetails details, EventSchedule schedule, DateTimeOffset now, TimeZoneInfo zone, Guid updatedBy)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(schedule);
        EnsureEditable();
        if (Status == EventStatus.Scheduled && schedule.StartUtc != StartsAtUtc && schedule.IsInPast(now, zone))
        {
            throw new DomainException("No se puede mover al pasado el inicio de una actividad programada.");
        }

        ApplyDetails(details);
        ApplySchedule(schedule);
        Touch(now, updatedBy);
    }

    /// <summary>
    /// Applies the common fields of a series (RF-6): everything in <paramref name="details"/> except "notify the team".
    /// Only events in Draft or Scheduled state that have not started change; the dates never do. Returns whether it applied.
    /// </summary>
    public bool TryApplyCommonFields(EventDetails details, DateTimeOffset now, Guid updatedBy)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (Status is not (EventStatus.Draft or EventStatus.Scheduled) || StartsAtUtc <= now)
        {
            return false;
        }

        var notifyTeam = NotifyTeam;
        ApplyDetails(details);
        NotifyTeam = notifyTeam;
        Touch(now, updatedBy);
        return true;
    }

    /// <summary>Draft → Scheduled (RF-9). The start cannot have passed (RN-3).</summary>
    public void Schedule(DateTimeOffset now, TimeZoneInfo zone, Guid scheduledBy)
    {
        if (Status != EventStatus.Draft)
        {
            throw new DomainException("Solo se puede programar una actividad en Borrador.");
        }

        if (Timing.IsInPast(now, zone))
        {
            throw new DomainException("No se puede programar una actividad cuyo inicio ya pasó.");
        }

        Status = EventStatus.Scheduled;
        Touch(now, scheduledBy);
    }

    /// <summary>Scheduled → Cancelled, with a reason of 1 to 500 characters (RF-10, RN-8).</summary>
    public void Cancel(string? reason, DateTimeOffset now, Guid cancelledBy)
    {
        if (Status != EventStatus.Scheduled)
        {
            throw new DomainException("Solo se puede cancelar una actividad programada.");
        }

        CancelReason = AgendaText.RequiredLine(reason, MaxCancelReasonLength, "El motivo de la cancelación");
        Status = EventStatus.Cancelled;
        Touch(now, cancelledBy);
    }

    /// <summary>
    /// Scheduled → Completed with the execution record (RF-11). It is possible only from the scheduled start (RN-8); the
    /// execution time defaults to the moment of the record.
    /// </summary>
    public void Complete(
        CompletionResult result,
        DateTimeOffset? executedAt,
        string? notes,
        IEnumerable<(string? Label, string? Url)>? evidenceLinks,
        DateTimeOffset now,
        Guid completedBy)
    {
        if (Status != EventStatus.Scheduled)
        {
            throw new DomainException("Solo se puede registrar la ejecución de una actividad programada.");
        }

        if (now < StartsAtUtc)
        {
            throw new DomainException("No se puede registrar la ejecución antes del inicio programado. Si se ejecutó antes, reprograma la actividad primero.");
        }

        if (!Enum.IsDefined(result))
        {
            throw new DomainException("El resultado de la ejecución no es válido.");
        }

        CompletionNotes = AgendaText.OptionalText(notes, MaxNotesLength, "Las notas");
        EvidenceLinks = EvidenceLink.CreateSet(evidenceLinks);
        CompletionResult = result;
        // Instants are stored in UTC whatever offset the caller wrote (timestamptz only admits offset 0).
        ExecutedAt = (executedAt ?? now).ToUniversalTime();
        CompletedAt = now.ToUniversalTime();
        CompletedBy = completedBy;
        Status = EventStatus.Completed;
        Touch(now, completedBy);
    }

    /// <summary>The only change a Completed event admits: its notes and evidence links (RN-9).</summary>
    public void UpdateCompletion(string? notes, IEnumerable<(string? Label, string? Url)>? evidenceLinks, DateTimeOffset now, Guid updatedBy)
    {
        if (Status != EventStatus.Completed)
        {
            throw new DomainException("Solo se pueden modificar las notas y los enlaces de una actividad completada.");
        }

        CompletionNotes = AgendaText.OptionalText(notes, MaxNotesLength, "Las notas");
        EvidenceLinks = EvidenceLink.CreateSet(evidenceLinks);
        Touch(now, updatedBy);
    }

    /// <summary>Only Draft events can be deleted (RF-7, RN-13): Scheduled ones are cancelled and the rest are kept.</summary>
    public void EnsureCanBeDeleted()
    {
        if (Status != EventStatus.Draft)
        {
            throw new DomainException("Solo se pueden eliminar actividades en Borrador. Las programadas se cancelan y las cerradas se conservan.");
        }
    }

    /// <summary>
    /// Replaces the tags (RF-24): any status, without changing <see cref="Revision"/>. The caller resolves the tags by name
    /// (<see cref="TagName"/>) and creates the missing ones.
    /// </summary>
    public void ReplaceTags(IReadOnlyCollection<AgendaTag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (tags.Select(tag => tag.Id).Distinct().Count() != tags.Count)
        {
            throw new DomainException("Hay etiquetas repetidas.");
        }

        if (tags.Count > TagName.MaxPerEvent)
        {
            throw new DomainException($"Máximo {TagName.MaxPerEvent} etiquetas por actividad.");
        }

        _tags.RemoveAll(current => tags.All(tag => tag.Id != current.Id));
        foreach (var tag in tags.Where(tag => _tags.All(current => current.Id != tag.Id)))
        {
            _tags.Add(tag);
        }
    }

    /// <summary>Outlook has to be told about the latest change (RF-18 "Pendiente"). Does not change <see cref="Revision"/>.</summary>
    public void MarkOutlookPending()
    {
        OutlookStatus = OutlookStatus.Pending;
        OutlookErrorCode = null;
    }

    /// <summary>Outlook is not in use for this event (RF-18 "No publicado").</summary>
    public void MarkOutlookNotPublished()
    {
        OutlookStatus = OutlookStatus.NotPublished;
        OutlookErrorCode = null;
        OutlookStatusAt = null;
    }

    /// <summary>
    /// Outlook confirmed the creation, the update or the cancellation of <paramref name="syncedRevision"/> (RF-18
    /// "Publicado"). When the event changed again while Outlook was being called, it stays "Pendiente" until the newer
    /// revision is sent.
    /// </summary>
    public void MarkOutlookPublished(string outlookEventId, string? webLink, int syncedRevision, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(outlookEventId))
        {
            throw new ArgumentException("The Outlook event id is required.", nameof(outlookEventId));
        }

        OutlookEventId = outlookEventId;
        OutlookWebLink = webLink;
        OutlookStatus = syncedRevision >= Revision ? OutlookStatus.Published : OutlookStatus.Pending;
        OutlookSyncedRevision = syncedRevision;
        OutlookErrorCode = null;
        OutlookStatusAt = now;
    }

    /// <summary>
    /// The last operation with Outlook failed (RF-18 "Error"): only a normalized code is kept, never the raw detail (RNF-8).
    /// </summary>
    public void MarkOutlookFailed(string errorCode, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            throw new ArgumentException("The error code is required.", nameof(errorCode));
        }

        OutlookStatus = OutlookStatus.Error;
        OutlookErrorCode = errorCode;
        OutlookStatusAt = now;
    }

    private void EnsureEditable()
    {
        switch (Status)
        {
            case EventStatus.Completed:
                throw new DomainException("Una actividad completada solo admite cambios en sus notas y enlaces de evidencia.");
            case EventStatus.Cancelled:
                throw new DomainException("Una actividad cancelada no se puede modificar.");
        }
    }

    private void ApplyDetails(EventDetails details)
    {
        TypeId = details.TypeId;
        Title = details.Title;
        Description = details.Description;
        ResponsibleUserId = details.ResponsibleUserId;
        Participants = details.Participants;
        NotifyTeam = details.NotifyTeam;
        Impact = details.Impact;
        Scope = details.Scope;
        Location = details.Location;
        ReminderMinutes = details.ReminderMinutes;
    }

    private void ApplySchedule(EventSchedule schedule)
    {
        IsAllDay = schedule.IsAllDay;
        StartsAtLocal = schedule.StartLocal;
        EndsAtLocal = schedule.EndLocal;
        TimeZoneId = schedule.TimeZoneId;
        StartsAtUtc = schedule.StartUtc;
        EndsAtUtc = schedule.EndUtc;
    }

    private void Touch(DateTimeOffset now, Guid actor)
    {
        UpdatedAt = now;
        UpdatedBy = actor;
        Revision++;
    }
}
