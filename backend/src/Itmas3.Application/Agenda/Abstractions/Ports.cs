using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Domain.Agenda;

namespace Itmas3.Application.Agenda.Abstractions;

/// <summary>An ITMas user as the agenda needs it: the responsible of an event (RN-4) and an invitee of its Outlook event.</summary>
public sealed record AgendaUser(Guid Id, string DisplayName, string? Email, bool IsActive);

/// <summary>Reads the ITMas users. The agenda domain only keeps the user id and does not depend on the authorization domain (§13).</summary>
public interface IAgendaDirectory
{
    Task<AgendaUser?> GetUserAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, AgendaUser>> GetUsersAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}

public interface IEventTypeRepository
{
    Task<EventType?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, EventType>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Whether another type (not <paramref name="excludeId"/>) has this name, ignoring case (RF-1).</summary>
    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken);

    void Add(EventType type);
}

public interface IAgendaEventRepository
{
    /// <summary>The event with its tags, or null.</summary>
    Task<AgendaEvent?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgendaEvent>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>The occurrences of a series, oldest first, with their tags.</summary>
    Task<IReadOnlyList<AgendaEvent>> GetBySeriesAsync(Guid seriesId, CancellationToken cancellationToken);

    /// <summary>
    /// Scheduled events that have not started and that Outlook has not confirmed at their current revision: Pending, Error or
    /// Not published (RF-19). Soonest first, at most <paramref name="limit"/>.
    /// </summary>
    Task<IReadOnlyList<AgendaEvent>> FindNeedingOutlookSyncAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Draft and Scheduled events that share time with the interval and can cause a warning (RN-11): scheduled freezes and
    /// events with an impact, with whether their type is a freeze. The others cannot cause one, so they need not be returned.
    /// </summary>
    Task<IReadOnlyList<OverlapCandidate>> FindOverlapCandidatesAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken);

    void Add(AgendaEvent agendaEvent);

    void AddRange(IEnumerable<AgendaEvent> agendaEvents);

    /// <summary>Removes a Draft event (RN-13). Its tag links go with it.</summary>
    void Remove(AgendaEvent agendaEvent);
}

public interface IEventSeriesRepository
{
    Task<EventSeries?> GetAsync(Guid id, CancellationToken cancellationToken);

    void Add(EventSeries series);
}

public interface IAgendaTagRepository
{
    /// <summary>The existing tags whose <see cref="TagName.Key"/> is one of <paramref name="keys"/>.</summary>
    Task<IReadOnlyList<AgendaTag>> FindByKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken);

    void Add(AgendaTag tag);
}

/// <summary>
/// Executes an agenda operation atomically: persists the changes and the audit entries recorded in the trail (shared audit
/// log of SPEC-0001) in a single transaction. The actor is the trail's, or else the current user, or else the system.
/// Concurrent changes (<c>xmin</c>) and unique-index races are reported as <c>ConflictException</c> (409).
/// </summary>
public interface IAgendaUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<AuditTrail, CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}

/// <summary>
/// The shared budget of the operations that notify people through Outlook (SPEC-0006 §14.1, P-26): scheduling, cancelling,
/// synchronizing and modifying what is already scheduled. One counter for the whole API process, taken only once the caller has
/// been authorized, so that requests that are going to be refused do not spend it.
/// </summary>
public interface IAgendaPublishLimit
{
    /// <summary>Takes one permit. False when the budget of the current window is spent; then <paramref name="retryAfter"/> may say when.</summary>
    bool TryAcquire(out TimeSpan? retryAfter);
}
