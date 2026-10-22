using System.Net;
using System.Text.Json;
using Itmas3.Domain.Agenda;
using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Itmas3.IntegrationTests.Api.Agenda;

/// <summary>
/// SPEC-0006 CA-10, CA-30 and the queries of the four views (CA-31 to CA-34 on the API side): lists by range, overdue,
/// filters, the year summary and the limits. Every test filters by its own responsible user, so the shared database of the
/// run does not leak between tests.
/// </summary>
public sealed class AgendaQueryTests(PostgreSqlFixture database) : AgendaTestBase(database)
{
    private static string Range(DateTime from, DateTime toExclusive) => $"from={from:yyyy-MM-dd}&to={toExclusive:yyyy-MM-dd}";

    private async Task<JsonElement> ListAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync(Url($"/api/v1/agenda-events?{query}"), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static List<Guid> Ids(JsonElement list) => [.. list.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

    [Fact]
    public async Task CA10_a_draft_or_scheduled_event_whose_end_passed_is_overdue_and_the_status_filter_includes_overdue()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var overdueScheduled = await SeedEventAsync(responsible.Id, LocalNow().AddDays(-3), "Vencida programada");
        var overdueDraft = await SeedEventAsync(responsible.Id, LocalNow().AddDays(-2), "Vencido borrador", status: EventStatus.Draft);
        var completed = await SeedEventAsync(responsible.Id, LocalNow().AddDays(-4), "Completada", status: EventStatus.Completed);
        var upcoming = await SeedEventAsync(responsible.Id, LocalNow().AddDays(3), "Próxima");
        var all = Range(LocalNow().AddDays(-10), LocalNow().AddDays(10));

        var list = await ListAsync(admin, $"{all}&responsible={responsible.Id}");

        var overdueFlags = list.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetGuid(), item => item.GetProperty("isOverdue").GetBoolean());
        Assert.True(overdueFlags[overdueScheduled]);
        Assert.True(overdueFlags[overdueDraft]);
        Assert.False(overdueFlags[completed]);
        Assert.False(overdueFlags[upcoming]);

        Assert.Equal([overdueScheduled, overdueDraft], Ids(await ListAsync(admin, $"{all}&responsible={responsible.Id}&status=overdue")));
        Assert.Equal([completed], Ids(await ListAsync(admin, $"{all}&responsible={responsible.Id}&status=completed")));
        Assert.Equal([overdueDraft], Ids(await ListAsync(admin, $"{all}&responsible={responsible.Id}&status=draft")));
        Assert.Equal([overdueScheduled, upcoming], Ids(await ListAsync(admin, $"{all}&responsible={responsible.Id}&status=scheduled")));
        var detail = await GetEventAsync(admin, overdueScheduled);
        Assert.True(detail.GetProperty("isOverdue").GetBoolean());
    }

    [Fact]
    public async Task CA30_the_list_is_chronological_and_each_item_has_the_data_of_the_year_list()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        // Days of this test alone: freezes give warnings to every event, whoever its responsible is.
        var late = await SeedEventAsync(responsible.Id, new DateTime(2181, 3, 10, 9, 0, 0), "Tardía", tags: ["b", "a"]);
        var early = await SeedEventAsync(responsible.Id, new DateTime(2181, 3, 3, 9, 0, 0), "Temprana");
        var middle = await SeedEventAsync(responsible.Id, new DateTime(2181, 3, 6, 9, 0, 0), "Intermedia", status: EventStatus.Draft);

        var list = await ListAsync(admin, $"{Range(new DateTime(2181, 3, 1), new DateTime(2181, 3, 21))}&responsible={responsible.Id}");

        Assert.Equal([early, middle, late], Ids(list));
        Assert.Equal("America/Bogota", list.GetProperty("timeZone").GetString());
        Assert.False(list.GetProperty("truncated").GetBoolean());
        var item = list.GetProperty("items").EnumerateArray().Last();
        Assert.Equal("Tardía", item.GetProperty("title").GetString());
        Assert.Equal(PatchingTypeId, item.GetProperty("type").GetProperty("id").GetGuid());
        Assert.Equal("scheduled", item.GetProperty("status").GetString());
        Assert.Equal(responsible.Id, item.GetProperty("responsible").GetProperty("id").GetGuid());
        Assert.StartsWith("Usuario ", item.GetProperty("responsible").GetProperty("name").GetString(), StringComparison.Ordinal);
        Assert.Equal(["a", "b"], item.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()));
        Assert.Equal("not-published", item.GetProperty("outlook").GetProperty("status").GetString());
        Assert.Equal("none", item.GetProperty("impact").GetString());
        Assert.False(item.GetProperty("hasWarnings").GetBoolean());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("seriesId").ValueKind);
    }

    [Fact]
    public async Task CA31_to_CA34_the_range_includes_events_that_overlap_it_in_local_time_and_all_day_events()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var day = LocalNow().Date.AddDays(40);
        var spanning = await SeedEventAsync(responsible.Id, day.AddHours(22), "Cruza la medianoche", hours: 4);
        var allDay = await SeedEventAsync(responsible.Id, day.AddDays(1), "Todo el día", allDay: true);
        var before = await SeedEventAsync(responsible.Id, day.AddDays(-2).AddHours(9), "Antes");
        var after = await SeedEventAsync(responsible.Id, day.AddDays(3).AddHours(9), "Después");

        var onlyNextDay = await ListAsync(admin, $"{Range(day.AddDays(1), day.AddDays(2))}&responsible={responsible.Id}");
        var firstDay = await ListAsync(admin, $"{Range(day, day.AddDays(1))}&responsible={responsible.Id}");
        var wide = await ListAsync(admin, $"{Range(day.AddDays(-5), day.AddDays(5))}&responsible={responsible.Id}");

        Assert.Equal([spanning, allDay], Ids(onlyNextDay));
        Assert.Equal([spanning], Ids(firstDay));
        Assert.Equal([before, spanning, allDay, after], Ids(wide));
        var allDayItem = wide.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == allDay);
        Assert.True(allDayItem.GetProperty("isAllDay").GetBoolean());
        Assert.Equal(day.AddDays(1).ToString("yyyy-MM-dd"), allDayItem.GetProperty("start").GetString());
        Assert.Equal(day.AddDays(1).ToString("yyyy-MM-dd"), allDayItem.GetProperty("end").GetString());
        var spanningItem = wide.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == spanning);
        Assert.Equal(day.AddDays(1).AddHours(2).ToString("yyyy-MM-dd'T'HH:mm:ss"), spanningItem.GetProperty("end").GetString());
    }

    [Fact]
    public async Task CA31_the_list_marks_events_with_warnings()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        // Days of this test alone: the warnings are global, an event of another test on the same days would mark "Solo".
        var first = await SeedEventAsync(responsible.Id, new DateTime(2181, 6, 10, 9, 0, 0), "Reinicio", impact: EventImpact.Outage);
        var second = await SeedEventAsync(responsible.Id, new DateTime(2181, 6, 10, 10, 0, 0), "Parches", impact: EventImpact.Degradation);
        var alone = await SeedEventAsync(responsible.Id, new DateTime(2181, 6, 12, 9, 0, 0), "Solo", impact: EventImpact.Outage);

        var list = await ListAsync(admin, $"{Range(new DateTime(2181, 6, 9), new DateTime(2181, 6, 14))}&responsible={responsible.Id}");

        var flags = list.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetGuid(), item => item.GetProperty("hasWarnings").GetBoolean());
        Assert.True(flags[first]);
        Assert.True(flags[second]);
        Assert.False(flags[alone]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("from=2090-01-01")]
    [InlineData("to=2090-01-01")]
    [InlineData("from=2090-02-01&to=2090-01-01")]
    [InlineData("from=2090-01-01&to=2090-01-01")]
    [InlineData("from=2090-01-01&to=2091-01-03")]
    [InlineData("from=ayer&to=2090-01-01")]
    [InlineData("from=2090-01-01&to=2090-02-01&status=archived")]
    [InlineData("from=2090-01-01&to=2090-02-01&type=no-es-guid")]
    public async Task CA31_the_range_is_required_and_limited_to_366_days(string query)
    {
        var admin = await AdminAsync();

        var response = await admin.GetAsync(Url($"/api/v1/agenda-events?{query}"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CA31_a_range_of_366_days_is_accepted()
    {
        var admin = await AdminAsync();

        var response = await admin.GetAsync(Url("/api/v1/agenda-events?from=2090-01-01&to=2091-01-02&responsible=" + Guid.NewGuid()), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CA31_more_than_1000_results_are_truncated_and_the_list_says_so()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        await SeedManyAsync(responsible.Id, 1001, new DateTime(2095, 1, 1));

        var list = await ListAsync(admin, $"from=2095-01-01&to=2096-01-01&responsible={responsible.Id}");

        Assert.True(list.GetProperty("truncated").GetBoolean());
        Assert.Equal(1000, list.GetProperty("items").GetArrayLength());

        var exact = await ListAsync(admin, $"from=2095-01-01&to=2095-01-03&responsible={responsible.Id}");
        Assert.False(exact.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task CA10_CA30_the_year_summary_matches_the_data_and_respects_the_filters()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var other = await UserAsync();
        const int year = 2088;
        await SeedEventAsync(responsible.Id, new DateTime(year, 3, 10, 9, 0, 0), status: EventStatus.Draft);
        await SeedEventAsync(responsible.Id, new DateTime(year, 3, 20, 9, 0, 0));
        await SeedEventAsync(responsible.Id, new DateTime(year, 3, 21, 9, 0, 0));
        await SeedEventAsync(responsible.Id, new DateTime(year, 7, 1, 9, 0, 0), status: EventStatus.Cancelled, typeId: FreezeTypeId);
        await SeedEventAsync(other.Id, new DateTime(year, 3, 22, 9, 0, 0));
        await SeedEventAsync(responsible.Id, new DateTime(year + 1, 1, 1, 9, 0, 0));

        var summary = await ReadJsonAsync(await admin.GetAsync(Url($"/api/v1/agenda-events/summary?year={year}&responsible={responsible.Id}"), Ct));

        Assert.Equal(year, summary.GetProperty("year").GetInt32());
        var months = summary.GetProperty("months").EnumerateArray().ToList();
        Assert.Equal(Enumerable.Range(1, 12), months.Select(month => month.GetProperty("month").GetInt32()));
        var march = months[2];
        Assert.Equal(1, march.GetProperty("draft").GetInt32());
        Assert.Equal(2, march.GetProperty("scheduled").GetInt32());
        Assert.Equal(0, march.GetProperty("completed").GetInt32());
        Assert.Equal(0, march.GetProperty("overdue").GetInt32());
        Assert.Equal(1, months[6].GetProperty("cancelled").GetInt32());
        Assert.All(months.Where((_, index) => index is not (2 or 6)), month =>
            Assert.Equal(0, month.GetProperty("draft").GetInt32() + month.GetProperty("scheduled").GetInt32() + month.GetProperty("cancelled").GetInt32()));
        var byType = summary.GetProperty("byType").EnumerateArray().ToDictionary(row => row.GetProperty("typeId").GetGuid(), row => row.GetProperty("count").GetInt32());
        Assert.Equal(3, byType[PatchingTypeId]);
        Assert.Equal(1, byType[FreezeTypeId]);

        var freezes = await ReadJsonAsync(await admin.GetAsync(Url($"/api/v1/agenda-events/summary?year={year}&responsible={responsible.Id}&type={FreezeTypeId}"), Ct));
        Assert.Equal(1, freezes.GetProperty("months")[6].GetProperty("cancelled").GetInt32());
        Assert.Equal(0, freezes.GetProperty("months")[2].GetProperty("scheduled").GetInt32());
    }

    [Fact]
    public async Task CA26_CA30_the_year_summary_applies_the_tags_my_activities_and_the_text_search_like_the_list()
    {
        var me = await Factory.CreateUserAsync(false, AllPermissions);
        var client = Factory.CreateClientFor(me);
        var other = await UserAsync();
        var token = Guid.NewGuid().ToString("N")[..8];
        const int year = 2087;
        await SeedEventAsync(me.Id, new DateTime(year, 4, 5, 9, 0, 0), $"Parches de Windows {token}", tags: [$"resumen-{token}"]);
        await SeedEventAsync(me.Id, new DateTime(year, 4, 6, 9, 0, 0), $"Revisión de respaldo {token}", status: EventStatus.Draft);
        await SeedEventAsync(other.Id, new DateTime(year, 4, 7, 9, 0, 0), $"Parches de Linux {token}", tags: [$"resumen-{token}"]);

        async Task<(int Draft, int Scheduled)> AprilAsync(string query)
        {
            var response = await client.GetAsync(Url($"/api/v1/agenda-events/summary?year={year}&{query}"), Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var april = (await ReadJsonAsync(response)).GetProperty("months")[3];
            return (april.GetProperty("draft").GetInt32(), april.GetProperty("scheduled").GetInt32());
        }

        Assert.Equal((0, 2), await AprilAsync($"tags=resumen-{token}"));
        Assert.Equal((1, 1), await AprilAsync("mine=true"));
        Assert.Equal((0, 1), await AprilAsync($"mine=true&tags=RESUMEN-{token}"));
        Assert.Equal((1, 2), await AprilAsync($"search={token}"));
        Assert.Equal((1, 0), await AprilAsync($"search={Uri.EscapeDataString($"respaldo {token}")}"));
        Assert.Equal((0, 1), await AprilAsync($"search={Uri.EscapeDataString($"linux {token}")}&tags=resumen-{token}"));
    }

    [Fact]
    public async Task CA10_the_summary_counts_the_overdue_ones_inside_their_status()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var past = LocalNow().AddDays(-3);
        await SeedEventAsync(responsible.Id, past, status: EventStatus.Scheduled);
        await SeedEventAsync(responsible.Id, past.AddHours(-5), status: EventStatus.Draft);
        await SeedEventAsync(responsible.Id, past.AddHours(-6), status: EventStatus.Completed);

        var summary = await ReadJsonAsync(await admin.GetAsync(Url($"/api/v1/agenda-events/summary?year={past.Year}&responsible={responsible.Id}"), Ct));

        var month = summary.GetProperty("months")[past.Month - 1];
        Assert.Equal(1, month.GetProperty("scheduled").GetInt32());
        Assert.Equal(1, month.GetProperty("draft").GetInt32());
        Assert.Equal(1, month.GetProperty("completed").GetInt32());
        Assert.Equal(2, month.GetProperty("overdue").GetInt32());
    }

    [Theory]
    [InlineData("")]
    [InlineData("year=1999")]
    [InlineData("year=2201")]
    [InlineData("year=abc")]
    public async Task CA10_the_summary_needs_a_valid_year(string query)
    {
        var admin = await AdminAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Url($"/api/v1/agenda-events/summary?{query}"), Ct)).StatusCode);
    }

    [Fact]
    public async Task RF14_the_detail_of_a_completed_event_shows_its_execution_record_and_an_unknown_event_is_404()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var id = await SeedEventAsync(responsible.Id, LocalNow().AddDays(-3), status: EventStatus.Completed);

        var detail = await GetEventAsync(admin, id);

        Assert.Equal("completed", detail.GetProperty("status").GetString());
        Assert.Equal("succeeded", detail.GetProperty("completion").GetProperty("result").GetString());
        Assert.Equal("Hecho", detail.GetProperty("completion").GetProperty("notes").GetString());
        Assert.False(detail.GetProperty("isOverdue").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(EventUrl(Guid.NewGuid()), Ct)).StatusCode);
    }

    private async Task SeedManyAsync(Guid responsible, int count, DateTime firstStart)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var details = EventDetails.Create(PatchingTypeId, "Masivo", null, responsible, null, ["example.com"], true, EventImpact.None, null, null, 60);
        var recordedAt = new DateTimeOffset(firstStart, TimeSpan.FromHours(-5)).AddDays(-3).ToUniversalTime();
        for (var index = 0; index < count; index++)
        {
            var start = firstStart.AddMinutes(index * 30);
            var agendaEvent = AgendaEvent.Create(details, EventSchedule.Timed(start, start.AddMinutes(20), Bogota), recordedAt, responsible);
            db.AgendaEvents.Add(agendaEvent);
        }

        await db.SaveChangesAsync(Ct);
    }
}
