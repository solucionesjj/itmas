using System.Text.Json;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Events;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;

namespace Itmas3.UnitTests.Application.Agenda;

/// <summary>SPEC-0006 CA-14 to CA-17 and CA-22 at the use-case level, with a double of the Outlook port. Nothing calls Microsoft.</summary>
public sealed class OutlookHandlerTests
{
    private readonly AgendaHarness _agenda = new(outlookEnabled: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<ScheduleEventResult[]> ScheduleAsync(params Guid[] ids) =>
        [.. await _agenda.Schedule.HandleAsync(new ScheduleEventsCommand(ids), Ct)];

    [Fact]
    public async Task CA14_scheduling_publishes_the_event_with_the_content_of_rf16()
    {
        var draft = await _agenda.CreateDraftAsync(_agenda.Input(participants: ["luis@example.com"], impact: EventImpact.Degradation));

        var results = await ScheduleAsync(draft.Id);

        var (data, existingId) = Assert.Single(_agenda.Gateway.Published);
        Assert.Null(existingId);
        Assert.Equal(draft.Id, data.EventId);
        Assert.Equal("[Mantenimiento y parches] Parches de SO", data.Subject);
        Assert.Equal("Mantenimiento y parches", data.Category);
        Assert.Equal(1440, data.ReminderMinutes);
        Assert.Equal("Remoto", data.Location);
        Assert.Equal(new DateTime(2026, 10, 24, 9, 0, 0), data.StartLocal);
        Assert.Equal(new DateTime(2026, 10, 24, 11, 0, 0), data.EndLocal);
        Assert.Equal("America/Bogota", data.TimeZoneId);
        Assert.False(data.IsAllDay);
        Assert.Equal(["ana.perez@example.com", "luis@example.com"], data.RequiredAttendees);
        Assert.Equal(["equipo@example.com"], data.OptionalAttendees);
        Assert.Contains("Aplicar los parches del mes", data.BodyText, StringComparison.Ordinal);
        Assert.Contains("Alcance: Servidores Windows", data.BodyText, StringComparison.Ordinal);
        Assert.Contains("Impacto: Degradación del servicio", data.BodyText, StringComparison.Ordinal);
        Assert.Contains($"https://itmas.example.com/agenda/events/{draft.Id}", data.BodyText, StringComparison.Ordinal);

        Assert.Equal(OutlookStatus.Published, results[0].Outlook.Status);
        Assert.Equal(OutlookStatus.Published, draft.OutlookStatus);
        Assert.NotNull(draft.OutlookEventId);
        Assert.NotNull(draft.OutlookWebLink);
        Assert.Equal(draft.Revision, draft.OutlookSyncedRevision);
        Assert.Contains(_agenda.UnitOfWork.Entries, entry => entry.Action == AgendaAuditActions.EventOutlookPublished);
    }

    [Fact]
    public async Task CA14_the_team_is_invited_only_when_the_event_asks_for_it_and_an_address_is_configured()
    {
        var noTeam = await _agenda.CreateDraftAsync(_agenda.Input(title: "Sin equipo") with { NotifyTeam = false });
        await ScheduleAsync(noTeam.Id);
        Assert.Empty(_agenda.Gateway.Published[0].Data.OptionalAttendees);

        var withoutAddress = new AgendaHarness(outlookEnabled: true, teamAddress: null);
        var draft = await withoutAddress.CreateDraftAsync();
        await withoutAddress.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);
        Assert.Empty(withoutAddress.Gateway.Published[0].Data.OptionalAttendees);
    }

    [Fact]
    public async Task CA14_invitees_are_not_repeated_between_the_responsible_the_participants_and_the_team()
    {
        var draft = await _agenda.CreateDraftAsync(_agenda.Input(participants: ["ANA.PEREZ@example.com", "equipo@example.com"]));

        await ScheduleAsync(draft.Id);

        var data = _agenda.Gateway.Published.Single().Data;
        Assert.Equal(["ana.perez@example.com", "equipo@example.com"], data.RequiredAttendees);
        Assert.Empty(data.OptionalAttendees);
    }

    [Fact]
    public async Task CA22_text_with_markup_is_sent_literally_as_plain_text()
    {
        var input = _agenda.Input(title: "<b>Parches</b> <script>alert(1)</script>") with { Description = "<img src=x onerror=alert(1)>" };
        var draft = await _agenda.CreateDraftAsync(input);

        await ScheduleAsync(draft.Id);

        var data = _agenda.Gateway.Published.Single().Data;
        Assert.Equal("[Mantenimiento y parches] <b>Parches</b> <script>alert(1)</script>", data.Subject);
        Assert.StartsWith("<img src=x onerror=alert(1)>", data.BodyText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CA15_modifying_a_scheduled_event_updates_the_outlook_event_with_its_saved_id()
    {
        var scheduled = await _agenda.CreateScheduledAsync();
        var outlookId = scheduled.OutlookEventId;

        var result = await _agenda.Update.HandleAsync(
            new UpdateEventCommand(scheduled.Id, scheduled.Revision, _agenda.Input(title: "Parches (nueva fecha)", start: new DateTime(2026, 10, 27, 9, 0, 0))), Ct);

        Assert.Equal(2, _agenda.Gateway.Published.Count);
        var (data, existingId) = _agenda.Gateway.Published[1];
        Assert.Equal(outlookId, existingId);
        Assert.Equal("[Mantenimiento y parches] Parches (nueva fecha)", data.Subject);
        Assert.Equal(new DateTime(2026, 10, 27, 9, 0, 0), data.StartLocal);
        Assert.Equal(OutlookStatus.Published, result.Outlook.Status);
        Assert.Equal(scheduled.Revision, scheduled.OutlookSyncedRevision);
    }

    [Fact]
    public async Task CA15_modifying_a_draft_never_calls_outlook()
    {
        var draft = await _agenda.CreateDraftAsync();

        await _agenda.Update.HandleAsync(new UpdateEventCommand(draft.Id, draft.Revision, _agenda.Input(title: "Otro")), Ct);

        Assert.Equal(0, _agenda.Gateway.Calls);
    }

    [Fact]
    public async Task CA15_cancelling_a_published_event_cancels_it_in_outlook_with_the_reason()
    {
        var scheduled = await _agenda.CreateScheduledAsync();
        var outlookId = scheduled.OutlookEventId!;

        var result = await _agenda.Cancel.HandleAsync(new CancelEventCommand(scheduled.Id, "Se pospone por el cierre"), Ct);

        Assert.Equal((outlookId, "Se pospone por el cierre"), Assert.Single(_agenda.Gateway.Cancelled));
        Assert.Equal(EventStatus.Cancelled, result.Status);
        Assert.Equal(OutlookStatus.Published, result.Outlook.Status);
        Assert.False(scheduled.NeedsOutlookCancellation);
    }

    [Fact]
    public async Task CA15_cancelling_an_event_that_was_never_published_does_not_call_outlook()
    {
        // Scheduled through the domain, as an event scheduled while Outlook was off: it has no Outlook id.
        var unpublished = AgendaEvent.Create(
            EventDetails.Create(_agenda.DefaultType.Id, "Sin publicar", null, _agenda.Responsible.Id, null, ["example.com"], true, EventImpact.None, null, null, 0),
            EventSchedule.Timed(new DateTime(2026, 10, 28, 9, 0, 0), new DateTime(2026, 10, 28, 10, 0, 0), _agenda.Settings.TimeZone),
            AgendaHarness.Now, AgendaHarness.CurrentUserId);
        unpublished.Schedule(AgendaHarness.Now, _agenda.Settings.TimeZone, AgendaHarness.CurrentUserId);
        _agenda.Events.Items.Add(unpublished);
        var calls = _agenda.Gateway.Calls;

        var result = await _agenda.Cancel.HandleAsync(new CancelEventCommand(unpublished.Id, "No se publicó"), Ct);

        Assert.Equal(calls, _agenda.Gateway.Calls);
        Assert.Equal(EventStatus.Cancelled, result.Status);
        Assert.Null(unpublished.OutlookEventId);
    }

    [Theory]
    [InlineData(OutlookErrorCodes.Unauthorized)]
    [InlineData(OutlookErrorCodes.Forbidden)]
    [InlineData(OutlookErrorCodes.Unavailable)]
    [InlineData(OutlookErrorCodes.Rejected)]
    public async Task CA16_a_failure_of_outlook_leaves_the_event_scheduled_with_a_normalized_code(string code)
    {
        _agenda.Gateway.Enqueue(OutlookGatewayResult.Failure(code));
        var draft = await _agenda.CreateDraftAsync();

        var results = await ScheduleAsync(draft.Id);

        Assert.Equal(EventStatus.Scheduled, draft.Status);
        Assert.Equal(EventStatus.Scheduled, results[0].Status);
        Assert.Equal(OutlookStatus.Error, results[0].Outlook.Status);
        Assert.Equal(code, results[0].Outlook.ErrorCode);
        Assert.Equal(code, draft.OutlookErrorCode);
        Assert.Null(draft.OutlookEventId);
        var entry = _agenda.UnitOfWork.Entries.Single(e => e.Action == AgendaAuditActions.EventOutlookFailed);
        Assert.Contains(code, JsonSerializer.Serialize(entry.After), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CA16_an_unknown_error_code_is_normalized_and_raw_detail_never_reaches_the_event_or_the_audit()
    {
        _agenda.Gateway.Enqueue(OutlookGatewayResult.Failure("{\"error\":{\"message\":\"buzon secreto@contoso.com\"}}"));
        var draft = await _agenda.CreateDraftAsync();

        await ScheduleAsync(draft.Id);

        Assert.Equal(OutlookErrorCodes.Unavailable, draft.OutlookErrorCode);
        Assert.DoesNotContain("contoso", JsonSerializer.Serialize(_agenda.UnitOfWork.Entries), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CA16_an_exception_from_the_gateway_never_fails_the_operation_and_leaks_nothing()
    {
        _agenda.Gateway.ThrowOnCall = new HttpRequestException("secret-detail-from-graph");
        var draft = await _agenda.CreateDraftAsync();

        var results = await ScheduleAsync(draft.Id);

        Assert.Equal(EventStatus.Scheduled, draft.Status);
        Assert.Equal(OutlookStatus.Error, results[0].Outlook.Status);
        Assert.Equal(OutlookErrorCodes.Unavailable, results[0].Outlook.ErrorCode);
        Assert.DoesNotContain("secret-detail", JsonSerializer.Serialize(_agenda.UnitOfWork.Entries), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CA16_a_timeout_is_reported_as_graph_timeout()
    {
        _agenda.Gateway.ThrowOnCall = new TaskCanceledException("timeout");
        var draft = await _agenda.CreateDraftAsync();

        var results = await ScheduleAsync(draft.Id);

        Assert.Equal(OutlookErrorCodes.Timeout, results[0].Outlook.ErrorCode);
        Assert.Equal(EventStatus.Scheduled, draft.Status);
    }

    [Fact]
    public async Task CA16_a_throttled_call_stops_the_batch_and_the_rest_stay_pending_without_calling_outlook()
    {
        var a = await _agenda.CreateDraftAsync(_agenda.Input(title: "A"));
        var b = await _agenda.CreateDraftAsync(_agenda.Input(title: "B"));
        var c = await _agenda.CreateDraftAsync(_agenda.Input(title: "C"));
        _agenda.Gateway.Enqueue(
            OutlookGatewayResult.Success("AAMk-a", null),
            OutlookGatewayResult.Failure(OutlookErrorCodes.Throttled, throttled: true));

        var results = await ScheduleAsync(a.Id, b.Id, c.Id);

        Assert.Equal(2, _agenda.Gateway.Calls);
        Assert.Equal(OutlookStatus.Published, results[0].Outlook.Status);
        Assert.Equal(OutlookStatus.Error, results[1].Outlook.Status);
        Assert.Equal(OutlookErrorCodes.Throttled, results[1].Outlook.ErrorCode);
        Assert.Equal(OutlookStatus.Pending, results[2].Outlook.Status);
        Assert.All(results, result => Assert.Equal(EventStatus.Scheduled, result.Status));
        Assert.Equal(OutlookStatus.Pending, c.OutlookStatus);
        Assert.True(c.NeedsOutlookSync);
    }

    [Fact]
    public async Task CA16_sync_again_publishes_without_duplicating_it_uses_the_same_transaction_id()
    {
        _agenda.Gateway.Enqueue(OutlookGatewayResult.Failure(OutlookErrorCodes.Unavailable));
        var draft = await _agenda.CreateDraftAsync();
        await ScheduleAsync(draft.Id);

        var sync = await _agenda.Sync.SyncOneAsync(draft.Id, Ct);

        Assert.Equal(OutlookStatus.Published, sync.Outlook.Status);
        Assert.Equal(2, _agenda.Gateway.Published.Count);
        Assert.All(_agenda.Gateway.Published, call => Assert.Equal(draft.Id, call.Data.EventId));
        Assert.Null(_agenda.Gateway.Published[1].OutlookEventId); // nothing was created the first time
        Assert.Equal(OutlookStatus.Published, draft.OutlookStatus);
        Assert.Null(draft.OutlookErrorCode);
    }

    [Fact]
    public async Task CA16_sync_again_of_a_published_event_updates_it_with_the_saved_id()
    {
        var scheduled = await _agenda.CreateScheduledAsync();
        var outlookId = scheduled.OutlookEventId;

        await _agenda.Sync.SyncOneAsync(scheduled.Id, Ct);

        Assert.Equal(outlookId, _agenda.Gateway.Published[^1].OutlookEventId);
        Assert.Equal(OutlookStatus.Published, scheduled.OutlookStatus);
    }

    [Fact]
    public async Task CA16_when_the_event_was_deleted_in_outlook_the_adapter_creates_it_again_and_the_new_id_is_kept()
    {
        var scheduled = await _agenda.CreateScheduledAsync();
        _agenda.Gateway.Enqueue(OutlookGatewayResult.Success("AAMk-recreated", "https://outlook.example/new"));

        await _agenda.Sync.SyncOneAsync(scheduled.Id, Ct);

        Assert.Equal("AAMk-recreated", scheduled.OutlookEventId);
        Assert.Equal("https://outlook.example/new", scheduled.OutlookWebLink);
    }

    [Fact]
    public async Task CA16_sync_again_retries_a_cancellation_that_failed()
    {
        var scheduled = await _agenda.CreateScheduledAsync();
        _agenda.Gateway.Enqueue(OutlookGatewayResult.Failure(OutlookErrorCodes.Unavailable));
        await _agenda.Cancel.HandleAsync(new CancelEventCommand(scheduled.Id, "Se pospone"), Ct);
        Assert.Equal(OutlookStatus.Error, scheduled.OutlookStatus);
        Assert.True(scheduled.NeedsOutlookCancellation);

        var sync = await _agenda.Sync.SyncOneAsync(scheduled.Id, Ct);

        Assert.Equal(OutlookStatus.Published, sync.Outlook.Status);
        Assert.Equal(2, _agenda.Gateway.Cancelled.Count);
        Assert.All(_agenda.Gateway.Cancelled, call => Assert.Equal("Se pospone", call.Reason));
        Assert.False(scheduled.NeedsOutlookCancellation);
    }

    [Fact]
    public async Task CA16_only_scheduled_events_or_pending_cancellations_can_be_synced()
    {
        var draft = await _agenda.CreateDraftAsync();
        var cancelled = await _agenda.CreateScheduledAsync(_agenda.Input(title: "X", start: new DateTime(2026, 10, 26, 9, 0, 0)));
        await _agenda.Cancel.HandleAsync(new CancelEventCommand(cancelled.Id, "motivo"), Ct);

        await Assert.ThrowsAsync<DomainException>(() => _agenda.Sync.SyncOneAsync(draft.Id, Ct));
        await Assert.ThrowsAsync<DomainException>(() => _agenda.Sync.SyncOneAsync(cancelled.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => _agenda.Sync.SyncOneAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task CA16_syncing_everything_retries_future_events_that_are_pending_in_error_or_not_published()
    {
        _agenda.Gateway.Enqueue(
            OutlookGatewayResult.Failure(OutlookErrorCodes.Unavailable),
            OutlookGatewayResult.Failure(OutlookErrorCodes.Forbidden));
        var failedA = await _agenda.CreateDraftAsync(_agenda.Input(title: "A"));
        var failedB = await _agenda.CreateDraftAsync(_agenda.Input(title: "B", start: new DateTime(2026, 10, 26, 9, 0, 0)));
        await ScheduleAsync(failedA.Id, failedB.Id);
        var healthy = await _agenda.CreateScheduledAsync(_agenda.Input(title: "OK", start: new DateTime(2026, 10, 27, 9, 0, 0)));
        var calls = _agenda.Gateway.Calls;

        var results = await _agenda.Sync.SyncPendingAsync(Ct);

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(OutlookStatus.Published, result.Outlook.Status));
        Assert.Equal(calls + 2, _agenda.Gateway.Calls);
        Assert.DoesNotContain(results, result => result.Id == healthy.Id);
    }

    [Fact]
    public async Task CA16_syncing_everything_stops_on_throttling()
    {
        var events = new List<AgendaEvent>();
        for (var day = 24; day < 27; day++)
        {
            events.Add(await _agenda.CreateDraftAsync(_agenda.Input(title: $"E{day}", start: new DateTime(2026, 10, day, 9, 0, 0))));
        }

        _agenda.Gateway.Enqueue(
            OutlookGatewayResult.Failure(OutlookErrorCodes.Unavailable),
            OutlookGatewayResult.Failure(OutlookErrorCodes.Unavailable),
            OutlookGatewayResult.Failure(OutlookErrorCodes.Unavailable));
        await ScheduleAsync([.. events.Select(e => e.Id)]);
        _agenda.Gateway.Enqueue(OutlookGatewayResult.Failure(OutlookErrorCodes.Throttled, throttled: true));
        var calls = _agenda.Gateway.Calls;

        var results = await _agenda.Sync.SyncPendingAsync(Ct);

        Assert.Equal(calls + 1, _agenda.Gateway.Calls);
        Assert.Equal(OutlookStatus.Error, results[0].Outlook.Status);
        Assert.All(results.Skip(1), result => Assert.Equal(OutlookStatus.Pending, result.Outlook.Status));
    }

    [Fact]
    public async Task CA17_with_outlook_off_everything_works_without_external_calls_and_nothing_is_published()
    {
        var off = new AgendaHarness(outlookEnabled: false);
        var draft = await off.CreateDraftAsync();
        var results = await off.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);
        await off.Update.HandleAsync(new UpdateEventCommand(draft.Id, draft.Revision, off.Input(title: "Otro")), Ct);
        await off.Cancel.HandleAsync(new CancelEventCommand(draft.Id, "motivo"), Ct);

        Assert.Equal(OutlookStatus.NotPublished, results[0].Outlook.Status);
        Assert.Equal(OutlookStatus.NotPublished, draft.OutlookStatus);
        Assert.Equal(0, off.Gateway.Calls);
        Assert.DoesNotContain(off.UnitOfWork.Entries, entry => entry.Action.StartsWith("agenda.event.outlook", StringComparison.Ordinal));

        var scheduled = await off.CreateScheduledAsync(off.Input(title: "Otra", start: new DateTime(2026, 10, 28, 9, 0, 0)));
        var sync = await off.Sync.SyncOneAsync(scheduled.Id, Ct);
        Assert.Equal(OutlookStatus.NotPublished, sync.Outlook.Status);
        Assert.Equal(OutlookErrorCodes.NotConfigured, sync.Outlook.ErrorCode);
        Assert.Empty(await off.Sync.SyncPendingAsync(Ct));
        Assert.Equal(0, off.Gateway.Calls);
    }

    [Fact]
    public async Task CA17_events_scheduled_before_outlook_was_enabled_are_published_by_syncing_again()
    {
        // Scheduled through the domain, as events scheduled while Outlook was off.
        var legacy = AgendaEvent.Create(
            EventDetails.Create(_agenda.DefaultType.Id, "Anterior", null, _agenda.Responsible.Id, null, ["example.com"], true, EventImpact.None, null, null, 60),
            EventSchedule.Timed(new DateTime(2026, 10, 29, 9, 0, 0), new DateTime(2026, 10, 29, 10, 0, 0), _agenda.Settings.TimeZone),
            AgendaHarness.Now, AgendaHarness.CurrentUserId);
        legacy.Schedule(AgendaHarness.Now, _agenda.Settings.TimeZone, AgendaHarness.CurrentUserId);
        _agenda.Events.Items.Add(legacy);
        Assert.Equal(OutlookStatus.NotPublished, legacy.OutlookStatus);

        var results = await _agenda.Sync.SyncPendingAsync(Ct);

        Assert.Equal(legacy.Id, Assert.Single(results).Id);
        Assert.Equal(OutlookStatus.Published, legacy.OutlookStatus);
    }

    [Fact]
    public async Task Events_that_already_started_are_not_part_of_the_bulk_sync()
    {
        var scheduled = await _agenda.CreateScheduledAsync();
        _agenda.Gateway.Enqueue(OutlookGatewayResult.Failure(OutlookErrorCodes.Unavailable));
        await _agenda.Update.HandleAsync(new UpdateEventCommand(scheduled.Id, scheduled.Revision, _agenda.Input(title: "Cambio")), Ct);
        _agenda.Clock.Set(scheduled.StartsAtUtc.AddMinutes(1));

        Assert.Empty(await _agenda.Sync.SyncPendingAsync(Ct));
    }
}
