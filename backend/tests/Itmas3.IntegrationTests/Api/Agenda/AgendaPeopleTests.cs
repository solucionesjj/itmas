using System.Net;
using System.Text.Json;
using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itmas3.IntegrationTests.Api.Agenda;

/// <summary>SPEC-0006 P-31: the people that can be chosen as the responsible, opened by <c>agenda.read</c> and without e-mails.</summary>
public sealed class AgendaPeopleTests(PostgreSqlFixture database) : AgendaTestBase(database)
{
    private static readonly Uri People = Url("/api/v1/agenda-people");

    private async Task RenameAsync(Guid userId, string displayName, bool? isActive = null)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(item => item.Id == userId, Ct);
        user.RecordSignIn(displayName, user.Email, DateTimeOffset.UtcNow);
        if (isActive is { } active)
        {
            user.SetActive(active);
        }

        await db.SaveChangesAsync(Ct);
        await ApiFactory.BumpAuthorizationVersionAsync(db);
    }

    private static List<(Guid Id, string Name, bool IsMe)> Read(JsonElement json) =>
        [.. json.GetProperty("items").EnumerateArray().Select(item => (item.GetProperty("id").GetGuid(), item.GetProperty("name").GetString()!, item.GetProperty("isMe").GetBoolean()))];

    [Fact]
    public async Task P31_it_lists_active_users_by_name_without_their_email_and_marks_the_caller()
    {
        var token = Guid.NewGuid().ToString("N")[..6];
        var me = await Factory.CreateUserAsync(false, AllPermissions);
        var ana = await UserAsync();
        var luis = await UserAsync();
        var gone = await UserAsync();
        await RenameAsync(me.Id, $"Zeta {token}");
        await RenameAsync(ana.Id, $"Ana {token}");
        await RenameAsync(luis.Id, $"Luis {token}");
        await RenameAsync(gone.Id, $"Baja {token}", isActive: false);
        var client = Factory.CreateClientFor(me);

        var response = await client.GetAsync(Url($"/api/v1/agenda-people?search={token}"), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal([($"Ana {token}", false), ($"Luis {token}", false), ($"Zeta {token}", true)], Read(json).Select(person => (person.Name, person.IsMe)));
        Assert.DoesNotContain(Read(json), person => person.Name.StartsWith("Baja", StringComparison.Ordinal));
        Assert.DoesNotContain("@", json.GetRawText(), StringComparison.Ordinal);
        Assert.All(json.GetProperty("items").EnumerateArray(), item => Assert.Equal(["id", "isMe", "name"], item.EnumerateObject().Select(property => property.Name).Order()));
    }

    [Fact]
    public async Task P31_the_search_ignores_case_and_a_special_character_is_not_a_pattern()
    {
        var token = Guid.NewGuid().ToString("N")[..6];
        var admin = await AdminAsync();
        var maria = await UserAsync();
        await RenameAsync(maria.Id, $"María Pérez {token}");

        var found = Read(await ReadJsonAsync(await admin.GetAsync(Url($"/api/v1/agenda-people?search=MARÍA%20pérez%20{token}"), Ct)));
        var wildcard = Read(await ReadJsonAsync(await admin.GetAsync(Url("/api/v1/agenda-people?search=%25"), Ct)));

        Assert.Equal([maria.Id], found.Select(person => person.Id));
        Assert.All(wildcard, person => Assert.Contains("%", person.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task P31_without_a_search_the_caller_is_always_offered_even_beyond_the_limit()
    {
        var me = await Factory.CreateUserAsync(false, AllPermissions);
        await RenameAsync(me.Id, "ZZZ último por nombre");
        var client = Factory.CreateClientFor(me);

        var json = await ReadJsonAsync(await client.GetAsync(Url("/api/v1/agenda-people?limit=1"), Ct));

        var people = Read(json);
        Assert.Contains(people, person => person.Id == me.Id && person.IsMe);
        Assert.True(people.Count <= 2);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=51")]
    [InlineData("limit=abc")]
    public async Task P31_the_limit_is_validated(string query)
    {
        var admin = await AdminAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Url($"/api/v1/agenda-people?{query}"), Ct)).StatusCode);
        Assert.NotNull(People);
    }

    [Fact]
    public async Task The_list_and_the_detail_say_whether_scheduling_publishes_to_outlook()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var id = (await CreateEventAsync(admin, EventBody(responsible.Id))).GetProperty("id").GetGuid();

        var list = await ReadJsonAsync(await admin.GetAsync(Url($"/api/v1/agenda-events?from={FutureDay(0)}&to={FutureDay(30)}&responsible={responsible.Id}"), Ct));
        var detail = await GetEventAsync(admin, id);

        Assert.False(list.GetProperty("outlookEnabled").GetBoolean());
        Assert.False(detail.GetProperty("outlookEnabled").GetBoolean());
    }
}
