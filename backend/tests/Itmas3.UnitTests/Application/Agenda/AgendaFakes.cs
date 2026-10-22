using Itmas3.Application.Abstractions.Authorization;
using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Agenda.Events;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Application.Agenda.Series;
using Itmas3.Application.Agenda.Types;
using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Agenda;
using Itmas3.UnitTests.Application.Authorization;
using Itmas3.UnitTests.Application.Inventory;

namespace Itmas3.UnitTests.Application.Agenda;

internal sealed class InMemoryEventTypes : IEventTypeRepository
{
    public List<EventType> Items { get; } = [];

    public Task<EventType?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(type => type.Id == id));

    public Task<IReadOnlyDictionary<Guid, EventType>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, EventType>>(Items.Where(type => ids.Contains(type.Id)).ToDictionary(type => type.Id));

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(type => type.Id != excludeId && string.Equals(type.Name, name, StringComparison.OrdinalIgnoreCase)));

    public void Add(EventType type) => Items.Add(type);
}

internal sealed class InMemoryAgendaEvents : IAgendaEventRepository
{
    private readonly InMemoryEventTypes _types;

    public InMemoryAgendaEvents(InMemoryEventTypes types) => _types = types;

    public List<AgendaEvent> Items { get; } = [];

    public Task<AgendaEvent?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(e => e.Id == id));

    public Task<IReadOnlyList<AgendaEvent>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AgendaEvent>>([.. Items.Where(e => ids.Contains(e.Id))]);

    public Task<IReadOnlyList<AgendaEvent>> GetBySeriesAsync(Guid seriesId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AgendaEvent>>([.. Items.Where(e => e.SeriesId == seriesId).OrderBy(e => e.StartsAtUtc)]);

    public Task<IReadOnlyList<AgendaEvent>> FindNeedingOutlookSyncAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AgendaEvent>>(
            [.. Items.Where(e => e.NeedsOutlookSync && e.StartsAtUtc > now).OrderBy(e => e.StartsAtUtc).Take(limit)]);

    public Task<IReadOnlyList<OverlapCandidate>> FindOverlapCandidatesAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OverlapCandidate>>(
            [.. Items
                .Where(e => e.Status is EventStatus.Draft or EventStatus.Scheduled && e.StartsAtUtc < endUtc && startUtc < e.EndsAtUtc)
                .Select(e => new OverlapCandidate(e.Id, e.Title, e.StartsAtUtc, e.EndsAtUtc, e.Status, e.Impact, _types.Items.Single(t => t.Id == e.TypeId).IsFreeze))]);

    public void Add(AgendaEvent agendaEvent) => Items.Add(agendaEvent);

    public void AddRange(IEnumerable<AgendaEvent> agendaEvents) => Items.AddRange(agendaEvents);

    public void Remove(AgendaEvent agendaEvent) => Items.Remove(agendaEvent);
}

internal sealed class InMemoryEventSeries : IEventSeriesRepository
{
    public List<EventSeries> Items { get; } = [];

    public Task<EventSeries?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(series => series.Id == id));

    public void Add(EventSeries series) => Items.Add(series);
}

internal sealed class InMemoryAgendaTags : IAgendaTagRepository
{
    public List<AgendaTag> Items { get; } = [];

    public Task<IReadOnlyList<AgendaTag>> FindByKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AgendaTag>>([.. Items.Where(tag => keys.Contains(TagName.Key(tag.Name)))]);

    public void Add(AgendaTag tag) => Items.Add(tag);
}

internal sealed class InMemoryAgendaDirectory : IAgendaDirectory
{
    public Dictionary<Guid, AgendaUser> Users { get; } = [];

    public Task<AgendaUser?> GetUserAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Users.GetValueOrDefault(id));

    public Task<IReadOnlyDictionary<Guid, AgendaUser>> GetUsersAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, AgendaUser>>(Users.Where(pair => ids.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value));
}

/// <summary>Runs the work and keeps every audit entry it recorded, like the real unit of work does inside its transaction.</summary>
internal sealed class RecordingAgendaUnitOfWork : IAgendaUnitOfWork
{
    public List<AuditEntry> Entries { get; } = [];

    /// <summary>Raised instead of running the work, to simulate a concurrent change detected by the database.</summary>
    public Exception? FailWith { get; set; }

    public async Task<T> ExecuteAsync<T>(Func<AuditTrail, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        if (FailWith is { } failure)
        {
            throw failure;
        }

        var trail = new AuditTrail();
        var result = await work(trail, cancellationToken);
        Entries.AddRange(trail.Entries);
        return result;
    }
}

/// <summary>An Outlook gateway that answers from a script and records what it was asked. It never calls anything.</summary>
internal sealed class FakeOutlookGateway : IOutlookCalendarGateway
{
    private readonly Queue<OutlookGatewayResult> _script = new();

    public List<(OutlookEventData Data, string? OutlookEventId)> Published { get; } = [];

    public List<(string OutlookEventId, string Reason)> Cancelled { get; } = [];

    /// <summary>When set, every call throws it (to prove that a failure of Outlook never fails the operation).</summary>
    public Exception? ThrowOnCall { get; set; }

    public int Calls => Published.Count + Cancelled.Count;

    public void Enqueue(params OutlookGatewayResult[] results)
    {
        foreach (var result in results)
        {
            _script.Enqueue(result);
        }
    }

    public Task<OutlookGatewayResult> PublishAsync(OutlookEventData data, string? outlookEventId, CancellationToken cancellationToken)
    {
        Published.Add((data, outlookEventId));
        return Task.FromResult(Next(() => OutlookGatewayResult.Success(outlookEventId ?? $"AAMk-{data.EventId:N}", $"https://outlook.example/{data.EventId:N}")));
    }

    public Task<OutlookGatewayResult> CancelAsync(string outlookEventId, string reason, CancellationToken cancellationToken)
    {
        Cancelled.Add((outlookEventId, reason));
        return Task.FromResult(Next(() => OutlookGatewayResult.Success(outlookEventId, null)));
    }

    private OutlookGatewayResult Next(Func<OutlookGatewayResult> otherwise)
    {
        if (ThrowOnCall is { } failure)
        {
            throw failure;
        }

        return _script.Count > 0 ? _script.Dequeue() : otherwise();
    }
}

/// <summary>Permissions the tests grant to the current user; everything is granted unless a test says otherwise.</summary>
internal sealed class FakePermissionEvaluator : IPermissionEvaluator
{
    public HashSet<string> Denied { get; } = new(StringComparer.Ordinal);

    public ValueTask<bool> IsGrantedAsync(ICurrentUser user, string permission, CancellationToken cancellationToken) =>
        ValueTask.FromResult(!Denied.Contains(permission));
}

/// <summary>The shared budget of notices (P-26): counts what is taken and refuses once <see cref="Remaining"/> reaches zero.</summary>
internal sealed class FakePublishLimit : IAgendaPublishLimit
{
    public int Remaining { get; set; } = int.MaxValue;

    public int Taken { get; private set; }

    public bool TryAcquire(out TimeSpan? retryAfter)
    {
        retryAfter = null;
        if (Remaining <= 0)
        {
            retryAfter = TimeSpan.FromMinutes(30);
            return false;
        }

        Remaining--;
        Taken++;
        return true;
    }
}

/// <summary>A clock the tests move to record an execution after the start of an event (RN-8).</summary>
internal sealed class AdjustableClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Set(DateTimeOffset value) => _now = value;
}

/// <summary>All the agenda use cases wired to in-memory fakes, with the same clock, user and settings.</summary>
internal sealed class AgendaHarness
{
    public static readonly DateTimeOffset Now = new(2026, 10, 17, 17, 0, 0, TimeSpan.Zero);

    public static readonly Guid CurrentUserId = Guid.Parse("0196b7a0-0000-7000-8000-0000000000aa");

    /// <param name="outlookEnabled">Whether events are published to Outlook.</param>
    /// <param name="teamAddress">Address of the team, invited as optional.</param>
    /// <param name="gateway">The Outlook port to use instead of <see cref="Gateway"/> (for example, the adapter of Graph over a simulated handler).</param>
    public AgendaHarness(bool outlookEnabled = false, string? teamAddress = "equipo@example.com", IOutlookCalendarGateway? gateway = null)
    {
        Settings = new AgendaSettings
        {
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota"),
            AllowedAttendeeDomains = ["example.com"],
            PublicUrl = "https://itmas.example.com/",
            OutlookEnabled = outlookEnabled,
            TeamAddress = teamAddress,
        };
        Events = new InMemoryAgendaEvents(Types);
        Directory.Users[CurrentUserId] = new AgendaUser(CurrentUserId, "Planificador", "planificador@example.com", true);
        Directory.Users[Responsible.Id] = Responsible;
        Types.Items.Add(DefaultType);

        var clock = new AdjustableClock(Now);
        var user = new FakeCurrentUser(CurrentUserId);
        Clock = clock;
        var preparer = new AgendaEventPreparer(Types, Directory, Settings);
        var resolver = new AgendaTagResolver(Tags);
        var warnings = new AgendaWarningService(Events);
        Publisher = new OutlookPublisher(gateway ?? Gateway, UnitOfWork, Events, clock);
        var guard = new AgendaNotificationGuard(user, Permissions, PublishLimit);

        CreateType = new CreateEventTypeHandler(Types, UnitOfWork, clock);
        UpdateType = new UpdateEventTypeHandler(Types, UnitOfWork, clock);
        Create = new CreateEventHandler(Settings, Events, UnitOfWork, user, clock, preparer, warnings, resolver);
        Update = new UpdateEventHandler(Settings, Events, UnitOfWork, user, clock, preparer, warnings, resolver, Publisher, guard);
        Delete = new DeleteDraftEventHandler(Events, UnitOfWork);
        Schedule = new ScheduleEventsHandler(Settings, Events, Types, Directory, UnitOfWork, user, clock, warnings, Publisher);
        Cancel = new CancelEventHandler(Settings, Events, UnitOfWork, user, clock, Publisher);
        Complete = new CompleteEventHandler(Events, UnitOfWork, user, clock);
        SetTags = new SetEventTagsHandler(Events, UnitOfWork, user, clock, resolver);
        CreateSeries = new CreateSeriesHandler(Settings, preparer, warnings, resolver, Events, Series, UnitOfWork, user, clock);
        UpdateSeries = new UpdateSeriesHandler(Settings, preparer, resolver, Events, Series, UnitOfWork, user, clock, Publisher, guard);
        Sync = new SyncWithOutlookHandler(Settings, Events, Types, Directory, UnitOfWork, clock, Publisher);
    }

    public AgendaSettings Settings { get; }

    public InMemoryEventTypes Types { get; } = new();

    public InMemoryAgendaEvents Events { get; }

    public InMemoryEventSeries Series { get; } = new();

    public InMemoryAgendaTags Tags { get; } = new();

    public InMemoryAgendaDirectory Directory { get; } = new();

    public RecordingAgendaUnitOfWork UnitOfWork { get; } = new();

    public FakeOutlookGateway Gateway { get; } = new();

    public FakePermissionEvaluator Permissions { get; } = new();

    public FakePublishLimit PublishLimit { get; } = new();

    public AdjustableClock Clock { get; }

    public OutlookPublisher Publisher { get; }

    public EventType DefaultType { get; } = EventType.Create("Mantenimiento y parches", null, 120, 1440, false, Now);

    public AgendaUser Responsible { get; } = new(Guid.Parse("0196b7a0-0000-7000-8000-0000000000bb"), "Ana Pérez", "ana.perez@example.com", true);

    public CreateEventTypeHandler CreateType { get; }

    public UpdateEventTypeHandler UpdateType { get; }

    public CreateEventHandler Create { get; }

    public UpdateEventHandler Update { get; }

    public DeleteDraftEventHandler Delete { get; }

    public ScheduleEventsHandler Schedule { get; }

    public CancelEventHandler Cancel { get; }

    public CompleteEventHandler Complete { get; }

    public SetEventTagsHandler SetTags { get; }

    public CreateSeriesHandler CreateSeries { get; }

    public UpdateSeriesHandler UpdateSeries { get; }

    public SyncWithOutlookHandler Sync { get; }

    /// <summary>An input for a two-hour activity next Saturday at 09:00 (Bogotá), with the default type and responsible.</summary>
    public EventInput Input(
        string title = "Parches de SO",
        Guid? typeId = null,
        Guid? responsible = null,
        DateTime? start = null,
        int? reminder = null,
        IReadOnlyList<string>? participants = null,
        EventImpact impact = EventImpact.None,
        IReadOnlyList<string>? tags = null,
        EventTimingInput? timing = null) =>
        new(
            typeId ?? DefaultType.Id,
            title,
            "Aplicar los parches del mes",
            responsible ?? Responsible.Id,
            participants,
            true,
            impact,
            "Servidores Windows",
            "Remoto",
            reminder,
            tags,
            timing ?? new EventTimingInput(false, start ?? new DateTime(2026, 10, 24, 9, 0, 0), (start ?? new DateTime(2026, 10, 24, 9, 0, 0)).AddHours(2), null, null));

    public async Task<AgendaEvent> CreateDraftAsync(EventInput? input = null)
    {
        var result = await Create.HandleAsync(input ?? Input(), CancellationToken.None);
        return Events.Items.Single(e => e.Id == result.Id);
    }

    public async Task<AgendaEvent> CreateScheduledAsync(EventInput? input = null)
    {
        var draft = await CreateDraftAsync(input);
        await Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), CancellationToken.None);
        return draft;
    }
}
