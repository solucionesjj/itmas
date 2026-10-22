using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.ExportCalendar;
using Itmas3.Application.Agenda.Queries;
using Itmas3.Domain.Agenda;

namespace Itmas3.UnitTests.Application.Agenda;

/// <summary>
/// SPEC-0006 RF-21 at the use-case level: only Scheduled events whatever the filter says, the range and the other filters pass through,
/// the limit is an answer and not a silent cut, and the public address of the settings reaches the file (P-27). The query port is a double.
/// </summary>
public sealed class ExportCalendarHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private sealed class ExportQueries(AgendaExportList export) : IAgendaQueries
    {
        public List<(DateOnly From, DateOnly ToExclusive, AgendaFilter Filter)> Calls { get; } = [];

        public Task<AgendaExportList> ListExportEventsAsync(DateOnly from, DateOnly toExclusive, AgendaFilter filter, CancellationToken cancellationToken)
        {
            Calls.Add((from, toExclusive, filter));
            return Task.FromResult(export);
        }

        public Task<AgendaEventList> ListEventsAsync(DateOnly from, DateOnly toExclusive, AgendaFilter filter, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AgendaSummary> GetSummaryAsync(int year, AgendaFilter filter, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AgendaEventDetail?> GetEventAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<AgendaTagCount>> ListTagsAsync(string? search, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<AgendaEventTypeView>> ListEventTypesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AgendaEventTypeView?> GetEventTypeAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<AgendaPersonOption>> SearchPeopleAsync(string? search, int limit, Guid? meId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static AgendaExportEvent Event(string title) =>
        new(
            Guid.Parse("0196b7a0-0000-7000-8000-0000000000e1"), "Mantenimiento y parches", title, null, null, null, EventImpact.None, false,
            new DateTime(2026, 11, 10, 9, 0, 0), new DateTime(2026, 11, 10, 10, 0, 0),
            new DateTimeOffset(2026, 11, 10, 14, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 10, 15, 0, 0, TimeSpan.Zero),
            [], new DateTimeOffset(2026, 10, 10, 15, 0, 0, TimeSpan.Zero), 1);

    private static ExportCalendarHandler HandlerOf(ExportQueries queries, string? publicUrl = "https://itmas.example.com") =>
        new(
            queries,
            new AgendaSettings { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota"), PublicUrl = publicUrl },
            new AdjustableClock(Now));

    private static ExportCalendarQuery YearOf(AgendaFilter? filter = null) =>
        new(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), filter ?? AgendaFilter.None);

    [Theory]
    [InlineData(null)]
    [InlineData(AgendaStatusFilter.Draft)]
    [InlineData(AgendaStatusFilter.Completed)]
    [InlineData(AgendaStatusFilter.Cancelled)]
    [InlineData(AgendaStatusFilter.Overdue)]
    [InlineData(AgendaStatusFilter.Scheduled)]
    public async Task RF21_only_scheduled_events_are_asked_for_whatever_the_status_filter_says(AgendaStatusFilter? status)
    {
        var queries = new ExportQueries(new AgendaExportList([], false));

        await HandlerOf(queries).HandleAsync(YearOf(AgendaFilter.None with { Status = status }), Ct);

        Assert.Equal(AgendaStatusFilter.Scheduled, Assert.Single(queries.Calls).Filter.Status);
    }

    [Fact]
    public async Task RF21_the_range_and_the_other_filters_reach_the_query_unchanged()
    {
        var queries = new ExportQueries(new AgendaExportList([], false));
        var typeId = Guid.NewGuid();
        var responsibleId = Guid.NewGuid();
        var mineId = Guid.NewGuid();
        var filter = new AgendaFilter(typeId, null, responsibleId, mineId, ["windows", "producción"], "parches");

        await HandlerOf(queries).HandleAsync(new ExportCalendarQuery(new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 1), filter), Ct);

        var call = Assert.Single(queries.Calls);
        Assert.Equal(new DateOnly(2026, 3, 1), call.From);
        Assert.Equal(new DateOnly(2026, 4, 1), call.ToExclusive);
        Assert.Equal(typeId, call.Filter.TypeId);
        Assert.Equal(responsibleId, call.Filter.ResponsibleId);
        Assert.Equal(mineId, call.Filter.MineUserId);
        Assert.Equal(["windows", "producción"], call.Filter.Tags);
        Assert.Equal("parches", call.Filter.Search);
    }

    [Fact]
    public async Task CA19_the_file_has_the_events_the_name_of_the_range_and_the_clock_as_dtstamp()
    {
        var queries = new ExportQueries(new AgendaExportList([Event("Parches")], false));

        var result = await HandlerOf(queries).HandleAsync(YearOf(), Ct);

        Assert.False(result.TooMany);
        Assert.Equal(1, result.Count);
        Assert.Equal("agenda-2026-01-01_2026-12-31.ics", result.FileName);
        Assert.StartsWith("BEGIN:VCALENDAR\r\n", result.Content, StringComparison.Ordinal);
        Assert.Contains("DTSTAMP:20261018T120000Z\r\n", result.Content, StringComparison.Ordinal);
        Assert.Contains("SUMMARY:[Mantenimiento y parches] Parches\r\n", result.Content, StringComparison.Ordinal);
        // The line is longer than 75 octets, so it comes folded: a calendar program joins it again.
        Assert.Contains("URL:https://itmas.example.com/agenda/events/0196b7a0-0000-7000-8000-0000000000e1\r\n", result.Content.Replace("\r\n ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task P27_without_a_public_url_in_the_settings_the_file_has_no_url_property()
    {
        var queries = new ExportQueries(new AgendaExportList([Event("Parches")], false));

        var result = await HandlerOf(queries, publicUrl: null).HandleAsync(YearOf(), Ct);

        Assert.DoesNotContain("URL:", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RF21_the_file_name_is_made_of_the_dates_only()
    {
        var queries = new ExportQueries(new AgendaExportList([], false));

        var month = await HandlerOf(queries).HandleAsync(new ExportCalendarQuery(new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1), AgendaFilter.None), Ct);
        var leap = await HandlerOf(queries).HandleAsync(new ExportCalendarQuery(new DateOnly(2028, 1, 1), new DateOnly(2029, 1, 1), AgendaFilter.None with { Search = "../../etc/passwd" }), Ct);

        Assert.Equal("agenda-2026-02-01_2026-02-28.ics", month.FileName);
        Assert.Equal("agenda-2028-01-01_2028-12-31.ics", leap.FileName);
    }

    [Fact]
    public async Task RF21_a_range_without_scheduled_events_is_an_empty_calendar()
    {
        var result = await HandlerOf(new ExportQueries(new AgendaExportList([], false))).HandleAsync(YearOf(), Ct);

        Assert.False(result.TooMany);
        Assert.Equal(0, result.Count);
        Assert.DoesNotContain("BEGIN:VEVENT", result.Content, StringComparison.Ordinal);
        Assert.StartsWith("BEGIN:VCALENDAR", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RF21_more_events_than_the_limit_is_reported_and_no_partial_file_is_written()
    {
        var queries = new ExportQueries(new AgendaExportList([Event("Parches")], true));

        var result = await HandlerOf(queries).HandleAsync(YearOf(), Ct);

        Assert.True(result.TooMany);
        Assert.Equal(0, result.Count);
        Assert.Empty(result.Content);
    }

    [Fact]
    public async Task RF21_the_query_must_exist()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => HandlerOf(new ExportQueries(new AgendaExportList([], false))).HandleAsync(null!, Ct));
    }
}
