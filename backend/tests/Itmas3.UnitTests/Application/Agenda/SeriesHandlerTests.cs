using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Agenda.Events;
using Itmas3.Application.Agenda.Series;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;

namespace Itmas3.UnitTests.Application.Agenda;

/// <summary>SPEC-0006 CA-3, CA-5, CA-28 and CA-29 at the use-case level: series.</summary>
public sealed class SeriesHandlerTests
{
    private readonly AgendaHarness _agenda = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SeriesRuleInput Monthly(int count = 3, RecurrenceAnchor? anchor = null) =>
        new(RecurrenceUnit.Month, 1, anchor ?? RecurrenceAnchor.OnNthWeekday(2, DayOfWeek.Tuesday), null, count);

    // Saturday 2026-10-24 at 09:00: the second Tuesday of October is behind, so the series starts in November.
    private EventInput First(IReadOnlyList<string>? tags = null, EventImpact impact = EventImpact.None) =>
        _agenda.Input(title: "Parches de SO", tags: tags, impact: impact);

    [Fact]
    public async Task CA03_the_preview_saves_nothing_not_even_tags_and_lists_the_dates()
    {
        var result = await _agenda.CreateSeries.HandleAsync(new CreateSeriesCommand(First(["windows"]), Monthly(), DryRun: true), Ct);

        Assert.Null(result.SeriesId);
        Assert.Empty(result.EventIds);
        Assert.Equal(
            [new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 8), new DateOnly(2027, 1, 12)],
            result.Occurrences.Select(o => o.Schedule.FirstDay));
        Assert.Empty(_agenda.Events.Items);
        Assert.Empty(_agenda.Series.Items);
        Assert.Empty(_agenda.Tags.Items);
        Assert.Empty(_agenda.UnitOfWork.Entries);
    }

    [Fact]
    public async Task CA03_the_preview_runs_the_same_checks_as_the_creation()
    {
        await Assert.ThrowsAsync<DomainException>(() => _agenda.CreateSeries.HandleAsync(
            new CreateSeriesCommand(First(), new SeriesRuleInput(RecurrenceUnit.Day, 1, null, null, 61), DryRun: true), Ct));
        await Assert.ThrowsAsync<DomainException>(() => _agenda.CreateSeries.HandleAsync(
            new CreateSeriesCommand(First(tags: [.. Enumerable.Range(1, 11).Select(i => $"t{i}")]), Monthly(), DryRun: true), Ct));
        await Assert.ThrowsAsync<DomainException>(() => _agenda.CreateSeries.HandleAsync(
            new CreateSeriesCommand(_agenda.Input(typeId: Guid.NewGuid()), Monthly(), DryRun: true), Ct));
    }

    [Fact]
    public async Task CA03_creating_a_series_saves_the_series_and_its_occurrences_in_draft_with_one_audit_entry()
    {
        var result = await _agenda.CreateSeries.HandleAsync(new CreateSeriesCommand(First(["windows", "producción"]), Monthly(), DryRun: false), Ct);

        var series = Assert.Single(_agenda.Series.Items);
        Assert.Equal(series.Id, result.SeriesId);
        Assert.Equal(3, result.EventIds.Count);
        Assert.Equal(3, _agenda.Events.Items.Count);
        Assert.All(_agenda.Events.Items, e =>
        {
            Assert.Equal(series.Id, e.SeriesId);
            Assert.Equal(EventStatus.Draft, e.Status);
            Assert.Equal(AgendaHarness.CurrentUserId, e.CreatedBy);
        });
        Assert.Equal(result.EventIds, _agenda.Events.Items.Select(e => e.Id));
        var entry = Assert.Single(_agenda.UnitOfWork.Entries);
        Assert.Equal(AgendaAuditActions.SeriesCreated, entry.Action);
        Assert.Equal(AgendaAuditActions.SeriesResourceType, entry.ResourceType);
    }

    [Fact]
    public async Task CA28_the_occurrences_inherit_the_tags_of_the_series()
    {
        await _agenda.CreateSeries.HandleAsync(new CreateSeriesCommand(First(["windows", "producción"]), Monthly(), DryRun: false), Ct);

        Assert.Equal(2, _agenda.Tags.Items.Count);
        Assert.All(_agenda.Events.Items, e => Assert.Equal(["producción", "windows"], e.Tags.Select(t => t.Name).Order()));
    }

    [Fact]
    public async Task CA29_a_series_that_starts_in_the_past_is_rejected()
    {
        var past = _agenda.Input(start: new DateTime(2026, 10, 10, 9, 0, 0));

        await Assert.ThrowsAsync<DomainException>(() => _agenda.CreateSeries.HandleAsync(new CreateSeriesCommand(past, Monthly(), false), Ct));
        Assert.Empty(_agenda.Events.Items);
    }

    [Fact]
    public async Task CA11_the_preview_warns_about_the_occurrences_that_overlap_a_freeze()
    {
        var freeze = await _agenda.CreateType.HandleAsync(new Itmas3.Application.Agenda.Types.EventTypeInput("Congelamiento", null, 1440, 1440, true), Ct);
        // The freeze covers Tuesday 2026-11-10 (the first occurrence) from 09:00 to the next day.
        await _agenda.CreateScheduledAsync(_agenda.Input(title: "Cierre", typeId: freeze.Id, start: new DateTime(2026, 11, 10, 8, 0, 0)));

        var result = await _agenda.CreateSeries.HandleAsync(new CreateSeriesCommand(First(), Monthly(), DryRun: true), Ct);

        Assert.Equal(AgendaWarningKind.FreezeOverlap, Assert.Single(result.Occurrences[0].Warnings).Kind);
        Assert.Empty(result.Occurrences[1].Warnings);
        Assert.Empty(result.Occurrences[2].Warnings);
    }

    private async Task<Guid> CreateSeriesAsync(AgendaHarness agenda, int count = 4)
    {
        var result = await agenda.CreateSeries.HandleAsync(
            new CreateSeriesCommand(
                agenda.Input(title: "Parches", tags: ["windows"], start: new DateTime(2026, 10, 24, 9, 0, 0)),
                new SeriesRuleInput(RecurrenceUnit.Week, 1, null, null, count), DryRun: false),
            Ct);
        return result.SeriesId!.Value;
    }

    private SeriesCommonInput Common(AgendaHarness agenda, string title, IReadOnlyList<string>? tags, Guid? responsible = null) =>
        new(agenda.DefaultType.Id, title, null, responsible ?? agenda.Responsible.Id, null, EventImpact.Degradation, "Todo", "Remoto", 30, tags);

    [Fact]
    public async Task CA05_common_fields_change_only_future_draft_or_scheduled_occurrences_and_never_the_dates()
    {
        var seriesId = await CreateSeriesAsync(_agenda);
        var occurrences = _agenda.Events.Items.OrderBy(e => e.StartsAtUtc).ToList();
        var dates = occurrences.Select(e => e.Timing).ToList();

        // 1st: scheduled and completed; 2nd: scheduled and cancelled; 3rd: scheduled; 4th: draft.
        await _agenda.Schedule.HandleAsync(new ScheduleEventsCommand([.. occurrences.Take(3).Select(e => e.Id)]), Ct);
        _agenda.Clock.Set(occurrences[0].EndsAtUtc.AddHours(1));
        await _agenda.Complete.HandleAsync(new CompleteEventCommand(occurrences[0].Id, CompletionResult.Succeeded, null, null, null), Ct);
        _agenda.Clock.Set(AgendaHarness.Now);
        await _agenda.Cancel.HandleAsync(new CancelEventCommand(occurrences[1].Id, "Se omite"), Ct);

        var newResponsible = new AgendaUser(Guid.NewGuid(), "Luis", "luis@example.com", true);
        _agenda.Directory.Users[newResponsible.Id] = newResponsible;
        var result = await _agenda.UpdateSeries.HandleAsync(new UpdateSeriesCommand(seriesId, Common(_agenda, "Parches 2026", ["iso-27001"], newResponsible.Id)), Ct);

        Assert.Equal(2, result.Updated);
        Assert.Equal([occurrences[2].Id, occurrences[3].Id], result.EventIds);
        Assert.Equal("Parches 2026", occurrences[2].Title);
        Assert.Equal(newResponsible.Id, occurrences[3].ResponsibleUserId);
        Assert.Equal(EventImpact.Degradation, occurrences[2].Impact);
        Assert.Equal(30, occurrences[2].ReminderMinutes);
        Assert.Equal("Parches", occurrences[0].Title); // completed: untouched
        Assert.Equal("Parches", occurrences[1].Title); // cancelled: untouched
        Assert.Equal(dates, occurrences.Select(e => e.Timing));
        Assert.Equal(AgendaAuditActions.SeriesUpdated, _agenda.UnitOfWork.Entries[^1].Action);
    }

    [Fact]
    public async Task CA05_occurrences_that_already_started_are_not_touched()
    {
        var seriesId = await CreateSeriesAsync(_agenda);
        var occurrences = _agenda.Events.Items.OrderBy(e => e.StartsAtUtc).ToList();
        _agenda.Clock.Set(occurrences[1].StartsAtUtc.AddMinutes(1)); // the first two have started

        var result = await _agenda.UpdateSeries.HandleAsync(new UpdateSeriesCommand(seriesId, Common(_agenda, "Nuevo", null)), Ct);

        Assert.Equal(2, result.Updated);
        Assert.Equal("Parches", occurrences[0].Title);
        Assert.Equal("Parches", occurrences[1].Title);
        Assert.Equal("Nuevo", occurrences[2].Title);
    }

    [Fact]
    public async Task CA28_the_common_edit_replaces_the_tags_of_the_updated_occurrences_and_null_keeps_them()
    {
        var seriesId = await CreateSeriesAsync(_agenda);

        await _agenda.UpdateSeries.HandleAsync(new UpdateSeriesCommand(seriesId, Common(_agenda, "A", ["iso-27001", "2026-t4"])), Ct);
        Assert.All(_agenda.Events.Items, e => Assert.Equal(["2026-t4", "iso-27001"], e.Tags.Select(t => t.Name).Order()));

        await _agenda.UpdateSeries.HandleAsync(new UpdateSeriesCommand(seriesId, Common(_agenda, "B", null)), Ct);
        Assert.All(_agenda.Events.Items, e => Assert.Equal(2, e.Tags.Count));

        await _agenda.UpdateSeries.HandleAsync(new UpdateSeriesCommand(seriesId, Common(_agenda, "C", [])), Ct);
        Assert.All(_agenda.Events.Items, e => Assert.Empty(e.Tags));
    }

    [Fact]
    public async Task The_common_edit_requires_an_active_type_and_responsible_and_an_existing_series()
    {
        var seriesId = await CreateSeriesAsync(_agenda);
        var inactive = new AgendaUser(Guid.NewGuid(), "Baja", "baja@example.com", false);
        _agenda.Directory.Users[inactive.Id] = inactive;

        await Assert.ThrowsAsync<DomainException>(() =>
            _agenda.UpdateSeries.HandleAsync(new UpdateSeriesCommand(seriesId, Common(_agenda, "X", null, inactive.Id)), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _agenda.UpdateSeries.HandleAsync(new UpdateSeriesCommand(Guid.NewGuid(), Common(_agenda, "X", null)), Ct));
    }
}
