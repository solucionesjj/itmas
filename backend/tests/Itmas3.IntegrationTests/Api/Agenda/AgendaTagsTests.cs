using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Itmas3.Domain.Agenda;
using Itmas3.IntegrationTests.Infrastructure;

namespace Itmas3.IntegrationTests.Api.Agenda;

/// <summary>SPEC-0006 CA-25, CA-26 and CA-27: free tags, suggestions, the "all tags" filter and editing in any status.</summary>
public sealed class AgendaTagsTests(PostgreSqlFixture database) : AgendaTestBase(database)
{
    private static string[] TagsOf(JsonElement json) => [.. json.GetProperty("tags").EnumerateArray().Select(item => item.GetString()!)];

    private static Uri TagsUrl(Guid id) => Url($"/api/v1/agenda-events/{id}/tags");

    [Fact]
    public async Task CA25_tags_are_trimmed_unified_ignoring_case_and_keep_the_first_spelling()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var token = Guid.NewGuid().ToString("N")[..6];

        var created = await CreateEventAsync(admin, EventBody(responsible.Id, tags: [$"  Windows-{token}  ", $"windows-{token}", $"WINDOWS-{token}", $"SQL   Server {token}"]));

        Assert.Equal([$"SQL Server {token}", $"Windows-{token}"], TagsOf(created).Order(StringComparer.OrdinalIgnoreCase));

        var second = await CreateEventAsync(admin, EventBody(responsible.Id, tags: [$"WINDOWS-{token}"]));
        Assert.Equal([$"Windows-{token}"], TagsOf(second));
    }

    [Fact]
    public async Task CA25_CA26_accented_tags_ignore_case_in_the_database_too_when_unifying_filtering_and_searching()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var token = Guid.NewGuid().ToString("N")[..6];
        var first = await CreateEventAsync(admin, EventBody(responsible.Id, tags: [$"Producción {token}"]));

        var second = await CreateEventAsync(admin, EventBody(responsible.Id, start: Future(12), tags: [$"PRODUCCIÓN {token}"]));

        Assert.Equal([$"Producción {token}"], TagsOf(second));
        var ids = new[] { first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid() };

        async Task<Guid[]> FindAsync(string query)
        {
            var response = await admin.GetAsync(Url($"/api/v1/agenda-events?from={FutureDay(0)}&to={FutureDay(30)}&responsible={responsible.Id}&{query}"), Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return [.. (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
        }

        Assert.Equal(ids, await FindAsync($"tags={Uri.EscapeDataString($"PRODUCCIÓN {token}")}"));
        Assert.Equal(ids, await FindAsync($"search={Uri.EscapeDataString($"producción {token}")}"));
    }

    [Fact]
    public async Task CA25_more_than_ten_tags_or_an_invalid_one_answer_422()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        string[][] invalid =
        [
            [.. Enumerable.Range(1, 11).Select(i => $"t{i}")],
            [new string('a', 31)],
            [""],
            ["   "],
            ["con\tcontrol"],
        ];

        foreach (var tags in invalid)
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(Events, EventBody(responsible.Id, tags: tags), Ct)).StatusCode);
        }

        var ten = await CreateEventAsync(admin, EventBody(responsible.Id, tags: [.. Enumerable.Range(1, 10).Select(i => $"t{i}")]));
        Assert.Equal(10, TagsOf(ten).Length);
        var repeatedOnes = await CreateEventAsync(admin, EventBody(responsible.Id, tags: [.. Enumerable.Range(1, 10).Select(i => $"u{i}"), "U1", "u2"]));
        Assert.Equal(10, TagsOf(repeatedOnes).Length);
    }

    [Fact]
    public async Task CA25_the_suggestions_list_existing_tags_with_their_number_of_events()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var token = Guid.NewGuid().ToString("N")[..6];
        await CreateEventAsync(admin, EventBody(responsible.Id, tags: [$"Linux-{token}", $"prod-{token}"]));
        await CreateEventAsync(admin, EventBody(responsible.Id, tags: [$"linux-{token}"]));
        await CreateEventAsync(admin, EventBody(responsible.Id, tags: [$"LINUX-{token}", $"Prod-{token}"]));

        var json = await ReadJsonAsync(await admin.GetAsync(Url($"/api/v1/agenda-tags?search={token}"), Ct));

        var items = json.GetProperty("items").EnumerateArray().Select(item => (item.GetProperty("name").GetString()!, item.GetProperty("count").GetInt32())).ToList();
        Assert.Equal([($"Linux-{token}", 3), ($"prod-{token}", 2)], items);

        var limited = await ReadJsonAsync(await admin.GetAsync(Url($"/api/v1/agenda-tags?search={token}&limit=1"), Ct));
        Assert.Equal(1, limited.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Url("/api/v1/agenda-tags?limit=51"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Url("/api/v1/agenda-tags?limit=0"), Ct)).StatusCode);
    }

    [Fact]
    public async Task CA25_a_tag_that_lost_all_its_events_is_not_suggested_and_a_special_search_does_not_act_as_a_pattern()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var token = Guid.NewGuid().ToString("N")[..6];
        var created = await CreateEventAsync(admin, EventBody(responsible.Id, tags: [$"efímera-{token}"]));
        await admin.DeleteAsync(EventUrl(created.GetProperty("id").GetGuid()), Ct);

        var none = await ReadJsonAsync(await admin.GetAsync(Url($"/api/v1/agenda-tags?search={token}"), Ct));
        Assert.Equal(0, none.GetProperty("items").GetArrayLength());

        var wildcard = await ReadJsonAsync(await admin.GetAsync(Url("/api/v1/agenda-tags?search=%25"), Ct));
        Assert.All(wildcard.GetProperty("items").EnumerateArray(), item => Assert.Contains("%", item.GetProperty("name").GetString()!, StringComparison.Ordinal));

        // The tag is reused with its original spelling if somebody uses it again.
        var again = await CreateEventAsync(admin, EventBody(responsible.Id, tags: [$"EFÍMERA-{token}"]));
        Assert.Equal([$"efímera-{token}"], TagsOf(again));
    }

    [Fact]
    public async Task CA26_filtering_by_several_tags_returns_only_the_events_that_have_all_of_them()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var token = Guid.NewGuid().ToString("N")[..6];
        var a = $"a-{token}";
        var b = $"b-{token}";
        var onlyA = await SeedEventAsync(responsible.Id, LocalNow().AddDays(5), "Solo A", tags: [a]);
        var both = await SeedEventAsync(responsible.Id, LocalNow().AddDays(6), "Ambas", tags: [a, b]);
        await SeedEventAsync(responsible.Id, LocalNow().AddDays(7), "Solo B", tags: [b]);
        await SeedEventAsync(responsible.Id, LocalNow().AddDays(8), "Ninguna");

        async Task<List<Guid>> Search(string query)
        {
            var response = await admin.GetAsync(Url($"/api/v1/agenda-events?from={FutureDay(0)}&to={FutureDay(30)}&responsible={responsible.Id}{query}"), Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return [.. (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
        }

        Assert.Equal([onlyA, both], await Search($"&tags={a}"));
        Assert.Equal([both], await Search($"&tags={a}&tags={b}"));
        Assert.Equal([both], await Search($"&tags={a.ToUpperInvariant()}&tags={b}"));
        Assert.Empty(await Search($"&tags={a}&tags=no-existe-{token}"));
        Assert.Equal(4, (await Search(string.Empty)).Count);
    }

    [Fact]
    public async Task CA26_the_tag_filter_normalizes_like_the_tags_spaces_and_case_and_a_text_that_cannot_be_a_tag_matches_nothing()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var token = Guid.NewGuid().ToString("N")[..6];
        var tagged = await SeedEventAsync(responsible.Id, LocalNow().AddDays(5), "Con etiqueta", tags: [$"SQL Server {token}"]);
        await SeedEventAsync(responsible.Id, LocalNow().AddDays(6), "Sin etiqueta");

        async Task<List<Guid>> Search(string tag)
        {
            var response = await admin.GetAsync(
                Url($"/api/v1/agenda-events?from={FutureDay(0)}&to={FutureDay(30)}&responsible={responsible.Id}&tags={Uri.EscapeDataString(tag)}"),
                Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return [.. (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
        }

        Assert.Equal([tagged], await Search($"  sql   server {token} "));
        Assert.Equal([tagged], await Search($"SQL SERVER {token}"));
        Assert.Empty(await Search(new string('x', 40)));
    }

    [Fact]
    public async Task CA26_the_tag_filter_combines_with_status_type_responsible_mine_and_the_text_search_finds_by_tag()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var other = await UserAsync();
        var token = Guid.NewGuid().ToString("N")[..6];
        var tag = $"combo-{token}";
        var scheduled = await SeedEventAsync(responsible.Id, LocalNow().AddDays(5), "Programada", tags: [tag]);
        var draft = await SeedEventAsync(responsible.Id, LocalNow().AddDays(6), "Borrador", status: EventStatus.Draft, tags: [tag]);
        var freeze = await SeedEventAsync(responsible.Id, LocalNow().AddDays(7), "Congelamiento", typeId: FreezeTypeId, tags: [tag]);
        var foreign = await SeedEventAsync(other.Id, LocalNow().AddDays(8), "De otra persona", tags: [tag]);

        async Task<List<Guid>> Search(string query)
        {
            var response = await admin.GetAsync(Url($"/api/v1/agenda-events?from={FutureDay(0)}&to={FutureDay(30)}&tags={tag}{query}"), Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return [.. (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
        }

        Assert.Equal(4, (await Search(string.Empty)).Count);
        Assert.Equal([draft], await Search("&status=draft"));
        Assert.Equal([scheduled, freeze, foreign], await Search("&status=scheduled"));
        Assert.Equal([freeze], await Search($"&type={FreezeTypeId}"));
        Assert.Equal([scheduled, draft, freeze], await Search($"&responsible={responsible.Id}"));
        Assert.Equal([foreign], await Search($"&responsible={other.Id}"));
        Assert.Equal([scheduled], await Search($"&responsible={responsible.Id}&status=scheduled&type={PatchingTypeId}"));

        // The text search also looks in the tags, ignoring case.
        var byTag = await admin.GetAsync(Url($"/api/v1/agenda-events?from={FutureDay(0)}&to={FutureDay(30)}&search={tag.ToUpperInvariant()}"), Ct);
        Assert.Equal(4, (await ReadJsonAsync(byTag)).GetProperty("items").GetArrayLength());
        var byTitle = await admin.GetAsync(Url($"/api/v1/agenda-events?from={FutureDay(0)}&to={FutureDay(30)}&tags={tag}&search=borrador"), Ct);
        Assert.Equal([draft], (await ReadJsonAsync(byTitle)).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task CA26_my_activities_returns_the_events_whose_responsible_is_the_signed_in_user()
    {
        var responsible = await Factory.CreateUserAsync(false, AllPermissions);
        var other = await UserAsync();
        var token = Guid.NewGuid().ToString("N")[..6];
        var mineId = await SeedEventAsync(responsible.Id, LocalNow().AddDays(5), "Mía", tags: [$"m-{token}"]);
        await SeedEventAsync(other.Id, LocalNow().AddDays(6), "Ajena", tags: [$"m-{token}"]);
        var client = Factory.CreateClientFor(responsible);

        var response = await client.GetAsync(Url($"/api/v1/agenda-events?from={FutureDay(0)}&to={FutureDay(30)}&tags=m-{token}&mine=true"), Ct);

        Assert.Equal([mineId], (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task CA27_tags_are_edited_in_any_status_without_changing_the_etag_and_with_audit()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var token = Guid.NewGuid().ToString("N")[..6];
        var ids = new[]
        {
            await SeedEventAsync(responsible.Id, LocalNow().AddDays(5), status: EventStatus.Draft),
            await SeedEventAsync(responsible.Id, LocalNow().AddDays(5), status: EventStatus.Scheduled),
            await SeedEventAsync(responsible.Id, LocalNow().AddDays(-2), status: EventStatus.Completed),
            await SeedEventAsync(responsible.Id, LocalNow().AddDays(5), status: EventStatus.Cancelled),
        };

        foreach (var id in ids)
        {
            var before = await admin.GetAsync(EventUrl(id), Ct);
            var etagBefore = before.Headers.ETag?.Tag;

            var response = await admin.SendAsync(Put(TagsUrl(id), new { tags = new[] { $"nueva-{token}", "otra" } }, null), Ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal([$"nueva-{token}", "otra"], TagsOf(await ReadJsonAsync(response)).Order());
            var after = await admin.GetAsync(EventUrl(id), Ct);
            Assert.Equal(etagBefore, after.Headers.ETag?.Tag);
            Assert.Equal([$"nueva-{token}", "otra"], TagsOf(await ReadJsonAsync(after)).Order());
        }

        var cleared = await admin.SendAsync(Put(TagsUrl(ids[0]), new { tags = Array.Empty<string>() }, null), Ct);
        Assert.Empty(TagsOf(await ReadJsonAsync(cleared)));
        var audit = await AuditAsync("agenda.event.tags-updated");
        Assert.True(audit.Count(row => ids.Select(id => id.ToString()).Contains(row.ResourceId)) >= 5);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.SendAsync(Put(TagsUrl(Guid.NewGuid()), new { tags = new[] { "x" } }, null), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.SendAsync(Put(TagsUrl(ids[0]), new { tags = Enumerable.Range(1, 11).Select(i => $"t{i}") }, null), Ct)).StatusCode);
    }

    [Fact]
    public async Task CA27_editing_tags_needs_agenda_update()
    {
        var reader = await Factory.CreateAuthenticatedClientAsync(false, ReadAndCreate());
        var responsible = await UserAsync();
        var id = await SeedEventAsync(responsible.Id, LocalNow().AddDays(5));

        var response = await reader.SendAsync(Put(TagsUrl(id), new { tags = new[] { "x" } }, null), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string[] ReadAndCreate() => [Itmas3.Application.Agenda.AgendaPermissions.Read, Itmas3.Application.Agenda.AgendaPermissions.Create];
}
