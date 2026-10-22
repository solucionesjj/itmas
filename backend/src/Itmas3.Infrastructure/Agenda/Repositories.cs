using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Domain.Agenda;
using Itmas3.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itmas3.Infrastructure.Agenda;

internal sealed class EventTypeRepository(AppDbContext db) : IEventTypeRepository
{
    public Task<EventType?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.AgendaEventTypes.SingleOrDefaultAsync(type => type.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, EventType>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.ToArray();
        return await db.AgendaEventTypes.Where(type => wanted.Contains(type.Id)).ToDictionaryAsync(type => type.Id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var lowered = name.ToLowerInvariant();
        return db.AgendaEventTypes.AnyAsync(type => type.Name.ToLower() == lowered && type.Id != excludeId, cancellationToken);
    }

    public void Add(EventType type) => db.AgendaEventTypes.Add(type);
}

internal sealed class AgendaEventRepository(AppDbContext db) : IAgendaEventRepository
{
    public Task<AgendaEvent?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.AgendaEvents.Include(agendaEvent => agendaEvent.Tags).SingleOrDefaultAsync(agendaEvent => agendaEvent.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AgendaEvent>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.ToArray();
        return await db.AgendaEvents.Include(agendaEvent => agendaEvent.Tags)
            .Where(agendaEvent => wanted.Contains(agendaEvent.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgendaEvent>> GetBySeriesAsync(Guid seriesId, CancellationToken cancellationToken) =>
        await db.AgendaEvents.Include(agendaEvent => agendaEvent.Tags)
            .Where(agendaEvent => agendaEvent.SeriesId == seriesId)
            .OrderBy(agendaEvent => agendaEvent.StartsAtUtc).ThenBy(agendaEvent => agendaEvent.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AgendaEvent>> FindNeedingOutlookSyncAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        await db.AgendaEvents
            .Where(agendaEvent => agendaEvent.Status == EventStatus.Scheduled
                && agendaEvent.StartsAtUtc > now
                && (agendaEvent.OutlookStatus != OutlookStatus.Published || agendaEvent.OutlookSyncedRevision != agendaEvent.Revision))
            .OrderBy(agendaEvent => agendaEvent.StartsAtUtc).ThenBy(agendaEvent => agendaEvent.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OverlapCandidate>> FindOverlapCandidatesAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken) =>
        await OverlapQueries.CandidatesAsync(db, startUtc, endUtc, cancellationToken);

    public void Add(AgendaEvent agendaEvent) => db.AgendaEvents.Add(agendaEvent);

    public void AddRange(IEnumerable<AgendaEvent> agendaEvents) => db.AgendaEvents.AddRange(agendaEvents);

    public void Remove(AgendaEvent agendaEvent) => db.AgendaEvents.Remove(agendaEvent);
}

internal sealed class EventSeriesRepository(AppDbContext db) : IEventSeriesRepository
{
    public Task<EventSeries?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.AgendaSeries.SingleOrDefaultAsync(series => series.Id == id, cancellationToken);

    public void Add(EventSeries series) => db.AgendaSeries.Add(series);
}

internal sealed class AgendaTagRepository(AppDbContext db) : IAgendaTagRepository
{
    public async Task<IReadOnlyList<AgendaTag>> FindByKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        var wanted = keys.ToArray();
        return await db.AgendaTags.Where(tag => wanted.Contains(tag.Name.ToLower())).ToListAsync(cancellationToken);
    }

    public void Add(AgendaTag tag) => db.AgendaTags.Add(tag);
}

/// <summary>Reads the ITMas users for the agenda: the responsible of an event and its invitee address.</summary>
internal sealed class AgendaDirectory(AppDbContext db) : IAgendaDirectory
{
    public Task<AgendaUser?> GetUserAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => new AgendaUser(user.Id, user.DisplayName, user.Email, user.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, AgendaUser>> GetUsersAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.ToArray();
        return await db.Users.AsNoTracking()
            .Where(user => wanted.Contains(user.Id))
            .Select(user => new AgendaUser(user.Id, user.DisplayName, user.Email, user.IsActive))
            .ToDictionaryAsync(user => user.Id, cancellationToken);
    }
}

/// <summary>The candidates of the overlap warnings (SPEC-0006 RN-11), shared by the repository and the queries.</summary>
internal static class OverlapQueries
{
    /// <summary>
    /// Draft and Scheduled events that share time with the interval and can cause a warning: scheduled freezes and events with
    /// an impact. Others cannot, so they are not read.
    /// </summary>
    /// <summary>
    /// No event lasts longer than this (RN-2: 60 days, plus a day for the hour a change of daylight saving time may add). An event
    /// that overlaps an interval starts after its start minus this, which gives the index on the start a lower bound as well, so a
    /// short range does not read the whole history.
    /// </summary>
    public static readonly TimeSpan LongestEvent = EventSchedule.MaxDuration + TimeSpan.FromDays(1);

    public static async Task<IReadOnlyList<OverlapCandidate>> CandidatesAsync(
        AppDbContext db, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
    {
        var earliestStart = startUtc - LongestEvent;
        return await (from agendaEvent in db.AgendaEvents.AsNoTracking()
               join type in db.AgendaEventTypes.AsNoTracking() on agendaEvent.TypeId equals type.Id
               where (agendaEvent.Status == EventStatus.Draft || agendaEvent.Status == EventStatus.Scheduled)
                   && agendaEvent.StartsAtUtc < endUtc
                   && agendaEvent.StartsAtUtc > earliestStart
                   && agendaEvent.EndsAtUtc > startUtc
                   && ((type.IsFreeze && agendaEvent.Status == EventStatus.Scheduled) || agendaEvent.Impact != EventImpact.None)
               select new OverlapCandidate(
                   agendaEvent.Id, agendaEvent.Title, agendaEvent.StartsAtUtc, agendaEvent.EndsAtUtc, agendaEvent.Status, agendaEvent.Impact, type.IsFreeze))
            .ToListAsync(cancellationToken);
    }
}
