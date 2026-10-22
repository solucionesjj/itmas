using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Queries;
using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;
using Itmas3.Infrastructure.Authorization;
using Itmas3.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itmas3.Infrastructure.Agenda;

/// <summary>
/// Read-only projections of the agenda (no tracking, parameterised SQL only). Date ranges are local dates of the zone of the
/// organization, turned into UTC instants with that zone (§13.2). The overdue mark and the warnings are calculated here from the
/// current time and the overlapping events, as they are not stored.
/// </summary>
internal sealed class AgendaQueries(AppDbContext db, AgendaSettings settings, TimeProvider timeProvider) : IAgendaQueries
{
    public async Task<AgendaEventList> ListEventsAsync(DateOnly from, DateOnly toExclusive, AgendaFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var now = timeProvider.GetUtcNow();
        var events = InRange(Filtered(db.AgendaEvents.AsNoTracking(), filter, now), from, toExclusive);

        var rows = await (from agendaEvent in events
                          join type in db.AgendaEventTypes.AsNoTracking() on agendaEvent.TypeId equals type.Id
                          join user in db.Users.AsNoTracking() on agendaEvent.ResponsibleUserId equals user.Id
                          orderby agendaEvent.StartsAtUtc, agendaEvent.Id
                          select new
                          {
                              agendaEvent.Id,
                              agendaEvent.SeriesId,
                              TypeId = type.Id,
                              TypeName = type.Name,
                              type.IsFreeze,
                              agendaEvent.Title,
                              agendaEvent.StartsAtLocal,
                              agendaEvent.EndsAtLocal,
                              agendaEvent.StartsAtUtc,
                              agendaEvent.EndsAtUtc,
                              agendaEvent.IsAllDay,
                              agendaEvent.Status,
                              ResponsibleId = user.Id,
                              ResponsibleName = user.DisplayName,
                              ResponsibleActive = user.IsActive,
                              agendaEvent.Impact,
                              Tags = agendaEvent.Tags.Select(tag => tag.Name).ToList(),
                              agendaEvent.OutlookStatus,
                          })
            .Take(AgendaLimits.MaxResults + 1)
            .ToListAsync(cancellationToken);

        var truncated = rows.Count > AgendaLimits.MaxResults;
        if (truncated)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var candidates = rows.Count == 0
            ? []
            : await OverlapQueries.CandidatesAsync(db, rows.Min(row => row.StartsAtUtc), rows.Max(row => row.EndsAtUtc), cancellationToken);

        var items = rows.Select(row => new AgendaEventSummary(
            row.Id,
            row.SeriesId,
            new AgendaTypeRef(row.TypeId, row.TypeName, row.IsFreeze),
            row.Title,
            row.StartsAtLocal,
            row.EndsAtLocal,
            row.StartsAtUtc,
            row.EndsAtUtc,
            row.IsAllDay,
            row.Status,
            row.Status is EventStatus.Draft or EventStatus.Scheduled && row.EndsAtUtc < now,
            new AgendaPersonRef(row.ResponsibleId, row.ResponsibleName, row.ResponsibleActive),
            row.Impact,
            [.. row.Tags.Order(StringComparer.OrdinalIgnoreCase)],
            row.Status is EventStatus.Draft or EventStatus.Scheduled
                && AgendaWarnings.Evaluate(row.Id, row.StartsAtUtc, row.EndsAtUtc, row.IsFreeze, row.Impact, candidates).Count > 0,
            row.OutlookStatus)).ToList();

        return new AgendaEventList(items, truncated, settings.TimeZone.Id, settings.OutlookEnabled);
    }

    public async Task<AgendaExportList> ListExportEventsAsync(DateOnly from, DateOnly toExclusive, AgendaFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var events = InRange(Filtered(db.AgendaEvents.AsNoTracking(), filter, timeProvider.GetUtcNow()), from, toExclusive);

        // No user is joined: the export carries neither the responsible nor any address (RF-21).
        var rows = await (from agendaEvent in events
                          join type in db.AgendaEventTypes.AsNoTracking() on agendaEvent.TypeId equals type.Id
                          orderby agendaEvent.StartsAtUtc, agendaEvent.Id
                          select new
                          {
                              agendaEvent.Id,
                              TypeName = type.Name,
                              agendaEvent.Title,
                              agendaEvent.Description,
                              agendaEvent.Scope,
                              agendaEvent.Location,
                              agendaEvent.Impact,
                              agendaEvent.IsAllDay,
                              agendaEvent.StartsAtLocal,
                              agendaEvent.EndsAtLocal,
                              agendaEvent.StartsAtUtc,
                              agendaEvent.EndsAtUtc,
                              Tags = agendaEvent.Tags.Select(tag => tag.Name).ToList(),
                              LastModified = agendaEvent.UpdatedAt ?? agendaEvent.CreatedAt,
                              agendaEvent.Revision,
                          })
            .Take(AgendaLimits.MaxResults + 1)
            .ToListAsync(cancellationToken);

        var truncated = rows.Count > AgendaLimits.MaxResults;
        var items = rows.Take(AgendaLimits.MaxResults).Select(row => new AgendaExportEvent(
            row.Id,
            row.TypeName,
            row.Title,
            row.Description,
            row.Scope,
            row.Location,
            row.Impact,
            row.IsAllDay,
            row.StartsAtLocal,
            row.EndsAtLocal,
            row.StartsAtUtc,
            row.EndsAtUtc,
            [.. row.Tags.Order(StringComparer.OrdinalIgnoreCase)],
            row.LastModified,
            row.Revision)).ToList();

        return new AgendaExportList(items, truncated);
    }

    public async Task<AgendaSummary> GetSummaryAsync(int year, AgendaFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var now = timeProvider.GetUtcNow();
        var firstDay = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var nextYear = firstDay.AddYears(1);

        // The status filter does not apply: the summary counts every status.
        var events = Filtered(db.AgendaEvents.AsNoTracking(), filter with { Status = null }, now)
            .Where(agendaEvent => agendaEvent.StartsAtLocal >= firstDay && agendaEvent.StartsAtLocal < nextYear);

        var byMonth = await events
            .GroupBy(agendaEvent => new
            {
                agendaEvent.StartsAtLocal.Month,
                agendaEvent.Status,
                Overdue = (agendaEvent.Status == EventStatus.Draft || agendaEvent.Status == EventStatus.Scheduled) && agendaEvent.EndsAtUtc < now,
            })
            .Select(group => new { group.Key.Month, group.Key.Status, group.Key.Overdue, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var byType = await events
            .GroupBy(agendaEvent => agendaEvent.TypeId)
            .Select(group => new AgendaTypeCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        // Counts are by status; "overdue" is how many of the drafts and scheduled ones have passed their end.
        var months = Enumerable.Range(1, 12).Select(month =>
        {
            var rows = byMonth.Where(row => row.Month == month).ToList();
            int Count(EventStatus status) => rows.Where(row => row.Status == status).Sum(row => row.Count);
            return new AgendaMonthSummary(
                month, Count(EventStatus.Draft), Count(EventStatus.Scheduled), Count(EventStatus.Completed), Count(EventStatus.Cancelled),
                rows.Where(row => row.Overdue).Sum(row => row.Count));
        }).ToList();

        return new AgendaSummary(year, months, [.. byType.OrderByDescending(row => row.Count).ThenBy(row => row.TypeId)]);
    }

    public async Task<AgendaEventDetail?> GetEventAsync(Guid id, CancellationToken cancellationToken)
    {
        var agendaEvent = await db.AgendaEvents.AsNoTracking().Include(candidate => candidate.Tags)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (agendaEvent is null)
        {
            return null;
        }

        var type = await db.AgendaEventTypes.AsNoTracking().SingleAsync(candidate => candidate.Id == agendaEvent.TypeId, cancellationToken);
        var userIds = new[] { agendaEvent.ResponsibleUserId, agendaEvent.CreatedBy, agendaEvent.UpdatedBy ?? Guid.Empty, agendaEvent.CompletedBy ?? Guid.Empty }
            .Where(userId => userId != Guid.Empty).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.DisplayName, user.IsActive })
            .ToDictionaryAsync(user => user.Id, cancellationToken);
        var pattern = agendaEvent.SeriesId is { } seriesId
            ? await db.AgendaSeries.AsNoTracking().Where(series => series.Id == seriesId).Select(series => series.Pattern).SingleOrDefaultAsync(cancellationToken)
            : null;

        string? NameOf(Guid? userId) => userId is { } value && users.TryGetValue(value, out var user) ? user.DisplayName : null;
        var responsible = users.TryGetValue(agendaEvent.ResponsibleUserId, out var person)
            ? new AgendaPersonRef(person.Id, person.DisplayName, person.IsActive)
            : new AgendaPersonRef(agendaEvent.ResponsibleUserId, string.Empty, false);

        var warnings = agendaEvent.Status is EventStatus.Draft or EventStatus.Scheduled
            ? AgendaWarnings.Evaluate(
                agendaEvent.Id, agendaEvent.StartsAtUtc, agendaEvent.EndsAtUtc, type.IsFreeze, agendaEvent.Impact,
                await OverlapQueries.CandidatesAsync(db, agendaEvent.StartsAtUtc, agendaEvent.EndsAtUtc, cancellationToken))
            : [];

        var completion = agendaEvent is { Status: EventStatus.Completed, CompletionResult: { } result }
            ? new AgendaCompletionView(
                result,
                agendaEvent.ExecutedAt ?? agendaEvent.CompletedAt ?? agendaEvent.CreatedAt,
                agendaEvent.CompletedAt ?? agendaEvent.CreatedAt,
                NameOf(agendaEvent.CompletedBy),
                agendaEvent.CompletionNotes,
                [.. agendaEvent.EvidenceLinks.Select(link => new AgendaEvidenceLinkView(link.Label, link.Url))])
            : null;

        return new AgendaEventDetail(
            agendaEvent.Id,
            agendaEvent.SeriesId,
            pattern,
            new AgendaTypeRef(type.Id, type.Name, type.IsFreeze),
            agendaEvent.Title,
            agendaEvent.Description,
            agendaEvent.Status,
            agendaEvent.IsOverdue(timeProvider.GetUtcNow()),
            agendaEvent.IsAllDay,
            agendaEvent.StartsAtLocal,
            agendaEvent.EndsAtLocal,
            agendaEvent.TimeZoneId,
            agendaEvent.StartsAtUtc,
            agendaEvent.EndsAtUtc,
            responsible,
            agendaEvent.Participants,
            agendaEvent.NotifyTeam,
            agendaEvent.Impact,
            agendaEvent.Scope,
            agendaEvent.Location,
            agendaEvent.ReminderMinutes,
            TagNames(agendaEvent),
            agendaEvent.CancelReason,
            completion,
            new AgendaOutlookView(agendaEvent.OutlookStatus, agendaEvent.OutlookErrorCode, agendaEvent.OutlookStatusAt, agendaEvent.OutlookWebLink),
            agendaEvent.CreatedAt,
            NameOf(agendaEvent.CreatedBy),
            agendaEvent.UpdatedAt,
            NameOf(agendaEvent.UpdatedBy),
            agendaEvent.Revision,
            warnings,
            settings.OutlookEnabled);
    }

    public async Task<IReadOnlyList<AgendaPersonOption>> SearchPeopleAsync(string? search, int limit, Guid? meId, CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking().Where(user => user.IsActive);
        if (LikePattern.Contains(search) is { } pattern)
        {
            users = users.Where(user => EF.Functions.ILike(user.DisplayName, pattern, LikePattern.Escape));
        }

        var take = Math.Clamp(limit, 1, AgendaLimits.MaxPeople);
        var people = await users
            .OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
            .Take(take)
            .Select(user => new AgendaPersonOption(user.Id, user.DisplayName))
            .ToListAsync(cancellationToken);

        if (meId is { } id && string.IsNullOrWhiteSpace(search) && people.All(person => person.Id != id))
        {
            var me = await users.Where(user => user.Id == id).Select(user => new AgendaPersonOption(user.Id, user.DisplayName)).SingleOrDefaultAsync(cancellationToken);
            if (me is not null)
            {
                people.Insert(0, me);
            }
        }

        return people;
    }

    public async Task<IReadOnlyList<AgendaTagCount>> ListTagsAsync(string? search, int limit, CancellationToken cancellationToken)
    {
        // Counts come from the join table; a tag without events is not listed (§13.2).
        var counts = db.Set<Dictionary<string, object>>("agenda_event_tags").AsNoTracking()
            .GroupBy(link => EF.Property<Guid>(link, "tag_id"))
            .Select(group => new { TagId = group.Key, Count = group.Count() });
        var tags = from tag in db.AgendaTags.AsNoTracking()
                   join count in counts on tag.Id equals count.TagId
                   select new { tag.Name, count.Count };
        if (LikePattern.Contains(search) is { } pattern)
        {
            tags = tags.Where(row => EF.Functions.ILike(row.Name, pattern, LikePattern.Escape));
        }

        var take = Math.Clamp(limit, 1, AgendaLimits.MaxTagSuggestions);
        return await tags
            .OrderByDescending(row => row.Count).ThenBy(row => row.Name)
            .Take(take)
            .Select(row => new AgendaTagCount(row.Name, row.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgendaEventTypeView>> ListEventTypesAsync(CancellationToken cancellationToken) =>
        await db.AgendaEventTypes.AsNoTracking()
            .OrderBy(type => type.Name).ThenBy(type => type.Id)
            .Select(type => new AgendaEventTypeView(
                type.Id, type.Name, type.Description, type.DefaultDurationMinutes, type.DefaultReminderMinutes, type.IsFreeze, type.IsActive, type.Revision))
            .ToListAsync(cancellationToken);

    public Task<AgendaEventTypeView?> GetEventTypeAsync(Guid id, CancellationToken cancellationToken) =>
        db.AgendaEventTypes.AsNoTracking()
            .Where(type => type.Id == id)
            .Select(type => new AgendaEventTypeView(
                type.Id, type.Name, type.Description, type.DefaultDurationMinutes, type.DefaultReminderMinutes, type.IsFreeze, type.IsActive, type.Revision))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Events that share time with the local dates <paramref name="from"/> (inclusive) to <paramref name="toExclusive"/>, resolved to UTC with the zone of the organization.</summary>
    private IQueryable<AgendaEvent> InRange(IQueryable<AgendaEvent> events, DateOnly from, DateOnly toExclusive)
    {
        var startUtc = LocalTime.Resolve(from.ToDateTime(TimeOnly.MinValue), settings.TimeZone).Utc;
        var endUtc = LocalTime.Resolve(toExclusive.ToDateTime(TimeOnly.MinValue), settings.TimeZone).Utc;
        var earliestStart = startUtc - OverlapQueries.LongestEvent;
        return events.Where(agendaEvent =>
            agendaEvent.StartsAtUtc < endUtc && agendaEvent.StartsAtUtc > earliestStart && agendaEvent.EndsAtUtc > startUtc);
    }

    private static IReadOnlyList<string> TagNames(AgendaEvent agendaEvent) =>
        [.. agendaEvent.Tags.Select(tag => tag.Name).Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The filters shared by every view (RF-12). Several tags require the event to have all of them (RF-25).</summary>
    private static IQueryable<AgendaEvent> Filtered(IQueryable<AgendaEvent> events, AgendaFilter filter, DateTimeOffset now)
    {
        if (filter.TypeId is { } typeId)
        {
            events = events.Where(agendaEvent => agendaEvent.TypeId == typeId);
        }

        if (filter.ResponsibleId is { } responsibleId)
        {
            events = events.Where(agendaEvent => agendaEvent.ResponsibleUserId == responsibleId);
        }

        if (filter.MineUserId is { } mineId)
        {
            events = events.Where(agendaEvent => agendaEvent.ResponsibleUserId == mineId);
        }

        events = filter.Status switch
        {
            AgendaStatusFilter.Draft => events.Where(agendaEvent => agendaEvent.Status == EventStatus.Draft),
            AgendaStatusFilter.Scheduled => events.Where(agendaEvent => agendaEvent.Status == EventStatus.Scheduled),
            AgendaStatusFilter.Completed => events.Where(agendaEvent => agendaEvent.Status == EventStatus.Completed),
            AgendaStatusFilter.Cancelled => events.Where(agendaEvent => agendaEvent.Status == EventStatus.Cancelled),
            AgendaStatusFilter.Overdue => events.Where(agendaEvent =>
                (agendaEvent.Status == EventStatus.Draft || agendaEvent.Status == EventStatus.Scheduled) && agendaEvent.EndsAtUtc < now),
            _ => events,
        };

        foreach (var key in filter.Tags.Select(FilterKey).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            var wanted = key;
            events = events.Where(agendaEvent => agendaEvent.Tags.Any(tag => tag.Name.ToLower() == wanted));
        }

        if (LikePattern.Contains(filter.Search) is { } pattern)
        {
            events = events.Where(agendaEvent => EF.Functions.ILike(agendaEvent.Title, pattern, LikePattern.Escape)
                || agendaEvent.Tags.Any(tag => EF.Functions.ILike(tag.Name, pattern, LikePattern.Escape)));
        }

        return events;
    }

    /// <summary>
    /// A tag of the filter as the key it is stored with (RN-15): spaces collapsed and the same case folding as the unique index, so
    /// "  SQL   Server " finds "sql server". A text that cannot be a tag (too long, control characters) is kept as is and matches no
    /// event; an empty one is ignored.
    /// </summary>
    private static string? FilterKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return TagName.Key(TagName.Normalize(raw));
        }
        catch (DomainException)
        {
            return TagName.Key(raw);
        }
    }
}
