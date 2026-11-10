using System.Text.Json;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Events;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;

namespace Itmas3.UnitTests.Application.Agenda;

/// <summary>
/// SPEC-0008 CA-8 at the use-case level with the SMTP transport (ADR-0023 decisions 5 to 7 and 11): the write-ahead, the second write
/// that only removes, the batch that stops or runs out of time, and the expiry. Nothing sends e-mail: the port is a double.
/// </summary>
public sealed class SmtpPublishingTests
{
    private readonly AgendaHarness _agenda = new(outlookEnabled: true, transport: OutlookTransport.Smtp);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<AgendaEvent> ScheduledAsync(IReadOnlyList<string>? participants = null, string title = "Parches de SO")
    {
        var draft = await _agenda.CreateDraftAsync(_agenda.Input(title: title, participants: participants));
        await _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);
        return draft;
    }

    private Task UpdateAsync(AgendaEvent agendaEvent, IReadOnlyList<string>? participants) =>
        _agenda.Update.HandleAsync(new UpdateEventCommand(agendaEvent.Id, agendaEvent.Revision, _agenda.Input(participants: participants)), Ct);

    [Fact]
    public async Task Scheduling_writes_ahead_the_uid_and_the_recipients_and_sends_the_sequence_and_the_instants()
    {
        var agendaEvent = await ScheduledAsync(["Luis@example.com"]);

        var uid = InvitationWriter.UidOf(agendaEvent.Id);
        var (data, outlookEventId) = Assert.Single(_agenda.Gateway.Published);
        Assert.Equal(uid, outlookEventId);
        Assert.Equal(uid, agendaEvent.OutlookEventId);
        Assert.Equal(["ana.perez@example.com", "equipo@example.com", "luis@example.com"], agendaEvent.OutlookSentRecipients);
        Assert.Equal((2 * agendaEvent.Revision) + 1, data.Sequence);
        Assert.Equal(agendaEvent.StartsAtUtc, data.StartUtc);
        Assert.Equal(agendaEvent.EndsAtUtc, data.EndUtc);
        Assert.Empty(data.RemovedRecipients);
        Assert.Equal(AgendaHarness.Now + OutlookPublisher.SmtpBudget, data.Deadline);
        Assert.Equal(OutlookStatus.Published, agendaEvent.OutlookStatus);
    }

    [Fact]
    public async Task Whoever_is_removed_gets_the_cancellation_and_leaves_the_list_once_the_server_accepts_it()
    {
        var agendaEvent = await ScheduledAsync(["luis@example.com"]);
        var uid = agendaEvent.OutlookEventId!;
        _agenda.Gateway.Enqueue(OutlookGatewayResult.Success(uid, null) with { ConfirmedRemovals = ["luis@example.com"] });

        await UpdateAsync(agendaEvent, participants: null);

        var data = _agenda.Gateway.Published[1].Data;
        Assert.Equal(["luis@example.com"], data.RemovedRecipients);
        Assert.DoesNotContain("luis@example.com", data.RequiredAttendees);
        Assert.Equal(["ana.perez@example.com", "equipo@example.com"], agendaEvent.OutlookSentRecipients);
    }

    [Fact]
    public async Task Without_a_confirmed_cancellation_the_removed_person_stays_in_the_list()
    {
        var agendaEvent = await ScheduledAsync(["luis@example.com"]);
        _agenda.Gateway.Enqueue(OutlookGatewayResult.Failure(OutlookErrorCodes.SmtpUnavailable));

        await UpdateAsync(agendaEvent, participants: null);

        Assert.Contains("luis@example.com", agendaEvent.OutlookSentRecipients!);
        Assert.Equal(OutlookStatus.Error, agendaEvent.OutlookStatus);
        Assert.Equal(OutlookErrorCodes.SmtpUnavailable, agendaEvent.OutlookErrorCode);
    }

    [Fact]
    public async Task A_permanent_rejection_only_forgets_the_addresses_this_send_added()
    {
        var agendaEvent = await ScheduledAsync(["luis@example.com"]);
        _agenda.Gateway.Enqueue(OutlookGatewayResult.Success(agendaEvent.OutlookEventId!, null) with
        {
            RejectedRecipients = ["marta@example.com", "luis@example.com"],
        });

        await UpdateAsync(agendaEvent, participants: ["luis@example.com", "marta@example.com"]);

        Assert.Contains("luis@example.com", agendaEvent.OutlookSentRecipients!);
        Assert.DoesNotContain("marta@example.com", agendaEvent.OutlookSentRecipients!);
    }

    [Fact]
    public async Task Not_attempted_leaves_the_event_pending_without_an_error_and_keeps_the_partial_cancellation()
    {
        var agendaEvent = await ScheduledAsync(["luis@example.com"]);
        _agenda.Gateway.Enqueue(OutlookGatewayResult.Postponed(agendaEvent.OutlookEventId) with { ConfirmedRemovals = ["luis@example.com"] });

        await UpdateAsync(agendaEvent, participants: null);

        Assert.Equal(OutlookStatus.Pending, agendaEvent.OutlookStatus);
        Assert.Null(agendaEvent.OutlookErrorCode);
        Assert.DoesNotContain("luis@example.com", agendaEvent.OutlookSentRecipients!);
    }

    [Fact]
    public async Task A_refused_authentication_stops_the_batch_and_the_rest_stays_pending_without_calling_again()
    {
        var first = await _agenda.CreateDraftAsync(_agenda.Input(title: "Uno"));
        var second = await _agenda.CreateDraftAsync(_agenda.Input(title: "Dos", start: new DateTime(2026, 10, 25, 9, 0, 0)));
        _agenda.Gateway.Enqueue(OutlookGatewayResult.Failure(OutlookErrorCodes.SmtpAuthFailed) with { StopBatch = true });

        var results = await _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([first.Id, second.Id]), Ct);

        Assert.Equal(1, _agenda.Gateway.Calls);
        Assert.Equal(OutlookErrorCodes.SmtpAuthFailed, results[0].Outlook.ErrorCode);
        Assert.Equal(OutlookStatus.Pending, results[1].Outlook.Status);
        Assert.Equal(EventStatus.Scheduled, second.Status);
    }

    [Fact]
    public async Task Past_the_budget_no_new_event_is_started_and_it_stays_pending()
    {
        var gateway = new ClockGateway(_agenda);
        var agenda = new AgendaHarness(outlookEnabled: true, transport: OutlookTransport.Smtp, gateway: gateway);
        gateway.Agenda = agenda;
        var first = await agenda.CreateDraftAsync(agenda.Input(title: "Uno"));
        var second = await agenda.CreateDraftAsync(agenda.Input(title: "Dos", start: new DateTime(2026, 10, 25, 9, 0, 0)));
        gateway.AdvanceBy = OutlookPublisher.SmtpBudget + TimeSpan.FromSeconds(1);

        var results = await agenda.Schedule.HandleAsync(new ScheduleEventsCommand([first.Id, second.Id]), Ct);

        Assert.Equal(1, gateway.Calls);
        Assert.Equal(OutlookStatus.Published, results[0].Outlook.Status);
        Assert.Equal(OutlookStatus.Pending, results[1].Outlook.Status);
        Assert.Equal(OutlookStatus.Pending, second.OutlookStatus);
    }

    [Fact]
    public async Task Cancelling_sends_the_meeting_to_whoever_may_hold_it_and_a_confirmed_cancellation_forgets_them()
    {
        var agendaEvent = await ScheduledAsync(["luis@example.com"]);

        await _agenda.Cancel.HandleAsync(new CancelEventCommand(agendaEvent.Id, "Se movió a enero"), Ct);

        var data = Assert.Single(_agenda.Gateway.CancelledData);
        Assert.NotNull(data);
        Assert.Equal(["ana.perez@example.com", "equipo@example.com", "luis@example.com"], data.RequiredAttendees);
        Assert.Equal((2 * agendaEvent.Revision) + 1, data.Sequence);
        Assert.Equal("Se movió a enero", data.CancelReason);
        Assert.Equal("[Mantenimiento y parches] Parches de SO", data.Subject);
        Assert.Null(agendaEvent.OutlookSentRecipients);
        Assert.True(agendaEvent.OutlookIsCurrent);
    }

    [Fact]
    public async Task With_smtp_there_is_no_follow_up_cancellation_because_the_uid_is_already_stored()
    {
        var gateway = new ClockGateway(_agenda);
        var agenda = new AgendaHarness(outlookEnabled: true, transport: OutlookTransport.Smtp, gateway: gateway);
        gateway.Agenda = agenda;
        var draft = await agenda.CreateDraftAsync();
        // While the REQUEST is on its way, the activity is cancelled (the event already has its UID from the write-ahead).
        gateway.DuringPublish = agendaEvent => agendaEvent.Cancel("Ya no aplica", AgendaHarness.Now, AgendaHarness.CurrentUserId);

        await agenda.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);

        Assert.Equal(1, gateway.Calls);
        Assert.Equal(0, gateway.Cancels);
        Assert.Equal(EventStatus.Cancelled, draft.Status);
        Assert.True(draft.NeedsOutlookCancellation);
    }

    [Fact]
    public async Task The_second_write_is_tried_once_more_on_a_conflict()
    {
        var gateway = new ClockGateway(_agenda);
        var agenda = new AgendaHarness(outlookEnabled: true, transport: OutlookTransport.Smtp, gateway: gateway);
        gateway.Agenda = agenda;
        var draft = await agenda.CreateDraftAsync();
        gateway.DuringPublish = _ =>
        {
            agenda.UnitOfWork.FailWith = new ConflictException("concurrent change");
            agenda.UnitOfWork.FailuresLeft = 1;
        };

        var results = await agenda.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);

        Assert.Equal(OutlookStatus.Published, results[0].Outlook.Status);
        Assert.Equal(OutlookStatus.Published, draft.OutlookStatus);
    }

    [Fact]
    public async Task After_the_expiry_nothing_is_written_ahead_or_sent_and_the_screens_see_outlook_off()
    {
        var agenda = new AgendaHarness(outlookEnabled: true, transport: OutlookTransport.Smtp, acceptedUntil: AgendaHarness.Now);
        var draft = await agenda.CreateDraftAsync();

        var results = await agenda.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);
        var sync = await agenda.Sync.SyncOneAsync(draft.Id, Ct);

        Assert.Equal(0, agenda.Gateway.Calls);
        Assert.Equal(OutlookStatus.NotPublished, results[0].Outlook.Status);
        Assert.Null(draft.OutlookEventId);
        Assert.Null(draft.OutlookSentRecipients);
        Assert.Equal(OutlookErrorCodes.NotConfigured, sync.Outlook.ErrorCode);
        Assert.False(agenda.Settings.IsOutlookActive(AgendaHarness.Now));
        Assert.True(agenda.Settings.IsOutlookActive(AgendaHarness.Now.AddTicks(-1)));
    }

    [Fact]
    public async Task An_event_published_with_graph_is_not_written_ahead_and_does_not_block_the_change()
    {
        var draft = await _agenda.CreateDraftAsync();
        draft.Schedule(AgendaHarness.Now, _agenda.Settings.TimeZone, AgendaHarness.CurrentUserId);
        draft.MarkOutlookPublished("AAMkGraphId", null, draft.Revision, AgendaHarness.Now);
        _agenda.Gateway.Enqueue(OutlookGatewayResult.Failure(OutlookErrorCodes.OutlookRejected));

        await UpdateAsync(draft, participants: ["luis@example.com"]);

        // The change is saved; the adapter refuses this event alone (decision 10 of ADR-0023), and nothing is written ahead.
        Assert.Equal(["luis@example.com"], draft.Participants);
        Assert.Equal("AAMkGraphId", Assert.Single(_agenda.Gateway.Published).OutlookEventId);
        Assert.Equal("AAMkGraphId", draft.OutlookEventId);
        Assert.Null(draft.OutlookSentRecipients);
        Assert.Equal(OutlookErrorCodes.OutlookRejected, draft.OutlookErrorCode);
    }

    [Fact]
    public async Task With_smtp_the_batch_keeps_going_when_the_browser_aborts_the_request()
    {
        var draft = await _agenda.CreateDraftAsync();
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        // The first write runs before the abort is noticed; only what follows depends on the own deadline of the batch.
        var work = await _agenda.UnitOfWork.ExecuteAsync((_, _) =>
        {
            draft.Schedule(AgendaHarness.Now, _agenda.Settings.TimeZone, AgendaHarness.CurrentUserId);
            draft.MarkOutlookPending();
            return Task.FromResult(OutlookEventContent.ForPublish(draft, _agenda.DefaultType, _agenda.Responsible, _agenda.Settings));
        }, CancellationToken.None);
        var states = await _agenda.Publisher.RunAsync([work], AgendaHarness.Now, aborted.Token);

        Assert.Equal(OutlookStatus.Published, states[draft.Id].Status);
        Assert.Single(_agenda.Gateway.Published);
        Assert.Equal(OutlookStatus.Published, draft.OutlookStatus);
    }

    [Fact]
    public async Task With_graph_an_aborted_request_still_cancels_the_call()
    {
        var agenda = new AgendaHarness(outlookEnabled: true, gateway: new CancellingGateway());
        var draft = await agenda.CreateDraftAsync();
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var work = OutlookEventContent.ForPublish(draft, agenda.DefaultType, agenda.Responsible, agenda.Settings);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => agenda.Publisher.RunAsync([work], AgendaHarness.Now, aborted.Token));
    }

    [Fact]
    public async Task The_budget_counts_from_the_start_of_the_request_not_from_after_the_first_write()
    {
        var draft = await _agenda.CreateDraftAsync();
        _agenda.UnitOfWork.OnExecute = () =>
        {
            // The first write takes 10 s.
            _agenda.Clock.Set(_agenda.Clock.GetUtcNow().AddSeconds(10));
            _agenda.UnitOfWork.OnExecute = null;
        };

        await _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);

        Assert.Equal(AgendaHarness.Now + OutlookPublisher.SmtpBudget, Assert.Single(_agenda.Gateway.Published).Data.Deadline);
    }

    [Fact]
    public async Task When_the_own_deadline_ends_during_the_second_write_the_event_stays_pending_and_nothing_fails()
    {
        var gateway = new ClockGateway(_agenda);
        var agenda = new AgendaHarness(outlookEnabled: true, transport: OutlookTransport.Smtp, gateway: gateway);
        gateway.Agenda = agenda;
        var draft = await agenda.CreateDraftAsync();
        gateway.DuringPublish = _ =>
        {
            agenda.UnitOfWork.FailWith = new OperationCanceledException("own deadline");
            agenda.UnitOfWork.FailuresLeft = 1;
        };

        var results = await agenda.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);

        Assert.Equal(OutlookStatus.Pending, results[0].Outlook.Status);
        Assert.Equal(EventStatus.Scheduled, draft.Status);
    }

    /// <summary>A Graph-like port that honours the token it receives.</summary>
    private sealed class CancellingGateway : IOutlookCalendarGateway
    {
        public Task<OutlookGatewayResult> PublishAsync(OutlookEventData data, string? outlookEventId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OutlookGatewayResult.Success("AAMk", null));
        }

        public Task<OutlookGatewayResult> CancelAsync(string outlookEventId, string reason, OutlookEventData? data, CancellationToken cancellationToken) =>
            Task.FromResult(OutlookGatewayResult.Success(outlookEventId, null));
    }

    [Fact]
    public async Task The_audit_carries_counts_and_never_the_addresses()
    {
        var agendaEvent = await ScheduledAsync(["luis@example.com"]);
        _agenda.Gateway.Enqueue(OutlookGatewayResult.Success(agendaEvent.OutlookEventId!, null) with
        {
            ConfirmedRemovals = ["luis@example.com"],
            RecipientCount = 3,
            OmittedCount = 1,
        });

        await UpdateAsync(agendaEvent, participants: null);

        var published = _agenda.UnitOfWork.Entries.Last(entry => entry.Action == AgendaAuditActions.EventOutlookPublished);
        var json = JsonSerializer.Serialize(published.After);
        Assert.Contains("\"Recipients\":3", json, StringComparison.Ordinal);
        Assert.Contains("\"Removed\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"Omitted\":1", json, StringComparison.Ordinal);
        // The entries of the activity itself keep its participants as SPEC-0006 decided; the Outlook results never carry addresses.
        var outlookEntries = _agenda.UnitOfWork.Entries
            .Where(entry => entry.Action is AgendaAuditActions.EventOutlookPublished or AgendaAuditActions.EventOutlookFailed)
            .Select(entry => entry.After);
        Assert.DoesNotContain("@", JsonSerializer.Serialize(outlookEntries), StringComparison.Ordinal);
    }

    /// <summary>A port that moves the clock or changes the event while a publication is on its way.</summary>
    private sealed class ClockGateway(AgendaHarness owner) : IOutlookCalendarGateway
    {
        public AgendaHarness Agenda { get; set; } = owner;

        public TimeSpan AdvanceBy { get; set; }

        public Action<AgendaEvent>? DuringPublish { get; set; }

        public int Calls { get; private set; }

        public int Cancels { get; private set; }

        public Task<OutlookGatewayResult> PublishAsync(OutlookEventData data, string? outlookEventId, CancellationToken cancellationToken)
        {
            Calls++;
            Agenda.Clock.Set(Agenda.Clock.GetUtcNow() + AdvanceBy);
            DuringPublish?.Invoke(Agenda.Events.Items.Single(e => e.Id == data.EventId));
            DuringPublish = null;
            return Task.FromResult(OutlookGatewayResult.Success(outlookEventId!, null));
        }

        public Task<OutlookGatewayResult> CancelAsync(string outlookEventId, string reason, OutlookEventData? data, CancellationToken cancellationToken)
        {
            Calls++;
            Cancels++;
            return Task.FromResult(OutlookGatewayResult.Success(outlookEventId, null));
        }
    }
}
