using System.Text.Json;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Agenda.Events;
using Itmas3.Application.Agenda.Types;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;

namespace Itmas3.UnitTests.Application.Agenda;

/// <summary>SPEC-0006 CA-1, CA-2, CA-4, CA-6, CA-11, CA-12, CA-13, CA-21, CA-25, CA-27, CA-29 at the use-case level.</summary>
public sealed class EventHandlerTests
{
    private readonly AgendaHarness _agenda = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CA01_types_are_created_and_modified_and_a_repeated_name_is_a_business_error()
    {
        var created = await _agenda.CreateType.HandleAsync(new EventTypeInput("Hardening", "Líneas base", 90, 2880, false), Ct);

        Assert.Equal(AgendaAuditActions.EventTypeCreated, _agenda.UnitOfWork.Entries[^1].Action);
        await Assert.ThrowsAsync<DomainException>(() => _agenda.CreateType.HandleAsync(new EventTypeInput("HARDENING", null, 60, 0, false), Ct));

        var updated = await _agenda.UpdateType.HandleAsync(
            new UpdateEventTypeCommand(created.Id, 1, new EventTypeInput("Hardening de servidores", null, 60, 0, false), IsActive: true), Ct);

        Assert.Equal("Hardening de servidores", updated.Name);
        Assert.Equal(2, updated.Revision);
        Assert.Equal(AgendaAuditActions.EventTypeUpdated, _agenda.UnitOfWork.Entries[^1].Action);
    }

    [Fact]
    public async Task CA01_renaming_a_type_to_the_name_of_another_is_rejected()
    {
        var other = await _agenda.CreateType.HandleAsync(new EventTypeInput("Otro tipo", null, 60, 0, false), Ct);

        await Assert.ThrowsAsync<DomainException>(() => _agenda.UpdateType.HandleAsync(
            new UpdateEventTypeCommand(other.Id, 1, new EventTypeInput("mantenimiento y PARCHES", null, 60, 0, false), true), Ct));
    }

    [Fact]
    public async Task CA04_a_stale_revision_of_a_type_is_a_precondition_failure()
    {
        await Assert.ThrowsAsync<PreconditionFailedException>(() => _agenda.UpdateType.HandleAsync(
            new UpdateEventTypeCommand(_agenda.DefaultType.Id, 99, new EventTypeInput("X", null, 60, 0, false), true), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => _agenda.UpdateType.HandleAsync(
            new UpdateEventTypeCommand(Guid.NewGuid(), 1, new EventTypeInput("X", null, 60, 0, false), true), Ct));
    }

    [Fact]
    public async Task CA01_an_inactive_type_cannot_be_assigned_to_new_events_but_existing_events_keep_it()
    {
        var existing = await _agenda.CreateDraftAsync();
        await _agenda.UpdateType.HandleAsync(
            new UpdateEventTypeCommand(_agenda.DefaultType.Id, _agenda.DefaultType.Revision, new EventTypeInput("Mantenimiento y parches", null, 120, 1440, false), IsActive: false), Ct);

        await Assert.ThrowsAsync<DomainException>(() => _agenda.Create.HandleAsync(_agenda.Input(), Ct));

        // Editing the existing event without changing its type is allowed.
        var result = await _agenda.Update.HandleAsync(
            new UpdateEventCommand(existing.Id, existing.Revision, _agenda.Input(title: "Parches (editado)")), Ct);
        Assert.Equal("Parches (editado)", existing.Title);
        Assert.Equal(existing.Revision, result.Revision);
        Assert.Equal(_agenda.DefaultType.Id, existing.TypeId);
    }

    [Fact]
    public async Task CA02_an_event_is_created_in_draft_with_the_reminder_of_its_type_and_is_audited()
    {
        var result = await _agenda.Create.HandleAsync(_agenda.Input(), Ct);

        var agendaEvent = Assert.Single(_agenda.Events.Items);
        Assert.Equal(result.Id, agendaEvent.Id);
        Assert.Equal(EventStatus.Draft, agendaEvent.Status);
        Assert.Equal(1440, agendaEvent.ReminderMinutes);
        Assert.Equal(AgendaHarness.CurrentUserId, agendaEvent.CreatedBy);
        Assert.Equal(OutlookStatus.NotPublished, result.Outlook.Status);
        var entry = Assert.Single(_agenda.UnitOfWork.Entries);
        Assert.Equal(AgendaAuditActions.EventCreated, entry.Action);
        Assert.Equal(AgendaAuditActions.EventResourceType, entry.ResourceType);
        Assert.Equal(agendaEvent.Id.ToString(), entry.ResourceId);
    }

    [Fact]
    public async Task CA02_an_explicit_reminder_wins_over_the_one_of_the_type()
    {
        await _agenda.Create.HandleAsync(_agenda.Input(reminder: 15), Ct);

        Assert.Equal(15, _agenda.Events.Items.Single().ReminderMinutes);
    }

    [Fact]
    public async Task CA02_a_missing_end_uses_the_default_duration_of_the_type()
    {
        var timing = new EventTimingInput(false, new DateTime(2026, 10, 24, 9, 0, 0), null, null, null);

        await _agenda.Create.HandleAsync(_agenda.Input(timing: timing), Ct);

        Assert.Equal(new DateTime(2026, 10, 24, 11, 0, 0), _agenda.Events.Items.Single().EndsAtLocal);
    }

    [Fact]
    public async Task CA02_an_all_day_event_without_last_day_lasts_one_day()
    {
        var timing = new EventTimingInput(true, null, null, new DateOnly(2026, 10, 26), null);

        await _agenda.Create.HandleAsync(_agenda.Input(timing: timing), Ct);

        var agendaEvent = _agenda.Events.Items.Single();
        Assert.True(agendaEvent.IsAllDay);
        Assert.Equal(new DateTime(2026, 10, 27, 0, 0, 0), agendaEvent.EndsAtLocal);
    }

    [Fact]
    public async Task CA02_an_unknown_type_is_a_business_error()
    {
        await Assert.ThrowsAsync<DomainException>(() => _agenda.Create.HandleAsync(_agenda.Input(typeId: Guid.NewGuid()), Ct));
        Assert.Empty(_agenda.Events.Items);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CA02_the_title_is_required(string title) =>
        await Assert.ThrowsAsync<DomainException>(() => _agenda.Create.HandleAsync(_agenda.Input(title: title), Ct));

    [Fact]
    public async Task CA12_an_inactive_or_unknown_responsible_cannot_be_assigned()
    {
        var inactive = new AgendaUser(Guid.NewGuid(), "Baja", "baja@example.com", false);
        _agenda.Directory.Users[inactive.Id] = inactive;

        await Assert.ThrowsAsync<DomainException>(() => _agenda.Create.HandleAsync(_agenda.Input(responsible: inactive.Id), Ct));
        await Assert.ThrowsAsync<DomainException>(() => _agenda.Create.HandleAsync(_agenda.Input(responsible: Guid.NewGuid()), Ct));
        Assert.Empty(_agenda.Events.Items);
    }

    [Fact]
    public async Task CA12_a_responsible_deactivated_later_does_not_block_editing_the_event()
    {
        var agendaEvent = await _agenda.CreateDraftAsync();
        _agenda.Directory.Users[_agenda.Responsible.Id] = _agenda.Responsible with { IsActive = false };

        await _agenda.Update.HandleAsync(new UpdateEventCommand(agendaEvent.Id, agendaEvent.Revision, _agenda.Input(title: "Nuevo título")), Ct);

        Assert.Equal("Nuevo título", agendaEvent.Title);
        Assert.Equal(_agenda.Responsible.Id, agendaEvent.ResponsibleUserId);

        // Reassigning to another inactive user is still rejected.
        var other = new AgendaUser(Guid.NewGuid(), "Otro", "otro@example.com", false);
        _agenda.Directory.Users[other.Id] = other;
        await Assert.ThrowsAsync<DomainException>(() => _agenda.Update.HandleAsync(
            new UpdateEventCommand(agendaEvent.Id, agendaEvent.Revision, _agenda.Input(responsible: other.Id)), Ct));
    }

    [Fact]
    public async Task CA13_participants_outside_the_allowed_domains_are_rejected()
    {
        await Assert.ThrowsAsync<DomainException>(() => _agenda.Create.HandleAsync(_agenda.Input(participants: ["intruso@otro.com"]), Ct));
        await Assert.ThrowsAsync<DomainException>(() => _agenda.Create.HandleAsync(_agenda.Input(participants: ["a@example.com", "A@example.com"]), Ct));

        await _agenda.Create.HandleAsync(_agenda.Input(participants: ["luis@example.com"]), Ct);
        Assert.Equal(["luis@example.com"], _agenda.Events.Items.Single().Participants);
    }

    [Fact]
    public async Task CA29_an_event_that_starts_in_the_past_is_rejected()
    {
        var past = _agenda.Input(start: new DateTime(2026, 10, 17, 8, 0, 0)); // Now is 12:00 in Bogotá

        var exception = await Assert.ThrowsAsync<DomainException>(() => _agenda.Create.HandleAsync(past, Ct));

        Assert.Contains("pasado", exception.Message, StringComparison.Ordinal);
        Assert.Empty(_agenda.Events.Items);
    }

    [Fact]
    public async Task CA29_an_event_that_starts_later_today_is_accepted()
    {
        await _agenda.Create.HandleAsync(_agenda.Input(start: new DateTime(2026, 10, 17, 15, 0, 0)), Ct);

        Assert.Single(_agenda.Events.Items);
    }

    [Fact]
    public async Task CA11_an_activity_overlapping_a_scheduled_freeze_is_saved_with_a_warning()
    {
        var freeze = await _agenda.CreateType.HandleAsync(new EventTypeInput("Congelamiento", null, 1440, 1440, IsFreeze: true), Ct);
        await _agenda.CreateScheduledAsync(_agenda.Input(title: "Cierre contable", typeId: freeze.Id));

        var result = await _agenda.Create.HandleAsync(_agenda.Input(title: "Parches"), Ct);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(AgendaWarningKind.FreezeOverlap, warning.Kind);
        Assert.Equal("Cierre contable", warning.EventTitle);
        Assert.Equal(2, _agenda.Events.Items.Count);
    }

    [Fact]
    public async Task CA11_two_overlapping_activities_with_impact_warn_each_other_on_creation_and_on_update()
    {
        await _agenda.CreateDraftAsync(_agenda.Input(title: "Reinicio de servidores", impact: EventImpact.Outage));

        var second = await _agenda.Create.HandleAsync(_agenda.Input(title: "Parches", impact: EventImpact.Degradation), Ct);
        Assert.Equal(AgendaWarningKind.ImpactOverlap, Assert.Single(second.Warnings).Kind);

        var agendaEvent = _agenda.Events.Items.Single(e => e.Id == second.Id);
        var updated = await _agenda.Update.HandleAsync(
            new UpdateEventCommand(agendaEvent.Id, agendaEvent.Revision, _agenda.Input(title: "Parches", impact: EventImpact.Degradation)), Ct);
        Assert.Single(updated.Warnings);

        var moved = await _agenda.Update.HandleAsync(
            new UpdateEventCommand(agendaEvent.Id, agendaEvent.Revision, _agenda.Input(title: "Parches", impact: EventImpact.Degradation, start: new DateTime(2026, 10, 25, 9, 0, 0))), Ct);
        Assert.Empty(moved.Warnings);
    }

    [Fact]
    public async Task CA04_updating_with_a_stale_revision_is_a_precondition_failure_and_changes_nothing()
    {
        var agendaEvent = await _agenda.CreateDraftAsync();

        await Assert.ThrowsAsync<PreconditionFailedException>(() => _agenda.Update.HandleAsync(
            new UpdateEventCommand(agendaEvent.Id, agendaEvent.Revision + 1, _agenda.Input(title: "Otro")), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => _agenda.Update.HandleAsync(
            new UpdateEventCommand(Guid.NewGuid(), 1, _agenda.Input()), Ct));

        Assert.Equal("Parches de SO", agendaEvent.Title);
    }

    [Fact]
    public async Task CA04_a_completed_event_cannot_be_updated_and_a_cancelled_one_neither()
    {
        var completed = await _agenda.CreateScheduledAsync(_agenda.Input(title: "Completada"));
        var cancelled = await _agenda.CreateScheduledAsync(_agenda.Input(title: "Cancelada", start: new DateTime(2026, 10, 26, 9, 0, 0)));
        await _agenda.Cancel.HandleAsync(new CancelEventCommand(cancelled.Id, "Ya no aplica"), Ct);
        _agenda.Clock.Set(completed.EndsAtUtc.AddHours(1));
        await _agenda.Complete.HandleAsync(new CompleteEventCommand(completed.Id, CompletionResult.Succeeded, null, null, null), Ct);

        await Assert.ThrowsAsync<DomainException>(() => _agenda.Update.HandleAsync(
            new UpdateEventCommand(completed.Id, completed.Revision, _agenda.Input()), Ct));
        await Assert.ThrowsAsync<DomainException>(() => _agenda.Update.HandleAsync(
            new UpdateEventCommand(cancelled.Id, cancelled.Revision, _agenda.Input()), Ct));
    }

    [Fact]
    public async Task CA06_only_drafts_are_deleted_and_the_deletion_is_audited()
    {
        var draft = await _agenda.CreateDraftAsync(_agenda.Input(title: "Borrador"));
        var scheduled = await _agenda.CreateScheduledAsync(_agenda.Input(title: "Programada", start: new DateTime(2026, 10, 26, 9, 0, 0)));

        await _agenda.Delete.HandleAsync(draft.Id, Ct);

        Assert.DoesNotContain(draft, _agenda.Events.Items);
        Assert.Equal(AgendaAuditActions.EventDeleted, _agenda.UnitOfWork.Entries[^1].Action);
        await Assert.ThrowsAsync<DomainException>(() => _agenda.Delete.HandleAsync(scheduled.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => _agenda.Delete.HandleAsync(Guid.NewGuid(), Ct));
        Assert.Contains(scheduled, _agenda.Events.Items);
    }

    [Fact]
    public async Task CA25_tags_are_created_when_first_used_and_reused_ignoring_case()
    {
        await _agenda.Create.HandleAsync(_agenda.Input(title: "Uno", tags: ["Windows", "producción", "windows"]), Ct);
        await _agenda.Create.HandleAsync(_agenda.Input(title: "Dos", start: new DateTime(2026, 10, 26, 9, 0, 0), tags: ["WINDOWS"]), Ct);

        Assert.Equal(2, _agenda.Tags.Items.Count);
        Assert.Equal(["Windows"], _agenda.Tags.Items.Where(t => TagName.Key(t.Name) == "windows").Select(t => t.Name));
        Assert.Equal(["producción", "Windows"], _agenda.Events.Items[0].Tags.Select(t => t.Name).Order());
        Assert.Same(_agenda.Events.Items[0].Tags.Single(t => TagName.Key(t.Name) == "windows"), _agenda.Events.Items[1].Tags.Single());
    }

    [Fact]
    public async Task CA25_eleven_tags_or_an_invalid_one_are_rejected()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _agenda.Create.HandleAsync(_agenda.Input(tags: [.. Enumerable.Range(1, 11).Select(i => $"t{i}")]), Ct));
        await Assert.ThrowsAsync<DomainException>(() => _agenda.Create.HandleAsync(_agenda.Input(tags: [new string('a', 31)]), Ct));
        Assert.Empty(_agenda.Events.Items);
    }

    [Fact]
    public async Task CA27_tags_are_replaced_in_any_status_without_changing_the_revision_or_calling_outlook()
    {
        var agenda = new AgendaHarness(outlookEnabled: true);
        var agendaEvent = await agenda.CreateScheduledAsync();
        await agenda.Cancel.HandleAsync(new CancelEventCommand(agendaEvent.Id, "Ya no aplica"), Ct);
        var revision = agendaEvent.Revision;
        var calls = agenda.Gateway.Calls;

        var tags = await agenda.SetTags.HandleAsync(new SetEventTagsCommand(agendaEvent.Id, ["iso-27001", "2026-t4"]), Ct);

        Assert.Equal(["2026-t4", "iso-27001"], tags);
        Assert.Equal(revision, agendaEvent.Revision);
        Assert.Equal(calls, agenda.Gateway.Calls);
        var entry = agenda.UnitOfWork.Entries[^1];
        Assert.Equal(AgendaAuditActions.EventTagsUpdated, entry.Action);
        Assert.Contains("iso-27001", JsonSerializer.Serialize(entry.After), StringComparison.Ordinal);

        await agenda.SetTags.HandleAsync(new SetEventTagsCommand(agendaEvent.Id, []), Ct);
        Assert.Empty(agendaEvent.Tags);
        await Assert.ThrowsAsync<NotFoundException>(() => agenda.SetTags.HandleAsync(new SetEventTagsCommand(Guid.NewGuid(), ["x"]), Ct));
    }

    [Fact]
    public async Task CA21_every_write_is_audited_without_secrets()
    {
        var agendaEvent = await _agenda.CreateDraftAsync();
        await _agenda.Update.HandleAsync(new UpdateEventCommand(agendaEvent.Id, agendaEvent.Revision, _agenda.Input(title: "Otro nombre")), Ct);
        await _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([agendaEvent.Id]), Ct);

        Assert.Equal(
            [AgendaAuditActions.EventCreated, AgendaAuditActions.EventUpdated, AgendaAuditActions.EventScheduled],
            _agenda.UnitOfWork.Entries.Select(entry => entry.Action));
        var update = _agenda.UnitOfWork.Entries[1];
        Assert.Contains("Parches de SO", JsonSerializer.Serialize(update.Before), StringComparison.Ordinal);
        Assert.Contains("Otro nombre", JsonSerializer.Serialize(update.After), StringComparison.Ordinal);
    }
}
