using System.Diagnostics;
using System.Net;
using Itmas3.IntegrationTests.Infrastructure;
using Npgsql;

namespace Itmas3.IntegrationTests.Api.Agenda;

/// <summary>
/// SPEC-0006 RNF-1: with 20 000 events (2 000 a year over ten years, two tags on most) the queries of a week or a month answer in
/// less than 500 ms and the ones of a whole year (list, summary and calendar) in less than 1 s. The events are inserted with
/// constant SQL, as the API cannot create that many; each query is warmed up once and the median of three runs is measured.
/// Runs alone, after the parallel classes (<see cref="PerformanceCollection"/>).
/// </summary>
[Collection(nameof(PerformanceCollection))]
public sealed class AgendaPerformanceTests(PostgreSqlFixture database) : AgendaTestBase(database)
{
    private const int TotalEvents = 20_000;

    private static readonly TimeSpan WeekOrMonth = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan WholeYear = TimeSpan.FromSeconds(1);

    private async Task SeedAsync(Guid responsible, string tagA, string tagB)
    {
        await using var connection = new NpgsqlConnection(Database.AdminConnectionString);
        await connection.OpenAsync(Ct);

        await using (var events = new NpgsqlCommand(
            """
            INSERT INTO app.agenda_events (
                id, type_id, title, status, is_all_day, starts_at_local, ends_at_local, time_zone, starts_at_utc, ends_at_utc,
                responsible_user_id, participants, notify_team, impact, reminder_minutes, evidence_links, outlook_status,
                created_at, created_by, revision)
            SELECT gen_random_uuid(), @type, 'Evento masivo ' || series.n,
                   CASE WHEN series.n % 5 = 0 THEN 'draft' ELSE 'scheduled' END, false,
                   series.starts, series.starts + interval '1 hour', 'America/Bogota',
                   series.starts AT TIME ZONE 'America/Bogota', (series.starts + interval '1 hour') AT TIME ZONE 'America/Bogota',
                   @responsible, '[]'::jsonb, true, CASE WHEN series.n % 10 = 0 THEN 'degradation' ELSE 'none' END, 1440, '[]'::jsonb,
                   'not-published', now(), @responsible, 1
            FROM (SELECT n, timestamp '2090-01-01 08:00' + (n * interval '263 minutes') AS starts FROM generate_series(1, @total) AS n) AS series
            """,
            connection))
        {
            events.Parameters.AddWithValue("type", PatchingTypeId);
            events.Parameters.AddWithValue("responsible", responsible);
            events.Parameters.AddWithValue("total", TotalEvents);
            Assert.Equal(TotalEvents, await events.ExecuteNonQueryAsync(Ct));
        }

        await using (var tags = new NpgsqlCommand(
            """
            INSERT INTO app.agenda_tags (id, name, created_at, created_by) VALUES (gen_random_uuid(), @a, now(), @responsible), (gen_random_uuid(), @b, now(), @responsible);
            INSERT INTO app.agenda_event_tags (event_id, tag_id)
            SELECT e.id, t.id FROM app.agenda_events e JOIN app.agenda_tags t ON t.name = @a WHERE e.responsible_user_id = @responsible;
            INSERT INTO app.agenda_event_tags (event_id, tag_id)
            SELECT e.id, t.id FROM app.agenda_events e JOIN app.agenda_tags t ON t.name = @b
            WHERE e.responsible_user_id = @responsible AND (e.starts_at_local::date - date '2090-01-01') % 3 = 0;
            """,
            connection))
        {
            tags.Parameters.AddWithValue("a", tagA);
            tags.Parameters.AddWithValue("b", tagB);
            tags.Parameters.AddWithValue("responsible", responsible);
            await tags.ExecuteNonQueryAsync(Ct);
        }
    }

    private async Task<TimeSpan> MedianAsync(HttpClient client, string path)
    {
        var uri = Url(path);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(uri, Ct)).StatusCode); // warm-up (JIT, plans, caches)
        var runs = new List<TimeSpan>();
        for (var run = 0; run < 3; run++)
        {
            var watch = Stopwatch.StartNew();
            var response = await client.GetAsync(uri, Ct);
            await response.Content.ReadAsStringAsync(Ct);
            watch.Stop();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            runs.Add(watch.Elapsed);
        }

        return runs.Order().ElementAt(1);
    }

    [Fact]
    public async Task RNF01_week_and_month_queries_take_less_than_500_ms_and_year_ones_less_than_one_second_with_20000_events()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var token = Guid.NewGuid().ToString("N")[..6];
        var (tagA, tagB) = ($"masivo-a-{token}", $"masivo-b-{token}");
        await SeedAsync(responsible.Id, tagA, tagB);
        var owner = $"responsible={responsible.Id}";

        var week = await MedianAsync(admin, $"/api/v1/agenda-events?from=2093-03-01&to=2093-03-08&{owner}");
        var month = await MedianAsync(admin, $"/api/v1/agenda-events?from=2093-03-01&to=2093-04-01&{owner}");
        var yearList = await MedianAsync(admin, $"/api/v1/agenda-events?from=2093-01-01&to=2094-01-01&{owner}");
        var yearSummary = await MedianAsync(admin, $"/api/v1/agenda-events/summary?year=2093&{owner}");
        var byTags = await MedianAsync(admin, $"/api/v1/agenda-events?from=2093-01-01&to=2094-01-01&{owner}&tags={tagA}&tags={tagB}");
        var search = await MedianAsync(admin, $"/api/v1/agenda-events?from=2093-01-01&to=2094-01-01&{owner}&search=masivo");
        var suggestions = await MedianAsync(admin, $"/api/v1/agenda-tags?search={token}");

        Assert.True(week < WeekOrMonth, $"week: {week.TotalMilliseconds:F0} ms");
        Assert.True(month < WeekOrMonth, $"month: {month.TotalMilliseconds:F0} ms");
        Assert.True(yearList < WholeYear, $"year list: {yearList.TotalMilliseconds:F0} ms");
        Assert.True(yearSummary < WholeYear, $"year summary: {yearSummary.TotalMilliseconds:F0} ms");
        Assert.True(byTags < WholeYear, $"tags filter: {byTags.TotalMilliseconds:F0} ms");
        Assert.True(search < WholeYear, $"search: {search.TotalMilliseconds:F0} ms");
        Assert.True(suggestions < WeekOrMonth, $"tag suggestions: {suggestions.TotalMilliseconds:F0} ms");
    }
}
