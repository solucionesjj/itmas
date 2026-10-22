using System.Text.Json;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Events;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;

namespace Itmas3.UnitTests.Application.Agenda;

/// <summary>SPEC-0006 CA-7, CA-8 and CA-9 at the use-case level: scheduling, cancelling and recording the execution.</summary>
public sealed class LifecycleHandlerTests
{
    private readonly AgendaHarness _agenda = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static EventInput At(AgendaHarness agenda, int day, string title) =>
        agenda.Input(title: title, start: new DateTime(2026, 10, day, 9, 0, 0));

    [Fact]
    public async Task CA07_scheduling_several_drafts_returns_one_result_per_event_and_rejects_only_the_invalid_ones()
    {
        var ok1 = await _agenda.CreateDraftAsync(At(_agenda, 24, "A"));
        var ok2 = await _agenda.CreateDraftAsync(At(_agenda, 26, "B"));
        // Created through the domain because the use case would not allow a past start (RN-16).
        var past = AgendaEvent.Create(
            EventDetails.Create(_agenda.DefaultType.Id, "Vieja", null, _agenda.Responsible.Id, null, ["example.com"], true, EventImpact.None, null, null, 0),
            EventSchedule.Timed(new DateTime(2026, 10, 10, 9, 0, 0), new DateTime(2026, 10, 10, 10, 0, 0), _agenda.Settings.TimeZone),
            AgendaHarness.Now.AddDays(-10), AgendaHarness.CurrentUserId);
        _agenda.Events.Items.Add(past);
        var unknown = Guid.NewGuid();

        var results = await _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([ok1.Id, past.Id, unknown, ok2.Id]), Ct);

        Assert.Equal([ok1.Id, past.Id, unknown, ok2.Id], results.Select(r => r.Id));
        Assert.Equal(EventStatus.Scheduled, results[0].Status);
        Assert.Null(results[0].Error);
        Assert.Null(results[1].Status);
        Assert.Contains("pasó", results[1].Error, StringComparison.Ordinal);
        Assert.Null(results[2].Status);
        Assert.Equal("La actividad no existe.", results[2].Error);
        Assert.Equal(EventStatus.Scheduled, results[3].Status);
        Assert.Equal(EventStatus.Scheduled, ok1.Status);
        Assert.Equal(EventStatus.Scheduled, ok2.Status);
        Assert.Equal(EventStatus.Draft, past.Status);
        Assert.Equal(2, _agenda.UnitOfWork.Entries.Count(entry => entry.Action == AgendaAuditActions.EventScheduled));
    }

    [Fact]
    public async Task CA07_a_batch_has_between_one_and_fifty_events()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([]), Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([.. Enumerable.Range(0, 51).Select(_ => Guid.NewGuid())]), Ct));
    }

    [Fact]
    public async Task CA07_repeated_ids_are_processed_once_and_an_already_scheduled_event_is_rejected()
    {
        var draft = await _agenda.CreateDraftAsync();

        var first = await _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id, draft.Id]), Ct);
        var second = await _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);

        Assert.Single(first);
        Assert.Equal(EventStatus.Scheduled, first[0].Status);
        Assert.Null(second[0].Status);
        Assert.NotNull(second[0].Error);
    }

    [Fact]
    public async Task CA07_the_warnings_of_each_scheduled_event_are_returned()
    {
        var first = await _agenda.CreateDraftAsync(_agenda.Input(title: "A", impact: EventImpact.Outage));
        var second = await _agenda.CreateDraftAsync(_agenda.Input(title: "B", impact: EventImpact.Outage));

        var results = await _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([first.Id, second.Id]), Ct);

        Assert.All(results, result => Assert.Equal(AgendaWarningKind.ImpactOverlap, Assert.Single(result.Warnings).Kind));
    }

    [Fact]
    public async Task CA07_with_outlook_off_scheduling_only_changes_the_status()
    {
        var draft = await _agenda.CreateDraftAsync();

        var results = await _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);

        Assert.Equal(OutlookStatus.NotPublished, results[0].Outlook.Status);
        Assert.Equal(0, _agenda.Gateway.Calls);
    }

    [Fact]
    public async Task CA08_cancelling_requires_a_reason_and_closes_the_event()
    {
        var scheduled = await _agenda.CreateScheduledAsync();

        await Assert.ThrowsAsync<DomainException>(() => _agenda.Cancel.HandleAsync(new CancelEventCommand(scheduled.Id, "  "), Ct));
        Assert.Equal(EventStatus.Scheduled, scheduled.Status);

        var result = await _agenda.Cancel.HandleAsync(new CancelEventCommand(scheduled.Id, "Se pospone"), Ct);

        Assert.Equal(EventStatus.Cancelled, result.Status);
        Assert.Equal("Se pospone", scheduled.CancelReason);
        var entry = _agenda.UnitOfWork.Entries[^1];
        Assert.Equal(AgendaAuditActions.EventCancelled, entry.Action);
        Assert.Contains("Se pospone", JsonSerializer.Serialize(entry.After), StringComparison.Ordinal);
        await Assert.ThrowsAsync<DomainException>(() => _agenda.Cancel.HandleAsync(new CancelEventCommand(scheduled.Id, "otra vez"), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => _agenda.Cancel.HandleAsync(new CancelEventCommand(Guid.NewGuid(), "x"), Ct));
    }

    [Fact]
    public async Task CA08_a_draft_cannot_be_cancelled()
    {
        var draft = await _agenda.CreateDraftAsync();

        await Assert.ThrowsAsync<DomainException>(() => _agenda.Cancel.HandleAsync(new CancelEventCommand(draft.Id, "motivo"), Ct));
    }

    [Fact]
    public async Task CA09_the_execution_is_recorded_with_result_notes_and_links_and_audited()
    {
        var scheduled = await _agenda.CreateScheduledAsync();
        _agenda.Clock.Set(scheduled.EndsAtUtc.AddHours(1));
        var command = new CompleteEventCommand(
            scheduled.Id, CompletionResult.Partial, null, "  Faltó un servidor  ", [new EvidenceLinkInput("Informe", "https://evidencia.example.com/r1")]);

        var result = await _agenda.Complete.HandleAsync(command, Ct);

        Assert.Equal(EventStatus.Completed, result.Status);
        Assert.Equal(CompletionResult.Partial, scheduled.CompletionResult);
        Assert.Equal("Faltó un servidor", scheduled.CompletionNotes);
        Assert.Equal(scheduled.EndsAtUtc.AddHours(1), scheduled.ExecutedAt);
        Assert.Equal(AgendaHarness.CurrentUserId, scheduled.CompletedBy);
        var entry = _agenda.UnitOfWork.Entries[^1];
        Assert.Equal(AgendaAuditActions.EventCompleted, entry.Action);
        Assert.Contains("https://evidencia.example.com/r1", JsonSerializer.Serialize(entry.After), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CA09_the_execution_cannot_be_recorded_before_the_scheduled_start()
    {
        var scheduled = await _agenda.CreateScheduledAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            _agenda.Complete.HandleAsync(new CompleteEventCommand(scheduled.Id, CompletionResult.Succeeded, null, null, null), Ct));
        Assert.Equal(EventStatus.Scheduled, scheduled.Status);
    }

    [Theory]
    [InlineData("ftp://example.com/x")]
    [InlineData("https://usuario:clave@example.com/x")]
    [InlineData("javascript:alert(1)")]
    public async Task CA09_links_that_are_not_http_or_carry_credentials_are_rejected(string url)
    {
        var scheduled = await _agenda.CreateScheduledAsync();
        _agenda.Clock.Set(scheduled.EndsAtUtc.AddHours(1));

        await Assert.ThrowsAsync<DomainException>(() => _agenda.Complete.HandleAsync(
            new CompleteEventCommand(scheduled.Id, CompletionResult.Succeeded, null, null, [new EvidenceLinkInput("Evidencia", url)]), Ct));
        Assert.Equal(EventStatus.Scheduled, scheduled.Status);
    }

    [Fact]
    public async Task CA09_after_the_record_only_the_notes_and_the_links_can_change()
    {
        var scheduled = await _agenda.CreateScheduledAsync();
        _agenda.Clock.Set(scheduled.EndsAtUtc.AddHours(1));
        await _agenda.Complete.HandleAsync(new CompleteEventCommand(scheduled.Id, CompletionResult.Succeeded, null, "inicial", null), Ct);

        await _agenda.Complete.HandleAsync(
            new CompleteEventCommand(scheduled.Id, CompletionResult.Succeeded, null, "corregida", [new EvidenceLinkInput("Acta", "https://example.com/acta")]), Ct);

        Assert.Equal("corregida", scheduled.CompletionNotes);
        Assert.Single(scheduled.EvidenceLinks);
        Assert.Equal(AgendaAuditActions.EventCompletionUpdated, _agenda.UnitOfWork.Entries[^1].Action);

        await Assert.ThrowsAsync<DomainException>(() => _agenda.Complete.HandleAsync(
            new CompleteEventCommand(scheduled.Id, CompletionResult.Failed, null, "x", null), Ct));
        await Assert.ThrowsAsync<DomainException>(() => _agenda.Complete.HandleAsync(
            new CompleteEventCommand(scheduled.Id, CompletionResult.Succeeded, scheduled.ExecutedAt!.Value.AddDays(1), "x", null), Ct));
        Assert.Equal(CompletionResult.Succeeded, scheduled.CompletionResult);
    }

    [Fact]
    public async Task CA09_a_draft_cannot_be_completed()
    {
        var draft = await _agenda.CreateDraftAsync();
        _agenda.Clock.Set(draft.EndsAtUtc.AddHours(1));

        await Assert.ThrowsAsync<DomainException>(() =>
            _agenda.Complete.HandleAsync(new CompleteEventCommand(draft.Id, CompletionResult.Succeeded, null, null, null), Ct));
    }
}
