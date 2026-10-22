using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;
using static Itmas3.UnitTests.Domain.Agenda.AgendaTestData;

namespace Itmas3.UnitTests.Domain.Agenda;

/// <summary>SPEC-0006 RF-3 to RF-11, RF-18, RF-24, RN-3, RN-7 to RN-9 and CA-2, CA-4 to CA-9.</summary>
public sealed class AgendaEventTests
{
    private static readonly (string?, string?)[] OneLink = [("Informe", "https://evidencia.example.com/informe")];

    [Fact]
    public void CA02_an_event_is_created_in_draft_with_the_given_data()
    {
        var details = Details(participants: ["ana@example.com"], impact: EventImpact.Degradation, reminderMinutes: 120);
        var schedule = NextWeek();

        var agendaEvent = AgendaEvent.Create(details, schedule, Now, Actor);

        Assert.Equal(EventStatus.Draft, agendaEvent.Status);
        Assert.Equal(1, agendaEvent.Revision);
        Assert.Equal(TypeId, agendaEvent.TypeId);
        Assert.Equal("Parches de SO", agendaEvent.Title);
        Assert.Equal(Responsible, agendaEvent.ResponsibleUserId);
        Assert.Equal(["ana@example.com"], agendaEvent.Participants);
        Assert.Equal(EventImpact.Degradation, agendaEvent.Impact);
        Assert.Equal(120, agendaEvent.ReminderMinutes);
        Assert.Equal(schedule, agendaEvent.Timing);
        Assert.Equal(Now, agendaEvent.CreatedAt);
        Assert.Equal(Actor, agendaEvent.CreatedBy);
        Assert.Null(agendaEvent.UpdatedAt);
        Assert.Equal(OutlookStatus.NotPublished, agendaEvent.OutlookStatus);
        Assert.Null(agendaEvent.SeriesId);
        Assert.Empty(agendaEvent.Tags);
    }

    [Fact]
    public void Draft_events_can_be_updated_and_each_change_raises_the_revision()
    {
        var agendaEvent = Draft();

        agendaEvent.Update(Details(title: "Parches de octubre"), Timed(2026, 10, 27), Now.AddDays(1), Bogota, Responsible);

        Assert.Equal("Parches de octubre", agendaEvent.Title);
        Assert.Equal(new DateTime(2026, 10, 27, 9, 0, 0), agendaEvent.StartsAtLocal);
        Assert.Equal(2, agendaEvent.Revision);
        Assert.Equal(Now.AddDays(1), agendaEvent.UpdatedAt);
        Assert.Equal(Responsible, agendaEvent.UpdatedBy);
    }

    [Fact]
    public void RN07_a_draft_is_scheduled_once()
    {
        var agendaEvent = Draft();

        agendaEvent.Schedule(Now, Bogota, Actor);

        Assert.Equal(EventStatus.Scheduled, agendaEvent.Status);
        Assert.Equal(2, agendaEvent.Revision);
        Assert.Throws<DomainException>(() => agendaEvent.Schedule(Now, Bogota, Actor));
    }

    [Fact]
    public void CA07_RN03_an_event_that_already_started_cannot_be_scheduled()
    {
        var agendaEvent = Draft(Timed(2026, 10, 17, hour: 11));

        Assert.Throws<DomainException>(() => agendaEvent.Schedule(Now, Bogota, Actor));
        Assert.Equal(EventStatus.Draft, agendaEvent.Status);
    }

    [Fact]
    public void RN03_an_all_day_event_of_today_can_be_scheduled()
    {
        var today = EventSchedule.AllDay(new DateOnly(2026, 10, 17), new DateOnly(2026, 10, 17), Bogota);
        var agendaEvent = Draft(today);

        agendaEvent.Schedule(Now, Bogota, Actor);

        Assert.Equal(EventStatus.Scheduled, agendaEvent.Status);
    }

    [Fact]
    public void RN03_the_start_of_a_scheduled_event_cannot_move_into_the_past()
    {
        var agendaEvent = Scheduled();

        Assert.Throws<DomainException>(() =>
            agendaEvent.Update(Details(), Timed(2026, 10, 16), Now, Bogota, Actor));
        Assert.Equal(NextWeek(), agendaEvent.Timing);
    }

    [Fact]
    public void RN03_a_scheduled_event_that_already_started_can_still_change_its_other_data()
    {
        var agendaEvent = Scheduled();
        var later = Now.AddDays(8); // the start (2026-10-24 09:00 Bogotá) has passed

        agendaEvent.Update(Details(title: "Parches (extendidos)"), agendaEvent.Timing, later, Bogota, Actor);

        Assert.Equal("Parches (extendidos)", agendaEvent.Title);
    }

    [Fact]
    public void CA04_a_completed_event_admits_no_update_and_a_cancelled_one_neither()
    {
        var completed = Scheduled();
        completed.Complete(CompletionResult.Succeeded, null, null, null, Now.AddDays(8), Actor);
        var cancelled = Scheduled();
        cancelled.Cancel("Ya no es necesaria", Now, Actor);

        Assert.Throws<DomainException>(() => completed.Update(Details(), NextWeek(), Now.AddDays(9), Bogota, Actor));
        Assert.Throws<DomainException>(() => cancelled.Update(Details(), NextWeek(), Now, Bogota, Actor));
        Assert.Throws<DomainException>(() => cancelled.UpdateCompletion("notas", null, Now, Actor));
    }

    [Fact]
    public void CA08_cancelling_requires_a_reason_and_leaves_the_event_cancelled()
    {
        var agendaEvent = Scheduled();

        Assert.Throws<DomainException>(() => agendaEvent.Cancel("  ", Now, Actor));
        Assert.Throws<DomainException>(() => agendaEvent.Cancel(new string('a', 501), Now, Actor));
        Assert.Equal(EventStatus.Scheduled, agendaEvent.Status);

        agendaEvent.Cancel("  Se pospone por el cierre contable  ", Now.AddHours(1), Responsible);

        Assert.Equal(EventStatus.Cancelled, agendaEvent.Status);
        Assert.Equal("Se pospone por el cierre contable", agendaEvent.CancelReason);
        Assert.Equal(3, agendaEvent.Revision);
    }

    [Fact]
    public void RN07_only_scheduled_events_can_be_cancelled_or_completed()
    {
        var draft = Draft();

        Assert.Throws<DomainException>(() => draft.Cancel("motivo", Now, Actor));
        Assert.Throws<DomainException>(() => draft.Complete(CompletionResult.Succeeded, null, null, null, Now.AddDays(8), Actor));
    }

    [Fact]
    public void RN07_a_closed_event_is_final_completed_and_cancelled_do_not_turn_into_each_other()
    {
        var completed = Scheduled();
        completed.Complete(CompletionResult.Succeeded, null, null, null, Now.AddDays(8), Actor);
        var cancelled = Scheduled();
        cancelled.Cancel("motivo", Now, Actor);

        Assert.Throws<DomainException>(() => completed.Cancel("motivo", Now.AddDays(9), Actor));
        Assert.Throws<DomainException>(() => completed.Complete(CompletionResult.Partial, null, null, null, Now.AddDays(9), Actor));
        Assert.Throws<DomainException>(() => cancelled.Complete(CompletionResult.Succeeded, null, null, null, Now.AddDays(8), Actor));
        Assert.Throws<DomainException>(() => cancelled.Cancel("otro motivo", Now, Actor));
        Assert.Equal(EventStatus.Completed, completed.Status);
        Assert.Equal(CompletionResult.Succeeded, completed.CompletionResult);
        Assert.Equal(EventStatus.Cancelled, cancelled.Status);
        Assert.Equal("motivo", cancelled.CancelReason);
    }

    [Fact]
    public void CA09_the_execution_cannot_be_recorded_before_the_scheduled_start()
    {
        var agendaEvent = Scheduled(); // starts 2026-10-24 09:00 Bogotá = 14:00Z

        Assert.Throws<DomainException>(() =>
            agendaEvent.Complete(CompletionResult.Succeeded, null, null, null, agendaEvent.StartsAtUtc.AddMinutes(-1), Actor));
        agendaEvent.Complete(CompletionResult.Succeeded, null, null, null, agendaEvent.StartsAtUtc, Actor);

        Assert.Equal(EventStatus.Completed, agendaEvent.Status);
    }

    [Fact]
    public void CA09_the_execution_record_keeps_result_notes_links_and_dates()
    {
        var agendaEvent = Scheduled();
        var recordedAt = agendaEvent.EndsAtUtc.AddHours(1);
        var executedAt = agendaEvent.StartsAtUtc.AddMinutes(10);

        agendaEvent.Complete(CompletionResult.Partial, executedAt, "  Faltó un servidor  ", OneLink, recordedAt, Responsible);

        Assert.Equal(CompletionResult.Partial, agendaEvent.CompletionResult);
        Assert.Equal(executedAt, agendaEvent.ExecutedAt);
        Assert.Equal(recordedAt, agendaEvent.CompletedAt);
        Assert.Equal(Responsible, agendaEvent.CompletedBy);
        Assert.Equal("Faltó un servidor", agendaEvent.CompletionNotes);
        Assert.Equal(new EvidenceLink("Informe", "https://evidencia.example.com/informe"), Assert.Single(agendaEvent.EvidenceLinks));
    }

    [Fact]
    public void CA09_the_execution_instant_is_stored_in_utc_whatever_offset_the_caller_used()
    {
        var agendaEvent = Scheduled();
        var recordedAt = agendaEvent.EndsAtUtc.AddHours(1);
        var local = new DateTimeOffset(agendaEvent.StartsAtUtc.UtcDateTime.AddMinutes(10), TimeSpan.Zero).ToOffset(TimeSpan.FromHours(-5));

        agendaEvent.Complete(CompletionResult.Succeeded, local, null, null, recordedAt.ToOffset(TimeSpan.FromHours(-5)), Actor);

        Assert.Equal(TimeSpan.Zero, agendaEvent.ExecutedAt!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, agendaEvent.CompletedAt!.Value.Offset);
        Assert.Equal(local.UtcDateTime, agendaEvent.ExecutedAt.Value.UtcDateTime);
    }

    [Fact]
    public void CA09_the_execution_date_defaults_to_the_moment_of_the_record()
    {
        var agendaEvent = Scheduled();
        var recordedAt = agendaEvent.EndsAtUtc.AddHours(1);

        agendaEvent.Complete(CompletionResult.NotPerformed, null, null, null, recordedAt, Actor);

        Assert.Equal(recordedAt, agendaEvent.ExecutedAt);
    }

    [Fact]
    public void CA09_invalid_links_and_unknown_results_are_rejected_without_closing_the_event()
    {
        var agendaEvent = Scheduled();
        var after = agendaEvent.EndsAtUtc;

        Assert.Throws<DomainException>(() =>
            agendaEvent.Complete(CompletionResult.Succeeded, null, null, [("Malo", "ftp://example.com")], after, Actor));
        Assert.Throws<DomainException>(() =>
            agendaEvent.Complete(CompletionResult.Succeeded, null, null, [("Malo", "https://u:p@example.com")], after, Actor));
        Assert.Throws<DomainException>(() =>
            agendaEvent.Complete((CompletionResult)9, null, null, null, after, Actor));
        Assert.Throws<DomainException>(() =>
            agendaEvent.Complete(CompletionResult.Succeeded, null, new string('a', 4001), null, after, Actor));

        Assert.Equal(EventStatus.Scheduled, agendaEvent.Status);
    }

    [Fact]
    public void RN09_after_completing_only_the_notes_and_the_links_can_change()
    {
        var agendaEvent = Scheduled();
        agendaEvent.Complete(CompletionResult.Succeeded, null, "inicial", null, agendaEvent.EndsAtUtc, Actor);
        var revision = agendaEvent.Revision;

        agendaEvent.UpdateCompletion("corregidas", OneLink, agendaEvent.EndsAtUtc.AddDays(1), Responsible);

        Assert.Equal("corregidas", agendaEvent.CompletionNotes);
        Assert.Single(agendaEvent.EvidenceLinks);
        Assert.Equal(CompletionResult.Succeeded, agendaEvent.CompletionResult);
        Assert.Equal(revision + 1, agendaEvent.Revision);
        Assert.Throws<DomainException>(() => Scheduled().UpdateCompletion("x", null, Now, Actor));
    }

    [Fact]
    public void CA06_only_drafts_can_be_deleted()
    {
        Draft().EnsureCanBeDeleted();

        Assert.Throws<DomainException>(() => Scheduled().EnsureCanBeDeleted());
        var completed = Scheduled();
        completed.Complete(CompletionResult.Succeeded, null, null, null, completed.EndsAtUtc, Actor);
        Assert.Throws<DomainException>(() => completed.EnsureCanBeDeleted());
        var cancelled = Scheduled();
        cancelled.Cancel("motivo", Now, Actor);
        Assert.Throws<DomainException>(() => cancelled.EnsureCanBeDeleted());
    }

    [Fact]
    public void RF08_a_draft_or_scheduled_event_whose_end_has_passed_is_overdue()
    {
        var draft = Draft();
        var scheduled = Scheduled();

        Assert.False(draft.IsOverdue(draft.EndsAtUtc));
        Assert.True(draft.IsOverdue(draft.EndsAtUtc.AddMinutes(1)));
        Assert.True(scheduled.IsOverdue(scheduled.EndsAtUtc.AddMinutes(1)));

        scheduled.Complete(CompletionResult.Succeeded, null, null, null, scheduled.EndsAtUtc.AddMinutes(1), Actor);
        Assert.False(scheduled.IsOverdue(scheduled.EndsAtUtc.AddYears(1)));
    }

    [Fact]
    public void CA05_common_fields_change_future_events_without_touching_dates_or_notify_team()
    {
        var agendaEvent = Draft(details: Details(notifyTeam: true));
        var schedule = agendaEvent.Timing;
        var otherResponsible = Guid.NewGuid();
        var common = Details(title: "Parches 2026", notifyTeam: false, impact: EventImpact.Outage, responsible: otherResponsible, reminderMinutes: 60);

        var applied = agendaEvent.TryApplyCommonFields(common, Now, Actor);

        Assert.True(applied);
        Assert.Equal("Parches 2026", agendaEvent.Title);
        Assert.Equal(otherResponsible, agendaEvent.ResponsibleUserId);
        Assert.Equal(EventImpact.Outage, agendaEvent.Impact);
        Assert.Equal(60, agendaEvent.ReminderMinutes);
        Assert.True(agendaEvent.NotifyTeam);
        Assert.Equal(schedule, agendaEvent.Timing);
        Assert.Equal(2, agendaEvent.Revision);
    }

    [Fact]
    public void CA05_common_fields_skip_events_that_started_or_are_closed()
    {
        var started = Scheduled();
        var completed = Scheduled();
        completed.Complete(CompletionResult.Succeeded, null, null, null, completed.EndsAtUtc, Actor);
        var cancelled = Scheduled();
        cancelled.Cancel("motivo", Now, Actor);
        var common = Details(title: "Nuevo título");

        Assert.False(started.TryApplyCommonFields(common, started.StartsAtUtc, Actor));
        Assert.False(completed.TryApplyCommonFields(common, Now, Actor));
        Assert.False(cancelled.TryApplyCommonFields(common, Now, Actor));
        Assert.Equal("Parches de SO", started.Title);
    }

    [Fact]
    public void CA27_tags_can_be_replaced_in_any_status_without_changing_the_revision()
    {
        var tagA = AgendaTag.Create("windows", Now, Actor);
        var tagB = AgendaTag.Create("producción", Now, Actor);
        var cancelled = Scheduled();
        cancelled.Cancel("motivo", Now, Actor);
        var revision = cancelled.Revision;

        cancelled.ReplaceTags([tagA, tagB]);

        Assert.Equal(2, cancelled.Tags.Count);
        Assert.Equal(revision, cancelled.Revision);

        cancelled.ReplaceTags([tagB]);

        Assert.Equal([tagB], cancelled.Tags);
        Assert.Equal(revision, cancelled.Revision);
    }

    [Fact]
    public void Tags_are_limited_to_ten_and_cannot_repeat()
    {
        var tags = Enumerable.Range(1, 11).Select(i => AgendaTag.Create($"t{i}", Now, Actor)).ToList();
        var agendaEvent = Draft();

        Assert.Throws<DomainException>(() => agendaEvent.ReplaceTags(tags));
        Assert.Throws<DomainException>(() => agendaEvent.ReplaceTags([tags[0], tags[0]]));
        agendaEvent.ReplaceTags(tags.Take(10).ToList());
        Assert.Equal(10, agendaEvent.Tags.Count);
    }

    [Fact]
    public void RF18_the_publication_state_follows_the_outlook_operations_without_changing_the_revision()
    {
        var agendaEvent = Scheduled();
        var revision = agendaEvent.Revision;
        Assert.True(agendaEvent.NeedsOutlookSync);

        agendaEvent.MarkOutlookPending();
        Assert.Equal(OutlookStatus.Pending, agendaEvent.OutlookStatus);
        Assert.True(agendaEvent.NeedsOutlookSync);

        agendaEvent.MarkOutlookPublished("AAMk-1", "https://outlook.example/evento", agendaEvent.Revision, Now);
        Assert.Equal(OutlookStatus.Published, agendaEvent.OutlookStatus);
        Assert.Equal("AAMk-1", agendaEvent.OutlookEventId);
        Assert.Equal(revision, agendaEvent.OutlookSyncedRevision);
        Assert.False(agendaEvent.NeedsOutlookSync);

        agendaEvent.MarkOutlookFailed("graph_unavailable", Now.AddMinutes(1));
        Assert.Equal(OutlookStatus.Error, agendaEvent.OutlookStatus);
        Assert.Equal("graph_unavailable", agendaEvent.OutlookErrorCode);
        Assert.True(agendaEvent.NeedsOutlookSync);
        Assert.Equal(revision, agendaEvent.Revision);
    }

    [Fact]
    public void RF19_a_published_event_that_changes_needs_to_be_synced_again()
    {
        var agendaEvent = Scheduled();
        agendaEvent.MarkOutlookPublished("AAMk-1", null, agendaEvent.Revision, Now);
        Assert.False(agendaEvent.NeedsOutlookSync);

        agendaEvent.Update(Details(title: "Otro título"), NextWeek(), Now.AddHours(1), Bogota, Actor);

        Assert.True(agendaEvent.NeedsOutlookSync);
    }

    [Fact]
    public void RF18_an_event_that_changed_while_outlook_was_being_called_stays_pending()
    {
        var agendaEvent = Scheduled();
        var sentRevision = agendaEvent.Revision;
        agendaEvent.Update(Details(title: "Cambió mientras tanto"), NextWeek(), Now.AddMinutes(1), Bogota, Actor);

        agendaEvent.MarkOutlookPublished("AAMk-1", null, sentRevision, Now.AddMinutes(2));

        Assert.Equal(OutlookStatus.Pending, agendaEvent.OutlookStatus);
        Assert.Equal(sentRevision, agendaEvent.OutlookSyncedRevision);
        Assert.True(agendaEvent.NeedsOutlookSync);
    }

    [Fact]
    public void RF19_a_cancelled_event_whose_cancellation_is_not_confirmed_needs_it_again()
    {
        var agendaEvent = Scheduled();
        agendaEvent.MarkOutlookPublished("AAMk-1", null, agendaEvent.Revision, Now);
        agendaEvent.Cancel("motivo", Now.AddHours(1), Actor);

        Assert.True(agendaEvent.NeedsOutlookCancellation);

        agendaEvent.MarkOutlookPublished("AAMk-1", null, agendaEvent.Revision, Now.AddHours(2));

        Assert.False(agendaEvent.NeedsOutlookCancellation);

        var neverPublished = Scheduled();
        neverPublished.Cancel("motivo", Now, Actor);
        Assert.False(neverPublished.NeedsOutlookCancellation);
    }

    [Fact]
    public void Drafts_and_closed_events_never_need_an_outlook_sync()
    {
        Assert.False(Draft().NeedsOutlookSync);
        var cancelled = Scheduled();
        cancelled.Cancel("motivo", Now, Actor);
        Assert.False(cancelled.NeedsOutlookSync);
    }

    [Fact]
    public void RN12_the_occurrences_of_a_series_are_created_in_draft_with_the_series_id()
    {
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Month, 1, RecurrenceAnchor.OnDay(20), new DateOnly(2026, 10, 20), null, 3);
        var series = EventSeries.Create(TypeId, pattern, Now, Actor);
        var schedules = RecurrenceGenerator.Generate(pattern, Timed(2026, 10, 20), Bogota, Now);

        var events = series.CreateOccurrences(Details(), schedules, Now, Actor);

        Assert.Equal(3, events.Count);
        Assert.All(events, e =>
        {
            Assert.Equal(series.Id, e.SeriesId);
            Assert.Equal(EventStatus.Draft, e.Status);
        });
        Assert.Equal(3, events.Select(e => e.Id).Distinct().Count());
    }
}
