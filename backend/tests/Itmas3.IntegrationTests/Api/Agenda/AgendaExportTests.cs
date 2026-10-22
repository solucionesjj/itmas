using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Itmas3.Domain.Agenda;
using Itmas3.IntegrationTests.Infrastructure;
using Npgsql;

namespace Itmas3.IntegrationTests.Api.Agenda;

/// <summary>
/// SPEC-0006 RF-21 and CA-19 with the real API and PostgreSQL: <c>GET /agenda-events/export.ics</c> exports only the Scheduled events of
/// the range with the filters of the list, as an attachment of type <c>text/calendar</c>, with a stable <c>UID</c>, the type and the tags as
/// categories, escaped text and no people. The shape of the file itself is tested against fixtures in the unit tests; here the point is
/// what the endpoint selects and how it answers. Every test filters by its own responsible user, so the shared database does not leak.
/// </summary>
public sealed class AgendaExportTests(PostgreSqlFixture database) : AgendaTestBase(database)
{
    // Scheduled change freezes are global: they raise a warning on every overlapping event of every test sharing the database.
    // These tests never schedule one; the type filter uses the second seeded type, "Pruebas de vulnerabilidad", which is not a freeze.
    private static readonly Guid VulnerabilityTypeId = Guid.Parse("019ba5c0-0000-7000-8000-000000000002");

    private static string Range(DateTime from, DateTime toExclusive) => $"from={from:yyyy-MM-dd}&to={toExclusive:yyyy-MM-dd}";

    private static string Window() => Range(LocalNow().Date, LocalNow().Date.AddDays(60));

    private async Task<(HttpResponseMessage Response, string Body)> ExportAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync(Url($"/api/v1/agenda-events/export.ics?{query}"), Ct);
        return (response, await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task<string> ExportOkAsync(HttpClient client, string query)
    {
        var (response, body) = await ExportAsync(client, query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body;
    }

    /// <summary>The lines as a calendar program reads them: folded lines joined.</summary>
    private static string[] Unfolded(string ics) => [.. ics.Replace("\r\n ", string.Empty, StringComparison.Ordinal).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)];

    private static List<string> Uids(string ics) =>
        [.. Unfolded(ics).Where(line => line.StartsWith("UID:", StringComparison.Ordinal)).Select(line => line["UID:".Length..])];

    private static string UidOf(Guid id) => $"{id:D}@agenda.itmas";

    private static string Summaries(string ics) => string.Join('|', Unfolded(ics).Where(line => line.StartsWith("SUMMARY:", StringComparison.Ordinal)));

    // ---- What is exported ------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task CA19_only_scheduled_events_of_the_range_are_exported()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var day = LocalNow().Date;
        var scheduled = await SeedEventAsync(responsible.Id, day.AddDays(5).AddHours(9), "Programada en el rango");
        var draft = await SeedEventAsync(responsible.Id, day.AddDays(6).AddHours(9), "Borrador", status: EventStatus.Draft);
        var completed = await SeedEventAsync(responsible.Id, day.AddDays(7).AddHours(9), "Completada", status: EventStatus.Completed);
        var cancelled = await SeedEventAsync(responsible.Id, day.AddDays(8).AddHours(9), "Cancelada", status: EventStatus.Cancelled);
        var outside = await SeedEventAsync(responsible.Id, day.AddDays(80).AddHours(9), "Programada fuera del rango");
        var overdue = await SeedEventAsync(responsible.Id, day.AddDays(-3).AddHours(9), "Programada vencida");

        var ics = await ExportOkAsync(admin, $"{Range(day.AddDays(-5), day.AddDays(30))}&responsible={responsible.Id}");

        Assert.Equal([UidOf(overdue), UidOf(scheduled)], Uids(ics)); // chronological; an overdue scheduled event is still scheduled
        Assert.DoesNotContain(UidOf(draft), ics, StringComparison.Ordinal);
        Assert.DoesNotContain(UidOf(completed), ics, StringComparison.Ordinal);
        Assert.DoesNotContain(UidOf(cancelled), ics, StringComparison.Ordinal);
        Assert.DoesNotContain(UidOf(outside), ics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RF21_the_status_filter_of_the_list_does_not_apply_the_export_is_always_scheduled()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var scheduled = await SeedEventAsync(responsible.Id, LocalNow().Date.AddDays(5).AddHours(9), "Programada");
        await SeedEventAsync(responsible.Id, LocalNow().Date.AddDays(6).AddHours(9), "Borrador", status: EventStatus.Draft);
        var query = $"{Window()}&responsible={responsible.Id}";

        foreach (var status in new[] { "draft", "completed", "cancelled", "overdue", "scheduled" })
        {
            Assert.Equal([UidOf(scheduled)], Uids(await ExportOkAsync(admin, $"{query}&status={status}")));
        }
    }

    [Fact]
    public async Task CA19_the_range_includes_events_that_overlap_it_and_excludes_the_end_date()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var day = LocalNow().Date.AddDays(40);
        var spanning = await SeedEventAsync(responsible.Id, day.AddDays(-1).AddHours(22), "Entra al rango", hours: 4);
        var inside = await SeedEventAsync(responsible.Id, day.AddHours(10), "Dentro");
        var startsAtEnd = await SeedEventAsync(responsible.Id, day.AddDays(2).AddHours(9), "Empieza el día final");
        var before = await SeedEventAsync(responsible.Id, day.AddDays(-3).AddHours(9), "Antes");

        var ics = await ExportOkAsync(admin, $"{Range(day, day.AddDays(2))}&responsible={responsible.Id}");

        Assert.Equal([UidOf(spanning), UidOf(inside)], Uids(ics));
        Assert.DoesNotContain(UidOf(startsAtEnd), ics, StringComparison.Ordinal);
        Assert.DoesNotContain(UidOf(before), ics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CA19_the_filters_of_the_list_apply_type_responsible_tags_mine_and_search()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var other = await UserAsync();
        var day = LocalNow().Date;
        var windowsProd = await SeedEventAsync(responsible.Id, day.AddDays(3).AddHours(9), $"Parches {token}", tags: [$"win-{token}", $"prod-{token}"]);
        var windowsOnly = await SeedEventAsync(responsible.Id, day.AddDays(4).AddHours(9), $"Revisión {token}", tags: [$"win-{token}"]);
        var scan = await SeedEventAsync(responsible.Id, day.AddDays(5), $"Escaneo {token}", typeId: VulnerabilityTypeId, tags: [$"win-{token}"], allDay: true);
        var others = await SeedEventAsync(other.Id, day.AddDays(6).AddHours(9), $"De otra persona {token}", tags: [$"win-{token}", $"prod-{token}"]);
        var window = Window();

        // Every tag asked for is required (RF-25), and the case does not matter.
        Assert.Equal([UidOf(windowsProd), UidOf(others)], Uids(await ExportOkAsync(admin, $"{window}&tags=WIN-{token}&tags=Prod-{token}")));
        Assert.Equal([UidOf(windowsProd), UidOf(windowsOnly), UidOf(scan), UidOf(others)], Uids(await ExportOkAsync(admin, $"{window}&tags=win-{token}")));
        Assert.Equal([UidOf(windowsProd)], Uids(await ExportOkAsync(admin, $"{window}&tags=win-{token}&tags=prod-{token}&responsible={responsible.Id}")));

        Assert.Equal([UidOf(scan)], Uids(await ExportOkAsync(admin, $"{window}&tags=win-{token}&type={VulnerabilityTypeId}")));
        Assert.Equal([UidOf(others)], Uids(await ExportOkAsync(admin, $"{window}&responsible={other.Id}")));
        Assert.Equal([UidOf(windowsOnly)], Uids(await ExportOkAsync(admin, $"{window}&search={Uri.EscapeDataString($"Revisión {token}")}")));
        Assert.Equal([UidOf(others)], Uids(await ExportOkAsync(admin, $"{window}&search=prod-{token}&responsible={other.Id}"))); // the search also looks in tags (P-18)

        var me = await Factory.CreateUserAsync(false, AllPermissions);
        var mine = await SeedEventAsync(me.Id, day.AddDays(7).AddHours(9), $"Mía {token}", tags: [$"mine-{token}"]);
        await SeedEventAsync(responsible.Id, day.AddDays(7).AddHours(10), $"No mía {token}", tags: [$"mine-{token}"]);
        var meClient = Factory.CreateClientFor(me);
        Assert.Equal([UidOf(mine)], Uids(await ExportOkAsync(meClient, $"{window}&tags=mine-{token}&mine=true")));
    }

    [Fact]
    public async Task RF21_a_range_without_scheduled_events_is_an_empty_calendar()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();

        var ics = await ExportOkAsync(admin, $"{Window()}&responsible={responsible.Id}");

        Assert.Empty(Uids(ics));
        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics, StringComparison.Ordinal);
        Assert.EndsWith("END:VCALENDAR\r\n", ics, StringComparison.Ordinal);
    }

    // ---- The file --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task CA19_the_response_is_an_attachment_of_type_text_calendar_in_utf8_that_is_never_cached_or_sniffed()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        await SeedEventAsync(responsible.Id, LocalNow().Date.AddDays(5).AddHours(9), "Parches");
        var from = LocalNow().Date;

        var (response, body) = await ExportAsync(admin, $"{Range(from, from.AddDays(30))}&responsible={responsible.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal($"agenda-{from:yyyy-MM-dd}_{from.AddDays(29):yyyy-MM-dd}.ics", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));

        // UTF-8 without a byte-order mark and CRLF ends.
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "The file must not start with a byte-order mark.");
        Assert.StartsWith("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n", body, StringComparison.Ordinal);
        Assert.Equal(body.Count(c => c == '\r'), body.Count(c => c == '\n'));
    }

    [Fact]
    public async Task CA19_a_timed_event_carries_its_data_in_utc_with_the_type_and_the_tags_as_categories()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var body = EventBody(responsible.Id, title: $"Parches {token}", tags: [$"windows-{token}", $"2026-t4-{token}"], impact: "degradation");
        var created = await CreateEventAsync(admin, body);
        var id = created.GetProperty("id").GetGuid();
        await ScheduleAsync(admin, id);

        var ics = await ExportOkAsync(admin, $"{Window()}&responsible={responsible.Id}");
        var lines = Unfolded(ics);

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.ParseExact(Future(10), "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture), Bogota);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.ParseExact(Future(10, 11), "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture), Bogota);
        Assert.Equal(UidOf(id), Assert.Single(Uids(ics)));
        Assert.Contains($"DTSTART:{startUtc:yyyyMMdd'T'HHmmss}Z", lines);
        Assert.Contains($"DTEND:{endUtc:yyyyMMdd'T'HHmmss}Z", lines);
        Assert.Contains($"SUMMARY:[Mantenimiento y parches de sistema operativo] Parches {token}", lines);
        Assert.Contains("LOCATION:Remoto", lines);
        Assert.Contains("DESCRIPTION:Aplicar los parches del mes\\n\\nAlcance: Servidores Windows\\n\\nImpacto: Degradación del servicio", lines);
        Assert.Contains($"CATEGORIES:Mantenimiento y parches de sistema operativo,2026-t4-{token},windows-{token}", lines);
        Assert.Contains("STATUS:CONFIRMED", lines);
        Assert.Contains("SEQUENCE:1", lines); // revision 1 at creation and 2 once scheduled
        Assert.Equal(1, lines.Count(line => line == "BEGIN:VEVENT"));
    }

    [Fact]
    public async Task CA19_an_all_day_event_is_exported_as_dates_with_the_end_on_the_day_after_the_last_day()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var created = await CreateEventAsync(admin, EventBody(responsible.Id, title: "Parches de fin de año", isAllDay: true, start: FutureDay(20), end: FutureDay(21)));
        await ScheduleAsync(admin, created.GetProperty("id").GetGuid());

        var lines = Unfolded(await ExportOkAsync(admin, $"{Window()}&responsible={responsible.Id}"));

        var first = DateTime.ParseExact(FutureDay(20), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Contains($"DTSTART;VALUE=DATE:{first:yyyyMMdd}", lines);
        Assert.Contains($"DTEND;VALUE=DATE:{first.AddDays(2):yyyyMMdd}", lines); // the last day is the 21st: the end is the 22nd (P-24)
        Assert.DoesNotContain(lines, line => line.StartsWith("DTSTART:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CA19_semicolons_commas_and_line_breaks_typed_in_the_activity_are_escaped_and_cannot_start_a_property()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var body = EventBody(responsible.Id, title: "Cambio; de, claves\\ y más");
        body["description"] = "Línea 1; con, signos\r\nATTENDEE:mailto:eve@example.com\nDTSTART:20300101T000000Z\r\nEND:VEVENT\r\nBEGIN:VEVENT";
        body["scope"] = "Servidores; red, y más";
        body["location"] = "Sala A; piso 2, edificio B";
        var created = await CreateEventAsync(admin, body);
        var id = created.GetProperty("id").GetGuid();
        await ScheduleAsync(admin, id);

        var ics = await ExportOkAsync(admin, $"{Window()}&responsible={responsible.Id}");
        var lines = Unfolded(ics);

        Assert.Contains("SUMMARY:[Mantenimiento y parches de sistema operativo] Cambio\\; de\\, claves\\\\ y más", lines);
        Assert.Contains(
            "DESCRIPTION:Línea 1\\; con\\, signos\\nATTENDEE:mailto:eve@example.com\\nDTSTART:20300101T000000Z\\nEND:VEVENT\\nBEGIN:VEVENT\\n\\nAlcance: Servidores\\; red\\, y más\\n\\nImpacto: Ninguno",
            lines);
        Assert.Contains("LOCATION:Sala A\\; piso 2\\, edificio B", lines);
        Assert.Equal(1, lines.Count(line => line == "BEGIN:VEVENT"));
        Assert.Equal(1, lines.Count(line => line == "END:VEVENT"));
        Assert.Equal(1, lines.Count(line => line.StartsWith("DTSTART", StringComparison.Ordinal)));
        Assert.DoesNotContain(lines, line => line.StartsWith("ATTENDEE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CA19_a_title_with_line_breaks_is_refused_when_the_activity_is_created_so_it_never_reaches_the_file()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();

        var response = await admin.PostAsJsonAsync(Events, EventBody(responsible.Id, title: "Uno\r\nATTENDEE:mailto:eve@example.com"), Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CA19_the_uid_is_stable_across_exports_and_changes_but_the_sequence_grows()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var created = await CreateEventAsync(admin, EventBody(responsible.Id, title: "Estable"));
        var id = created.GetProperty("id").GetGuid();
        await ScheduleAsync(admin, id);
        var query = $"{Window()}&responsible={responsible.Id}";
        var first = await ExportOkAsync(admin, query);

        // Change the title of the scheduled event (revision 2 → 3).
        var detail = await admin.GetAsync(EventUrl(id), Ct);
        var revision = int.Parse(detail.Headers.ETag!.Tag.Trim('"'), CultureInfo.InvariantCulture);
        var update = EventBody(responsible.Id, title: "Estable y cambiado");
        var updated = await admin.SendAsync(Put(EventUrl(id), update, revision), Ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var second = await ExportOkAsync(admin, query);

        Assert.Equal(Uids(first), Uids(second));
        Assert.Equal(UidOf(id), Assert.Single(Uids(second)));
        Assert.Contains("SUMMARY:[Mantenimiento y parches de sistema operativo] Estable", Unfolded(first));
        Assert.Contains("SUMMARY:[Mantenimiento y parches de sistema operativo] Estable y cambiado", Unfolded(second));
        Assert.Contains($"SEQUENCE:{revision - 1}", Unfolded(first));
        Assert.Contains($"SEQUENCE:{revision}", Unfolded(second));
    }

    [Fact]
    public async Task RF21_the_file_has_no_people_not_the_responsible_nor_any_address_and_no_url_without_a_public_address()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var created = await CreateEventAsync(admin, EventBody(responsible.Id, participants: ["luis@example.com"]));
        await ScheduleAsync(admin, created.GetProperty("id").GetGuid());
        var user = await ReadJsonAsync(await admin.GetAsync(EventUrl(created.GetProperty("id").GetGuid()), Ct));
        var responsibleName = user.GetProperty("responsible").GetProperty("name").GetString()!;

        var ics = await ExportOkAsync(admin, $"{Window()}&responsible={responsible.Id}");

        Assert.DoesNotContain(responsibleName, ics, StringComparison.Ordinal);
        Assert.DoesNotContain("luis@example.com", ics, StringComparison.Ordinal);
        Assert.DoesNotContain("mailto:", ics, StringComparison.Ordinal);
        Assert.DoesNotContain("ATTENDEE", ics, StringComparison.Ordinal);
        Assert.DoesNotContain("ORGANIZER", ics, StringComparison.Ordinal);
        // The test API has no AGENDA_PUBLIC_URL: the URL property is left out (P-27).
        Assert.DoesNotContain(Unfolded(ics), line => line.StartsWith("URL", StringComparison.Ordinal));
    }

    // ---- Limits and validation -------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("from=2090-01-01")] // no end
    [InlineData("to=2090-02-01")] // no start
    [InlineData("")] // neither
    [InlineData("from=2090-02-01&to=2090-02-01")] // empty range
    [InlineData("from=2090-02-02&to=2090-02-01")] // reversed
    [InlineData("from=2090-01-01&to=2091-01-03")] // 367 days
    [InlineData("from=hoy&to=mañana")] // not dates
    public async Task CA19_an_invalid_range_is_a_400_with_problem_details(string query)
    {
        var admin = await AdminAsync();

        var (response, body) = await ExportAsync(admin, query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("BEGIN:VCALENDAR", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CA19_a_range_of_366_days_is_the_longest_accepted()
    {
        var admin = await AdminAsync();
        // Only the length of the range is under test: a responsible of its own keeps the events of other tests (and the limit
        // of 1000 events) out of the answer.
        var nobody = await UserAsync();

        Assert.Equal(HttpStatusCode.OK, (await ExportAsync(admin, $"from=2092-01-01&to=2093-01-01&responsible={nobody.Id}")).Response.StatusCode); // 2092 is a leap year: 366 days
        Assert.Equal(HttpStatusCode.BadRequest, (await ExportAsync(admin, $"from=2092-01-01&to=2093-01-02&responsible={nobody.Id}")).Response.StatusCode); // 367
    }

    [Fact]
    public async Task RF21_exactly_1000_events_are_exported_and_one_more_is_a_400_instead_of_a_partial_file()
    {
        var admin = await AdminAsync();
        var exact = await UserAsync();
        var over = await UserAsync();
        await SeedManyAsync(exact.Id, 1000);
        await SeedManyAsync(over.Id, 1001);
        var query = "from=2091-01-01&to=2091-02-01";

        var ok = await ExportAsync(admin, $"{query}&responsible={exact.Id}");
        var tooMany = await ExportAsync(admin, $"{query}&responsible={over.Id}");

        Assert.Equal(HttpStatusCode.OK, ok.Response.StatusCode);
        Assert.Equal(1000, Unfolded(ok.Body).Count(line => line == "BEGIN:VEVENT"));
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.Response.StatusCode);
        Assert.Equal("application/problem+json", tooMany.Response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Acota el periodo o los filtros", tooMany.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("BEGIN:VEVENT", tooMany.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RF21_exporting_does_not_audit_or_change_anything()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var id = await SeedEventAsync(responsible.Id, LocalNow().Date.AddDays(5).AddHours(9), "Intacta");
        var revisionBefore = (await GetEventAsync(admin, id)).GetProperty("revision").GetInt32();

        await ExportOkAsync(admin, $"{Window()}&responsible={responsible.Id}");

        // Other tests run in parallel and write their own audit entries: look only at this event.
        Assert.DoesNotContain(await AuditAsync("agenda."), entry => entry.ResourceId == id.ToString());
        var detail = await GetEventAsync(admin, id);
        Assert.Equal("scheduled", detail.GetProperty("status").GetString());
        Assert.Equal(revisionBefore, detail.GetProperty("revision").GetInt32());
    }

    /// <summary>Inserts <paramref name="count"/> scheduled events of the user, ten minutes apart from 2091-01-01, with constant SQL.</summary>
    private async Task SeedManyAsync(Guid responsible, int count)
    {
        await using var connection = new NpgsqlConnection(Database.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO app.agenda_events (
                id, type_id, title, status, is_all_day, starts_at_local, ends_at_local, time_zone, starts_at_utc, ends_at_utc,
                responsible_user_id, participants, notify_team, impact, reminder_minutes, evidence_links, outlook_status,
                created_at, created_by, revision)
            SELECT gen_random_uuid(), @type, 'Evento masivo ' || series.n, 'scheduled', false,
                   series.starts, series.starts + interval '5 minutes', 'America/Bogota',
                   series.starts AT TIME ZONE 'America/Bogota', (series.starts + interval '5 minutes') AT TIME ZONE 'America/Bogota',
                   @responsible, '[]'::jsonb, true, 'none', 1440, '[]'::jsonb, 'not-published', now(), @responsible, 1
            FROM (SELECT n, timestamp '2091-01-01 08:00' + (n * interval '10 minutes') AS starts FROM generate_series(1, @total) AS n) AS series
            """,
            connection);
        command.Parameters.AddWithValue("type", PatchingTypeId);
        command.Parameters.AddWithValue("responsible", responsible);
        command.Parameters.AddWithValue("total", count);
        Assert.Equal(count, await command.ExecuteNonQueryAsync(Ct));
    }
}
