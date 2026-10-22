using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Domain.Agenda;
using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Itmas3.IntegrationTests.Api.Agenda;

/// <summary>
/// SPEC-0006 CA-14 to CA-16: the real API and PostgreSQL with Outlook enabled and the port replaced by a scripted double (no test
/// calls Microsoft, constitution §9.4). What the port receives when scheduling, modifying and cancelling, a failure that never
/// blocks, "Sync again" without duplicating and a batch that stops on throttling. Only events created by each test are touched:
/// "sync everything" would also pick up the scheduled events of the tests that run in parallel, so its only test moves the clock
/// of its host to 2190, where nobody else has events.
/// </summary>
public sealed class AgendaOutlookFlowTests(PostgreSqlFixture database) : AgendaTestBase(database)
{
    private const string TeamAddress = "equipo@example.com";

    private sealed record PublishCall(OutlookEventData Data, string? OutlookEventId);

    private sealed record CancelCall(string OutlookEventId, string Reason);

    /// <summary>A port that records what it receives and answers what the test says (a success by default).</summary>
    private sealed class ScriptedGateway : IOutlookCalendarGateway
    {
        private readonly object _gate = new();
        private readonly List<PublishCall> _published = [];
        private readonly List<CancelCall> _cancelled = [];

        public Func<OutlookEventData, string?, OutlookGatewayResult> OnPublish { get; set; } = Succeed;

        public IReadOnlyList<PublishCall> Published
        {
            get
            {
                lock (_gate)
                {
                    return [.. _published];
                }
            }
        }

        public IReadOnlyList<CancelCall> Cancelled
        {
            get
            {
                lock (_gate)
                {
                    return [.. _cancelled];
                }
            }
        }

        public static OutlookGatewayResult Succeed(OutlookEventData data, string? outlookEventId) =>
            OutlookGatewayResult.Success(outlookEventId ?? $"outlook-{data.EventId:N}", $"https://outlook.example.com/calendar/{data.EventId:N}");

        public Task<OutlookGatewayResult> PublishAsync(OutlookEventData data, string? outlookEventId, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _published.Add(new PublishCall(data, outlookEventId));
                return Task.FromResult(OnPublish(data, outlookEventId));
            }
        }

        public Task<OutlookGatewayResult> CancelAsync(string outlookEventId, string reason, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _cancelled.Add(new CancelCall(outlookEventId, reason));
            }

            return Task.FromResult(OutlookGatewayResult.Success(outlookEventId, null));
        }
    }

    /// <summary>The API with Outlook enabled and the scripted port, and a client with every agenda permission.</summary>
    private sealed class OutlookApp(WebApplicationFactory<Program> app, HttpClient client, ScriptedGateway gateway) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public ScriptedGateway Gateway { get; } = gateway;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    /// <summary>A clock that never moves, to place "now" where only the events of one test are ahead of it.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private async Task<OutlookApp> StartAsync(TimeProvider? clock = null)
    {
        var gateway = new ScriptedGateway();
        var app = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Agenda:Outlook:Enabled", "true");
            builder.UseSetting("Agenda:Outlook:TenantId", "33333333-3333-3333-3333-333333333333");
            builder.UseSetting("Agenda:Outlook:ClientId", "44444444-4444-4444-4444-444444444444");
            builder.UseSetting("Agenda:Outlook:ClientSecret", "placeholder-not-a-secret");
            builder.UseSetting("Agenda:Outlook:Mailbox", "agenda@example.com");
            builder.UseSetting("Agenda:Outlook:TeamAddress", TeamAddress);
            builder.UseSetting("Agenda:PublicUrl", "https://itmas.example.com");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOutlookCalendarGateway>();
                services.AddSingleton<IOutlookCalendarGateway>(gateway);
                if (clock is not null)
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton(clock);
                }
            });
        });
        var user = await Factory.CreateUserAsync(false, AllPermissions);
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, user.Id.ToString());
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.StampHeader, user.SecurityStamp);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.NameHeader, user.DisplayName);
        client.DefaultRequestHeaders.Add("X-CSRF", "1");
        return new OutlookApp(app, client, gateway);
    }

    private async Task<Guid> CreateDraftAsync(OutlookApp app, string title)
    {
        var responsible = await UserAsync();
        return (await CreateEventAsync(app.Client, EventBody(responsible.Id, title))).GetProperty("id").GetGuid();
    }

    private static string OutlookStatus(JsonElement entry) => entry.GetProperty("outlook").GetProperty("status").GetString()!;

    private static Uri SyncUrl(Guid id) => Url($"/api/v1/agenda-events/{id}/outlook-sync");

    [Fact]
    public async Task CA14_scheduling_calls_the_port_once_with_the_content_of_rf16_and_keeps_the_outlook_id_and_link()
    {
        await using var app = await StartAsync();
        var responsible = await UserAsync();
        var (expectedStart, expectedEnd) = (Future(10), Future(10, 11));
        var created = await CreateEventAsync(
            app.Client,
            EventBody(responsible.Id, "Parches de agosto", participants: ["luis@example.com"], impact: "degradation", notifyTeam: true));
        var id = created.GetProperty("id").GetGuid();
        var typeName = created.GetProperty("type").GetProperty("name").GetString()!;

        var outcome = await ScheduleAsync(app.Client, id);

        var call = Assert.Single(app.Gateway.Published);
        Assert.Null(call.OutlookEventId);
        Assert.Equal(id, call.Data.EventId);
        Assert.Equal($"[{typeName}] Parches de agosto", call.Data.Subject);
        Assert.Equal(typeName, call.Data.Category);
        Assert.Equal("America/Bogota", call.Data.TimeZoneId);
        Assert.False(call.Data.IsAllDay);
        Assert.Equal(expectedStart, call.Data.StartLocal.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture));
        Assert.Equal(expectedEnd, call.Data.EndLocal.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture));
        Assert.Equal("Remoto", call.Data.Location);
        Assert.Equal(created.GetProperty("reminderMinutes").GetInt32(), call.Data.ReminderMinutes);
        Assert.Contains("luis@example.com", call.Data.RequiredAttendees);
        Assert.All(call.Data.RequiredAttendees, address => Assert.EndsWith("@example.com", address, StringComparison.Ordinal));
        Assert.Equal([TeamAddress], call.Data.OptionalAttendees);
        Assert.Contains("Aplicar los parches del mes", call.Data.BodyText, StringComparison.Ordinal);
        Assert.Contains("Servidores Windows", call.Data.BodyText, StringComparison.Ordinal);
        Assert.Contains($"https://itmas.example.com/agenda/events/{id}", call.Data.BodyText, StringComparison.Ordinal);

        Assert.Equal("scheduled", outcome.Of(id).GetProperty("status").GetString());
        Assert.Equal("published", OutlookStatus(outcome.Of(id)));
        var detail = await GetEventAsync(app.Client, id);
        Assert.Equal("published", OutlookStatus(detail));
        Assert.Equal($"https://outlook.example.com/calendar/{id:N}", detail.GetProperty("outlook").GetProperty("webLink").GetString());
        var audit = await AuditAsync("agenda.event.outlook-published");
        Assert.Contains(audit, row => row.ResourceId == id.ToString());
        Assert.DoesNotContain(audit, row => (row.After ?? string.Empty).Contains("placeholder-not-a-secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CA15_modifying_a_scheduled_event_updates_the_saved_outlook_event_and_a_draft_never_calls_outlook()
    {
        await using var app = await StartAsync();
        var responsible = await UserAsync();
        var id = (await CreateEventAsync(app.Client, EventBody(responsible.Id, "Título inicial"))).GetProperty("id").GetGuid();

        var draftEdit = await app.Client.SendAsync(Put(EventUrl(id), EventBody(responsible.Id, "Borrador editado"), 1), Ct);
        Assert.Equal(HttpStatusCode.OK, draftEdit.StatusCode);
        Assert.Empty(app.Gateway.Published);

        await ScheduleAsync(app.Client, id);
        var scheduled = await GetEventAsync(app.Client, id);
        var edit = await app.Client.SendAsync(Put(EventUrl(id), EventBody(responsible.Id, "Programado y editado"), scheduled.GetProperty("revision").GetInt32()), Ct);

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(2, app.Gateway.Published.Count);
        var update = app.Gateway.Published[1];
        Assert.Equal($"outlook-{id:N}", update.OutlookEventId);
        Assert.EndsWith("] Programado y editado", update.Data.Subject, StringComparison.Ordinal);
        Assert.Equal("published", OutlookStatus(await GetEventAsync(app.Client, id)));
    }

    [Fact]
    public async Task CA15_cancelling_a_published_event_cancels_it_in_outlook_with_the_reason_and_one_never_published_does_not()
    {
        await using var app = await StartAsync();
        var published = await CreateDraftAsync(app, "Publicada");
        await ScheduleAsync(app.Client, published);
        const string reason = "Se pospone por el cierre contable";

        var cancelled = await app.Client.PostAsJsonAsync(Url($"/api/v1/agenda-events/{published}/cancel"), new { reason }, Ct);

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("cancelled", (await GetEventAsync(app.Client, published)).GetProperty("status").GetString());
        Assert.Equal(new CancelCall($"outlook-{published:N}", reason), Assert.Single(app.Gateway.Cancelled));

        app.Gateway.OnPublish = (_, _) => OutlookGatewayResult.Failure(OutlookErrorCodes.Unavailable);
        var neverPublished = await CreateDraftAsync(app, "Nunca publicada");
        await ScheduleAsync(app.Client, neverPublished);
        var second = await app.Client.PostAsJsonAsync(Url($"/api/v1/agenda-events/{neverPublished}/cancel"), new { reason }, Ct);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("cancelled", (await GetEventAsync(app.Client, neverPublished)).GetProperty("status").GetString());
        Assert.Single(app.Gateway.Cancelled);
    }

    [Theory]
    [InlineData(OutlookErrorCodes.Unauthorized)]
    [InlineData(OutlookErrorCodes.Forbidden)]
    [InlineData(OutlookErrorCodes.NotFound)]
    [InlineData(OutlookErrorCodes.Throttled)]
    [InlineData(OutlookErrorCodes.Unavailable)]
    [InlineData(OutlookErrorCodes.Timeout)]
    [InlineData(OutlookErrorCodes.Rejected)]
    [InlineData(OutlookErrorCodes.NotConfigured)]
    public async Task CA16_every_failure_of_outlook_leaves_the_event_scheduled_with_its_normalized_code(string code)
    {
        await using var app = await StartAsync();
        app.Gateway.OnPublish = (_, _) => OutlookGatewayResult.Failure(code, throttled: code == OutlookErrorCodes.Throttled);
        var id = await CreateDraftAsync(app, "Con fallo");

        var outcome = await ScheduleAsync(app.Client, id);

        Assert.Equal("scheduled", outcome.Of(id).GetProperty("status").GetString());
        Assert.Equal("error", OutlookStatus(outcome.Of(id)));
        Assert.Equal(code, outcome.Of(id).GetProperty("outlook").GetProperty("errorCode").GetString());
        var detail = await GetEventAsync(app.Client, id);
        Assert.Equal("scheduled", detail.GetProperty("status").GetString());
        Assert.Equal(code, detail.GetProperty("outlook").GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task CA16_a_code_outside_the_closed_set_and_an_exception_of_the_port_never_reach_the_event_or_the_audit()
    {
        await using var app = await StartAsync();
        const string raw = "HTTP 500 {\"error\":{\"message\":\"secret-detail-from-graph\"}}";
        var unknown = await CreateDraftAsync(app, "Código desconocido");
        var thrown = await CreateDraftAsync(app, "Excepción");
        // One answer per event: a code outside the closed set for the first one, an exception of the port for the second.
        app.Gateway.OnPublish = (data, _) =>
            data.EventId == thrown ? throw new InvalidOperationException(raw) : OutlookGatewayResult.Failure(raw);

        var outcome = await ScheduleAsync(app.Client, unknown, thrown);

        Assert.Equal("scheduled", outcome.Of(unknown).GetProperty("status").GetString());
        Assert.Equal("scheduled", outcome.Of(thrown).GetProperty("status").GetString());
        Assert.Equal("graph_unavailable", outcome.Of(unknown).GetProperty("outlook").GetProperty("errorCode").GetString());
        Assert.Equal("graph_unavailable", outcome.Of(thrown).GetProperty("outlook").GetProperty("errorCode").GetString());
        Assert.DoesNotContain("secret-detail-from-graph", outcome.Json.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret-detail-from-graph", (await GetEventAsync(app.Client, unknown)).GetRawText(), StringComparison.Ordinal);
        var audit = await AuditAsync("agenda.event.outlook-failed");
        Assert.DoesNotContain(audit, row => (row.After ?? string.Empty).Contains("secret-detail-from-graph", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CA16_sync_again_after_a_failure_publishes_with_the_same_event_id_and_afterwards_updates_without_creating_again()
    {
        await using var app = await StartAsync();
        app.Gateway.OnPublish = (_, _) => OutlookGatewayResult.Failure(OutlookErrorCodes.Timeout);
        var id = await CreateDraftAsync(app, "Se reintenta");
        await ScheduleAsync(app.Client, id);
        Assert.Equal("error", OutlookStatus(await GetEventAsync(app.Client, id)));

        app.Gateway.OnPublish = ScriptedGateway.Succeed;
        var first = await ReadJsonAsync(await app.Client.PostAsync(SyncUrl(id), null, Ct));
        var second = await ReadJsonAsync(await app.Client.PostAsync(SyncUrl(id), null, Ct));

        Assert.Equal("published", OutlookStatus(first));
        Assert.Equal("published", OutlookStatus(second));
        var calls = app.Gateway.Published;
        Assert.Equal(3, calls.Count);
        Assert.All(calls, call => Assert.Equal(id, call.Data.EventId));
        Assert.Null(calls[0].OutlookEventId);
        Assert.Null(calls[1].OutlookEventId);
        Assert.Equal($"outlook-{id:N}", calls[2].OutlookEventId);
    }

    [Fact]
    public async Task CA16_a_batch_stops_on_throttling_leaves_the_rest_pending_and_each_event_is_published_once_when_synced()
    {
        await using var app = await StartAsync();
        var ids = new List<Guid>();
        for (var i = 1; i <= 3; i++)
        {
            ids.Add(await CreateDraftAsync(app, $"Lote {i}"));
        }

        app.Gateway.OnPublish = (_, _) => OutlookGatewayResult.Failure(OutlookErrorCodes.Throttled, throttled: true);
        var outcome = await ScheduleAsync(app.Client, [.. ids]);

        Assert.Single(app.Gateway.Published);
        var states = ids.Select(id => (Id: id, Outlook: outcome.Of(id).GetProperty("outlook"))).ToList();
        Assert.All(ids, id => Assert.Equal("scheduled", outcome.Of(id).GetProperty("status").GetString()));
        Assert.Single(states, state => state.Outlook.GetProperty("status").GetString() == "error"
            && state.Outlook.GetProperty("errorCode").GetString() == OutlookErrorCodes.Throttled);
        Assert.Equal(2, states.Count(state => state.Outlook.GetProperty("status").GetString() == "pending"));

        app.Gateway.OnPublish = ScriptedGateway.Succeed;
        foreach (var id in ids)
        {
            Assert.Equal("published", OutlookStatus(await ReadJsonAsync(await app.Client.PostAsync(SyncUrl(id), null, Ct))));
        }

        var succeeded = app.Gateway.Published.Skip(1).ToList();
        Assert.Equal(3, succeeded.Count);
        Assert.Equal(ids.Order(), succeeded.Select(call => call.Data.EventId).Order());
        Assert.All(succeeded, call => Assert.Null(call.OutlookEventId));
    }

    private static readonly DateTimeOffset FarNow = new(2190, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Seeds an event "as of" December 2189, scheduled unless <paramref name="draft"/>, with the Outlook state that
    /// <paramref name="outlook"/> leaves; <paramref name="changedAfterwards"/> edits it once more without telling Outlook.
    /// </summary>
    private async Task<Guid> SeedFarAsync(Guid responsible, DateTime startLocal, Action<AgendaEvent>? outlook = null, bool draft = false, bool changedAfterwards = false)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recordedAt = FarNow.AddDays(-30);
        var details = EventDetails.Create(PatchingTypeId, "Sincronización", null, responsible, null, ["example.com"], true, EventImpact.None, null, null, 1440);
        var schedule = EventSchedule.Timed(startLocal, startLocal.AddHours(1), Bogota);
        var agendaEvent = AgendaEvent.Create(details, schedule, recordedAt, responsible);
        if (!draft)
        {
            agendaEvent.Schedule(recordedAt, Bogota, responsible);
        }

        outlook?.Invoke(agendaEvent);
        if (changedAfterwards)
        {
            agendaEvent.Update(details, schedule, recordedAt.AddHours(1), Bogota, responsible);
        }

        db.AgendaEvents.Add(agendaEvent);
        await db.SaveChangesAsync(Ct);
        return agendaEvent.Id;
    }

    [Fact]
    public async Task CA17_RF19_sync_everything_publishes_the_future_scheduled_events_outlook_lacks_at_their_revision_fifty_at_a_time()
    {
        // A clock in 2190: only the events of this test are ahead of it, so the events of the other tests are never picked up.
        await using var app = await StartAsync(new FixedClock(FarNow));
        var responsible = (await UserAsync()).Id;
        var day = new DateTime(2190, 2, 1, 9, 0, 0);
        var at = FarNow.AddDays(-29);

        var notPublished = await SeedFarAsync(responsible, day); // scheduled while Outlook was off (CA-17)
        var pending = await SeedFarAsync(responsible, day.AddDays(1), e => e.MarkOutlookPending());
        var failed = await SeedFarAsync(responsible, day.AddDays(2), e => e.MarkOutlookFailed(OutlookErrorCodes.Unavailable, at));
        var stale = await SeedFarAsync(responsible, day.AddDays(3), e => e.MarkOutlookPublished("outlook-stale", null, e.Revision, at), changedAfterwards: true);
        var current = await SeedFarAsync(responsible, day.AddDays(4), e => e.MarkOutlookPublished("outlook-current", null, e.Revision, at));
        var draft = await SeedFarAsync(responsible, day.AddDays(5), draft: true);
        var started = await SeedFarAsync(responsible, new DateTime(2189, 12, 31, 9, 0, 0), e => e.MarkOutlookPending());
        var rest = new List<Guid>();
        for (var index = 0; index < 47; index++)
        {
            rest.Add(await SeedFarAsync(responsible, day.AddDays(10 + index), e => e.MarkOutlookPending()));
        }

        async Task<List<(Guid Id, string Status)>> SyncAllAsync()
        {
            var response = await app.Client.PostAsync(Url("/api/v1/agenda-events/outlook-sync"), null, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return [.. (await ReadJsonAsync(response)).GetProperty("results").EnumerateArray()
                .Select(result => (result.GetProperty("id").GetGuid(), OutlookStatus(result)))];
        }

        var first = await SyncAllAsync();

        Assert.Equal(50, first.Count);
        Assert.Equal([notPublished, pending, failed, stale], first.Take(4).Select(result => result.Id));
        Assert.Equal(rest.Take(46), first.Skip(4).Select(result => result.Id));
        Assert.All(first, result => Assert.Equal("published", result.Status));
        Assert.Equal("outlook-stale", app.Gateway.Published.Single(call => call.Data.EventId == stale).OutlookEventId);
        Assert.All(app.Gateway.Published.Where(call => call.Data.EventId != stale), call => Assert.Null(call.OutlookEventId));

        var second = await SyncAllAsync();
        Assert.Equal([rest[^1]], second.Select(result => result.Id));
        Assert.Empty(await SyncAllAsync());

        var touched = app.Gateway.Published.Select(call => call.Data.EventId).ToList();
        Assert.Equal(51, touched.Count);
        Assert.DoesNotContain(current, touched);
        Assert.DoesNotContain(draft, touched);
        Assert.DoesNotContain(started, touched);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await app.Client.PostAsync(SyncUrl(draft), null, Ct)).StatusCode);
    }
}
