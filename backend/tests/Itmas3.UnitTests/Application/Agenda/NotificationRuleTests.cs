using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Agenda.Events;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Application.Agenda.Series;
using Itmas3.Application.Agenda.Types;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Agenda;

namespace Itmas3.UnitTests.Application.Agenda;

/// <summary>
/// The fixes of the reviews of 2026-10-21 at the use-case level: changing what is already scheduled needs <c>agenda.publish</c> and
/// spends the budget of notices (F-2, P-12, P-26); the responsible is invited only from an allowed domain (F-4, RN-5); a change
/// while Outlook is still creating the event neither leaves the invitation alive nor creates it twice (H1); an omitted
/// <c>isActive</c> keeps the type as it is (H9).
/// </summary>
public sealed class NotificationRuleTests
{
    private readonly AgendaHarness _agenda = new(outlookEnabled: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<AgendaCommandResult> RenameAsync(AgendaHarness agenda, AgendaEvent agendaEvent, string title) =>
        agenda.Update.HandleAsync(new UpdateEventCommand(agendaEvent.Id, agendaEvent.Revision, agenda.Input(title: title)), Ct);

    private static async Task<Guid> CreateWeeklySeriesAsync(AgendaHarness agenda)
    {
        var result = await agenda.CreateSeries.HandleAsync(
            new CreateSeriesCommand(
                agenda.Input(title: "Parches", start: new DateTime(2026, 10, 24, 9, 0, 0)),
                new SeriesRuleInput(RecurrenceUnit.Week, 1, null, null, 3), DryRun: false),
            Ct);
        return result.SeriesId!.Value;
    }

    private static UpdateSeriesCommand RenameSeries(AgendaHarness agenda, Guid seriesId, string title) =>
        new(seriesId, new SeriesCommonInput(agenda.DefaultType.Id, title, null, agenda.Responsible.Id, null, EventImpact.None, "Todo", "Remoto", 30, null));

    [Fact]
    public async Task F2_changing_a_scheduled_event_without_agenda_publish_is_forbidden_and_changes_nothing()
    {
        var scheduled = await _agenda.CreateScheduledAsync();
        var revision = scheduled.Revision;
        var entries = _agenda.UnitOfWork.Entries.Count;
        var calls = _agenda.Gateway.Calls;
        _agenda.Permissions.Denied.Add(AgendaPermissions.Publish);

        await Assert.ThrowsAsync<ForbiddenException>(() => RenameAsync(_agenda, scheduled, "Parches (editado)"));

        Assert.Equal("Parches de SO", scheduled.Title);
        Assert.Equal(revision, scheduled.Revision);
        Assert.Equal(entries, _agenda.UnitOfWork.Entries.Count);
        Assert.Equal(calls, _agenda.Gateway.Calls);
        Assert.Equal(0, _agenda.PublishLimit.Taken);
    }

    [Fact]
    public async Task F2_changing_a_scheduled_event_with_agenda_publish_spends_one_notice_of_the_budget()
    {
        var scheduled = await _agenda.CreateScheduledAsync();

        await RenameAsync(_agenda, scheduled, "Parches (editado)");

        Assert.Equal("Parches (editado)", scheduled.Title);
        Assert.Equal(1, _agenda.PublishLimit.Taken);
        Assert.Equal(2, _agenda.Gateway.Published.Count);
    }

    [Fact]
    public async Task F2_with_the_budget_spent_a_scheduled_event_is_not_changed_and_the_answer_says_when_to_retry()
    {
        var scheduled = await _agenda.CreateScheduledAsync();
        var calls = _agenda.Gateway.Calls;
        _agenda.PublishLimit.Remaining = 0;

        var error = await Assert.ThrowsAsync<TooManyRequestsException>(() => RenameAsync(_agenda, scheduled, "Parches (editado)"));

        Assert.Equal(TimeSpan.FromMinutes(30), error.RetryAfter);
        Assert.Equal("Parches de SO", scheduled.Title);
        Assert.Equal(calls, _agenda.Gateway.Calls);
    }

    [Fact]
    public async Task F2_a_draft_notifies_nobody_so_it_needs_only_agenda_update_and_spends_nothing()
    {
        var draft = await _agenda.CreateDraftAsync();
        _agenda.Permissions.Denied.Add(AgendaPermissions.Publish);
        _agenda.PublishLimit.Remaining = 0;

        await RenameAsync(_agenda, draft, "Parches (editado)");

        Assert.Equal("Parches (editado)", draft.Title);
        Assert.Equal(0, _agenda.PublishLimit.Taken);
        Assert.Equal(0, _agenda.Gateway.Calls);
    }

    [Fact]
    public async Task F2_a_series_with_scheduled_occurrences_needs_agenda_publish_and_changes_nothing_without_it()
    {
        var seriesId = await CreateWeeklySeriesAsync(_agenda);
        var first = _agenda.Events.Items.OrderBy(e => e.StartsAtUtc).First();
        await _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([first.Id]), Ct);
        var entries = _agenda.UnitOfWork.Entries.Count;
        var calls = _agenda.Gateway.Calls;
        _agenda.Permissions.Denied.Add(AgendaPermissions.Publish);

        await Assert.ThrowsAsync<ForbiddenException>(() => _agenda.UpdateSeries.HandleAsync(RenameSeries(_agenda, seriesId, "Parches 2026"), Ct));

        Assert.Equal(entries, _agenda.UnitOfWork.Entries.Count);
        Assert.Equal(calls, _agenda.Gateway.Calls);
        Assert.Equal(0, _agenda.PublishLimit.Taken);
    }

    [Fact]
    public async Task F2_a_series_spends_one_notice_however_many_scheduled_occurrences_it_updates()
    {
        var seriesId = await CreateWeeklySeriesAsync(_agenda);
        await _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([.. _agenda.Events.Items.Select(e => e.Id)]), Ct);

        var result = await _agenda.UpdateSeries.HandleAsync(RenameSeries(_agenda, seriesId, "Parches 2026"), Ct);

        Assert.Equal(3, result.Updated);
        Assert.Equal(1, _agenda.PublishLimit.Taken);
        Assert.Equal(6, _agenda.Gateway.Published.Count);
    }

    [Fact]
    public async Task F2_a_series_of_drafts_needs_only_agenda_update()
    {
        var seriesId = await CreateWeeklySeriesAsync(_agenda);
        _agenda.Permissions.Denied.Add(AgendaPermissions.Publish);

        var result = await _agenda.UpdateSeries.HandleAsync(RenameSeries(_agenda, seriesId, "Parches 2026"), Ct);

        Assert.Equal(3, result.Updated);
        Assert.All(_agenda.Events.Items, e => Assert.Equal("Parches 2026", e.Title));
        Assert.Equal(0, _agenda.PublishLimit.Taken);
    }

    [Fact]
    public async Task F4_a_responsible_outside_the_allowed_domains_is_not_invited_and_the_participants_still_are()
    {
        var guest = new AgendaUser(Guid.NewGuid(), "Invitado externo", "invitado@partner.example.org", true);
        _agenda.Directory.Users[guest.Id] = guest;
        var draft = await _agenda.CreateDraftAsync(_agenda.Input(responsible: guest.Id, participants: ["luis@example.com"]));

        await _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);

        var data = Assert.Single(_agenda.Gateway.Published).Data;
        Assert.Equal(["luis@example.com"], data.RequiredAttendees);
        Assert.Equal(guest.Id, draft.ResponsibleUserId);
    }

    [Fact]
    public async Task H1_cancelling_while_outlook_creates_the_event_sends_the_cancellation_with_the_new_id()
    {
        var gateway = new HeldOutlookGateway();
        var agenda = new AgendaHarness(outlookEnabled: true, gateway: gateway);
        var draft = await agenda.CreateDraftAsync();

        var scheduling = agenda.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);
        await agenda.Cancel.HandleAsync(new CancelEventCommand(draft.Id, "Se pospone"), Ct);
        Assert.Empty(gateway.Cancelled); // no Outlook id yet: nothing could be cancelled
        gateway.Release();
        await scheduling;

        var (outlookEventId, reason) = Assert.Single(gateway.Cancelled);
        Assert.Equal($"AAMk-{draft.Id:N}", outlookEventId);
        Assert.Equal("Se pospone", reason);
        Assert.Single(gateway.Published);
        Assert.Equal(EventStatus.Cancelled, draft.Status);
        Assert.Equal(OutlookStatus.Published, draft.OutlookStatus);
        Assert.False(draft.NeedsOutlookCancellation);
    }

    [Fact]
    public async Task H1_editing_while_outlook_creates_the_event_does_not_create_it_twice_and_the_next_sync_sends_the_change()
    {
        var gateway = new HeldOutlookGateway();
        var agenda = new AgendaHarness(outlookEnabled: true, gateway: gateway);
        var draft = await agenda.CreateDraftAsync();

        var scheduling = agenda.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);
        var edited = await RenameAsync(agenda, draft, "Parches (editado)");
        Assert.Equal(OutlookStatus.Pending, edited.Outlook.Status);
        Assert.Single(gateway.Published);
        gateway.Release();
        await scheduling;

        Assert.Single(gateway.Published);
        Assert.Equal(OutlookStatus.Pending, draft.OutlookStatus); // created with the revision before the edit
        Assert.True(draft.NeedsOutlookSync);

        await agenda.Sync.SyncOneAsync(draft.Id, Ct);

        Assert.Equal(2, gateway.Published.Count);
        Assert.Equal($"AAMk-{draft.Id:N}", gateway.Published[1].OutlookEventId);
        Assert.Equal("[Mantenimiento y parches] Parches (editado)", gateway.Published[1].Data.Subject);
        Assert.Equal(OutlookStatus.Published, draft.OutlookStatus);
    }

    [Fact]
    public async Task H9_an_omitted_is_active_keeps_the_type_as_it_is()
    {
        var input = new EventTypeInput("Mantenimiento y parches", "Ventanas mensuales", 120, 1440, false);
        await _agenda.UpdateType.HandleAsync(
            new UpdateEventTypeCommand(_agenda.DefaultType.Id, _agenda.DefaultType.Revision, input, IsActive: false), Ct);

        var updated = await _agenda.UpdateType.HandleAsync(
            new UpdateEventTypeCommand(_agenda.DefaultType.Id, _agenda.DefaultType.Revision, input with { DefaultDurationMinutes = 90 }, IsActive: null), Ct);

        Assert.False(updated.IsActive);
        Assert.Equal(90, updated.DefaultDurationMinutes);

        var active = await _agenda.CreateType.HandleAsync(new EventTypeInput("Hardening", null, 60, 0, false), Ct);
        var kept = await _agenda.UpdateType.HandleAsync(
            new UpdateEventTypeCommand(active.Id, active.Revision, new EventTypeInput("Hardening de servidores", null, 60, 0, false), IsActive: null), Ct);
        Assert.True(kept.IsActive);
    }

    /// <summary>
    /// Holds the first creation until the test releases it, as if Graph were still answering; everything else answers at once.
    /// The answers are the ones of <see cref="FakeOutlookGateway"/>.
    /// </summary>
    private sealed class HeldOutlookGateway : IOutlookCalendarGateway
    {
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<(OutlookEventData Data, string? OutlookEventId)> Published { get; } = [];

        public List<(string OutlookEventId, string Reason)> Cancelled { get; } = [];

        public void Release() => _released.TrySetResult();

        public async Task<OutlookGatewayResult> PublishAsync(OutlookEventData data, string? outlookEventId, CancellationToken cancellationToken)
        {
            Published.Add((data, outlookEventId));
            if (Published.Count == 1)
            {
                await _released.Task.WaitAsync(cancellationToken);
            }

            return OutlookGatewayResult.Success(outlookEventId ?? $"AAMk-{data.EventId:N}", $"https://outlook.example/{data.EventId:N}");
        }

        public Task<OutlookGatewayResult> CancelAsync(string outlookEventId, string reason, CancellationToken cancellationToken)
        {
            Cancelled.Add((outlookEventId, reason));
            return Task.FromResult(OutlookGatewayResult.Success(outlookEventId, null));
        }
    }
}
