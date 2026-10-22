using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Itmas3.Domain.Agenda;
using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itmas3.IntegrationTests.Api.Agenda;

/// <summary>SPEC-0006 CA-3, CA-5, CA-28 and CA-29: series, their preview and the edit of their common fields.</summary>
public sealed class AgendaSeriesTests(PostgreSqlFixture database) : AgendaTestBase(database)
{
    private static object Rule(string unit, int interval, int? count = null, string? endsOn = null, object? anchor = null) =>
        new { unit, interval, anchor, endsOn, count };

    private static object SeriesBody(Dictionary<string, object?> @event, object rule) => new { @event, rule };

    private static Uri SeriesUrl(Guid id) => Url($"/api/v1/agenda-series/{id}");

    private async Task<(int Series, int Events)> CountsAsync(Guid responsible)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var events = await db.AgendaEvents.CountAsync(e => e.ResponsibleUserId == responsible, Ct);
        var series = await db.AgendaEvents.Where(e => e.ResponsibleUserId == responsible && e.SeriesId != null).Select(e => e.SeriesId).Distinct().CountAsync(Ct);
        return (series, events);
    }

    private static DateOnly SecondTuesday(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        return first.AddDays(((int)DayOfWeek.Tuesday - (int)first.DayOfWeek + 7) % 7 + 7);
    }

    [Fact]
    public async Task CA03_the_preview_lists_dates_and_warnings_and_saves_nothing()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var body = SeriesBody(EventBody(responsible.Id, tags: ["nueva-etiqueta"]), Rule("week", 1, count: 4));

        var response = await admin.PostAsJsonAsync(Url("/api/v1/agenda-series?dryRun=true"), body, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var occurrences = (await ReadJsonAsync(response)).GetProperty("occurrences").EnumerateArray().ToList();
        Assert.Equal(4, occurrences.Count);
        Assert.Equal([Future(10), Future(17), Future(24), Future(31)], occurrences.Select(o => o.GetProperty("start").GetString()![..16]));
        Assert.All(occurrences, o => Assert.False(o.GetProperty("isAllDay").GetBoolean()));
        Assert.Equal((0, 0), await CountsAsync(responsible.Id));
        var tags = await ReadJsonAsync(await admin.GetAsync(Url("/api/v1/agenda-tags?search=nueva-etiqueta"), Ct));
        Assert.Equal(0, tags.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task CA03_creating_the_series_saves_the_occurrences_in_draft_with_the_tags_of_the_series()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var body = SeriesBody(EventBody(responsible.Id, tags: ["windows", "producción"]), Rule("week", 1, count: 3));

        var response = await admin.PostAsJsonAsync(Series, body, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await ReadJsonAsync(response);
        var seriesId = json.GetProperty("seriesId").GetGuid();
        var ids = json.GetProperty("eventIds").EnumerateArray().Select(item => item.GetGuid()).ToList();
        Assert.Equal(3, ids.Count);
        Assert.Equal((1, 3), await CountsAsync(responsible.Id));
        foreach (var id in ids)
        {
            var detail = await GetEventAsync(admin, id);
            Assert.Equal("draft", detail.GetProperty("status").GetString());
            Assert.Equal(seriesId, detail.GetProperty("seriesId").GetGuid());
            Assert.Equal(["producción", "windows"], detail.GetProperty("tags").EnumerateArray().Select(item => item.GetString()).Order());
            Assert.Equal("week", detail.GetProperty("series").GetProperty("unit").GetString());
            Assert.Equal(3, detail.GetProperty("series").GetProperty("count").GetInt32());
        }
    }

    [Fact]
    public async Task CA03_the_second_tuesday_of_every_month_skips_the_one_before_the_start_date()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var today = DateOnly.FromDateTime(LocalNow());
        var start = today.AddDays(1);
        var body = SeriesBody(
            EventBody(responsible.Id, start: $"{start:yyyy-MM-dd}T09:00", end: $"{start:yyyy-MM-dd}T10:00"),
            Rule("month", 1, count: 3, anchor: new { kind = "nth-weekday", nth = 2, weekday = "tuesday" }));

        var response = await admin.PostAsJsonAsync(Url("/api/v1/agenda-series?dryRun=true"), body, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var expected = new List<DateOnly>();
        for (var monthOffset = 0; expected.Count < 3; monthOffset++)
        {
            var month = new DateOnly(start.Year, start.Month, 1).AddMonths(monthOffset);
            var secondTuesday = SecondTuesday(month.Year, month.Month);
            if (secondTuesday >= start)
            {
                expected.Add(secondTuesday);
            }
        }

        var actual = (await ReadJsonAsync(response)).GetProperty("occurrences").EnumerateArray()
            .Select(o => DateOnly.ParseExact(o.GetProperty("start").GetString()![..10], "yyyy-MM-dd", CultureInfo.InvariantCulture));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task CA03_day_31_falls_on_the_last_day_of_the_shorter_months()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var start = DateOnly.FromDateTime(LocalNow()).AddDays(1);
        var body = SeriesBody(
            EventBody(responsible.Id, start: $"{start:yyyy-MM-dd}T09:00", end: $"{start:yyyy-MM-dd}T10:00"),
            Rule("month", 1, count: 5, anchor: new { kind = "day-of-month", dayOfMonth = 31 }));

        var response = await admin.PostAsJsonAsync(Url("/api/v1/agenda-series?dryRun=true"), body, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var days = (await ReadJsonAsync(response)).GetProperty("occurrences").EnumerateArray()
            .Select(o => DateOnly.ParseExact(o.GetProperty("start").GetString()![..10], "yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(5, days.Count);
        Assert.All(days, day => Assert.Equal(DateTime.DaysInMonth(day.Year, day.Month), day.Day));
        Assert.All(days, day => Assert.True(day >= start));
    }

    [Fact]
    public async Task CA03_every_90_days_keeps_a_fixed_interval()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();

        var response = await admin.PostAsJsonAsync(Url("/api/v1/agenda-series?dryRun=true"), SeriesBody(EventBody(responsible.Id), Rule("day", 90, count: 3)), Ct);

        var starts = (await ReadJsonAsync(response)).GetProperty("occurrences").EnumerateArray().Select(o => o.GetProperty("start").GetString()![..10]).ToList();
        Assert.Equal([FutureDay(10), FutureDay(100), FutureDay(190)], starts);
    }

    [Theory]
    [InlineData("day", 1, 61, null)]
    [InlineData("day", 1, null, "2999-12-31")]
    [InlineData("day", 100, 3, null)]
    [InlineData("day", 0, 3, null)]
    [InlineData("day", 1, null, null)]
    [InlineData("day", 1, 0, null)]
    public async Task CA03_more_than_60_occurrences_or_an_invalid_rule_answers_422(string unit, int interval, int? count, string? endsOn)
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var body = SeriesBody(EventBody(responsible.Id), Rule(unit, interval, count, endsOn));

        var preview = await admin.PostAsJsonAsync(Url("/api/v1/agenda-series?dryRun=true"), body, Ct);
        var create = await admin.PostAsJsonAsync(Series, body, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, preview.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, create.StatusCode);
        Assert.Equal((0, 0), await CountsAsync(responsible.Id));
    }

    [Fact]
    public async Task CA03_a_malformed_rule_answers_400()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        object[] bodies =
        [
            SeriesBody(EventBody(responsible.Id), Rule("fortnight", 1, count: 3)),
            SeriesBody(EventBody(responsible.Id), Rule("month", 1, count: 3, anchor: new { kind = "day-of-month" })),
            SeriesBody(EventBody(responsible.Id), Rule("month", 1, count: 3, anchor: new { kind = "nth-weekday", nth = 2 })),
            SeriesBody(EventBody(responsible.Id), Rule("day", 1, endsOn: "2999-02-31")),
        ];

        foreach (var body in bodies)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Series, body, Ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync(Series, Json("""{"event":{},"rule":{"unit":"day","interval":1,"count":2},"extra":1}"""), Ct)).StatusCode);
    }

    [Fact]
    public async Task CA29_a_series_that_starts_in_the_past_is_rejected()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var body = SeriesBody(EventBody(responsible.Id, start: Future(-3), end: Future(-3, 10)), Rule("week", 1, count: 3));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(Series, body, Ct)).StatusCode);
        Assert.Equal((0, 0), await CountsAsync(responsible.Id));
    }

    [Fact]
    public async Task CA11_the_preview_warns_about_the_occurrences_that_overlap_a_scheduled_freeze()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var freeze = await CreateEventAsync(admin, EventBody(responsible.Id, "Cierre", FreezeTypeId, Future(17, 0), Future(18, 0)));
        await ScheduleAsync(admin, freeze.GetProperty("id").GetGuid());
        var body = SeriesBody(EventBody(responsible.Id), Rule("week", 1, count: 3));

        var response = await admin.PostAsJsonAsync(Url("/api/v1/agenda-series?dryRun=true"), body, Ct);

        var occurrences = (await ReadJsonAsync(response)).GetProperty("occurrences").EnumerateArray().ToList();
        var freezeId = freeze.GetProperty("id").GetGuid();
        Assert.Empty(WarningsAbout(occurrences[0], freezeId));
        Assert.Equal("freeze-overlap", Assert.Single(WarningsAbout(occurrences[1], freezeId)).GetProperty("kind").GetString());
        Assert.Empty(WarningsAbout(occurrences[2], freezeId));
    }

    /// <summary>
    /// A series of five occurrences seeded "as of" the past: completed, cancelled, one that started an hour ago, a scheduled one
    /// and a draft in the future. The started one is relative to the moment of the run, so no time of day changes what a test expects.
    /// </summary>
    private async Task<(Guid SeriesId, List<Guid> Ids)> SeedSeriesAsync(Guid responsible)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Week, 1, null, DateOnly.FromDateTime(LocalNow().AddDays(-14)), null, 5);
        var recordedAt = DateTimeOffset.UtcNow.AddDays(-20);
        var series = EventSeries.Create(PatchingTypeId, pattern, recordedAt, responsible);
        db.AgendaSeries.Add(series);

        var details = EventDetails.Create(PatchingTypeId, "Parches", null, responsible, null, ["example.com"], true, EventImpact.None, null, null, 1440);
        var now = LocalNow();
        var anHourAgo = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddHours(-1);
        var ids = new List<Guid>();
        for (var index = 0; index < 5; index++)
        {
            var start = index == 2 ? anHourAgo : LocalNow().Date.AddDays((index * 7) - 14).AddHours(9);
            var schedule = EventSchedule.Timed(start, start.AddHours(2), Bogota);
            var agendaEvent = AgendaEvent.Create(details, schedule, recordedAt, responsible, series.Id);
            if (index != 4)
            {
                agendaEvent.Schedule(recordedAt, Bogota, responsible);
            }

            if (index == 0)
            {
                agendaEvent.Complete(CompletionResult.Succeeded, null, null, null, schedule.EndUtc.AddHours(1), responsible);
            }
            else if (index == 1)
            {
                agendaEvent.Cancel("Se omite", recordedAt, responsible);
            }

            db.AgendaEvents.Add(agendaEvent);
            ids.Add(agendaEvent.Id);
        }

        await db.SaveChangesAsync(Ct);
        return (series.Id, ids);
    }

    [Fact]
    public async Task CA05_CA28_the_common_fields_change_only_the_occurrences_that_have_not_started_keeping_their_dates()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var newResponsible = await UserAsync();
        var (seriesId, ids) = await SeedSeriesAsync(responsible.Id);
        var before = new List<JsonElement>();
        foreach (var id in ids)
        {
            before.Add(await GetEventAsync(admin, id));
        }

        var response = await admin.SendAsync(
            Put(
                SeriesUrl(seriesId),
                new
                {
                    typeId = PatchingTypeId,
                    title = "Parches 2026",
                    description = "Nueva descripción",
                    responsibleUserId = newResponsible.Id,
                    participants = new[] { "ana@example.com" },
                    impact = "degradation",
                    scope = "Todo",
                    location = "Oficina",
                    reminderMinutes = 30,
                    tags = new[] { "iso-27001" },
                },
                null),
            Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Only the future ones change, the scheduled one and the draft; the completed, the cancelled and the started one stay.
        Assert.Equal(2, (await ReadJsonAsync(response)).GetProperty("updated").GetInt32());

        for (var index = 0; index < ids.Count; index++)
        {
            var after = await GetEventAsync(admin, ids[index]);
            Assert.Equal(before[index].GetProperty("start").GetString(), after.GetProperty("start").GetString());
            Assert.Equal(before[index].GetProperty("end").GetString(), after.GetProperty("end").GetString());
            var wasUpdated = index >= 3;
            Assert.Equal(wasUpdated ? "Parches 2026" : "Parches", after.GetProperty("title").GetString());
            Assert.Equal(wasUpdated ? newResponsible.Id : responsible.Id, after.GetProperty("responsible").GetProperty("id").GetGuid());
            Assert.Equal(wasUpdated ? ["iso-27001"] : [], after.GetProperty("tags").EnumerateArray().Select(item => item.GetString()));
            Assert.Equal(before[index].GetProperty("status").GetString(), after.GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task CA05_the_common_edit_validates_the_new_values_and_the_series_exists()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var (seriesId, _) = await SeedSeriesAsync(responsible.Id);
        object Body(Guid type, Guid owner, string title = "Nuevo") =>
            new { typeId = type, title, responsibleUserId = owner, impact = "none" };

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.SendAsync(Put(SeriesUrl(seriesId), Body(Guid.NewGuid(), responsible.Id), null), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.SendAsync(Put(SeriesUrl(seriesId), Body(PatchingTypeId, Guid.NewGuid()), null), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.SendAsync(Put(SeriesUrl(seriesId), Body(PatchingTypeId, responsible.Id, ""), null), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.SendAsync(Put(SeriesUrl(Guid.NewGuid()), Body(PatchingTypeId, responsible.Id), null), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.SendAsync(Put(SeriesUrl(seriesId), new { typeId = PatchingTypeId, title = "x", responsibleUserId = responsible.Id, start = "2099-01-01" }, null), Ct)).StatusCode);
    }

    [Fact]
    public async Task CA28_a_common_edit_without_tags_keeps_the_tags_and_an_empty_list_removes_them()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var created = await admin.PostAsJsonAsync(Series, SeriesBody(EventBody(responsible.Id, tags: ["windows"]), Rule("week", 1, count: 2)), Ct);
        var json = await ReadJsonAsync(created);
        var seriesId = json.GetProperty("seriesId").GetGuid();
        var ids = json.GetProperty("eventIds").EnumerateArray().Select(item => item.GetGuid()).ToList();

        var keep = await admin.SendAsync(Put(SeriesUrl(seriesId), new { typeId = PatchingTypeId, title = "Sin tocar etiquetas", responsibleUserId = responsible.Id, impact = "none" }, null), Ct);
        Assert.Equal(HttpStatusCode.OK, keep.StatusCode);
        Assert.Equal(2, (await ReadJsonAsync(keep)).GetProperty("updated").GetInt32());
        foreach (var id in ids)
        {
            var kept = await GetEventAsync(admin, id);
            Assert.Equal("Sin tocar etiquetas", kept.GetProperty("title").GetString());
            Assert.Equal(["windows"], kept.GetProperty("tags").EnumerateArray().Select(item => item.GetString()));
        }

        var clear = await admin.SendAsync(Put(SeriesUrl(seriesId), new { typeId = PatchingTypeId, title = "Sin etiquetas", responsibleUserId = responsible.Id, impact = "none", tags = Array.Empty<string>() }, null), Ct);
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        Assert.Equal(2, (await ReadJsonAsync(clear)).GetProperty("updated").GetInt32());
        foreach (var id in ids)
        {
            var removed = await GetEventAsync(admin, id);
            Assert.Equal("Sin etiquetas", removed.GetProperty("title").GetString());
            Assert.Equal(0, removed.GetProperty("tags").GetArrayLength());
        }
    }
}
