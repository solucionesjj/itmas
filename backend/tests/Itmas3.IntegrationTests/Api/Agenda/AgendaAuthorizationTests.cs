using System.Net;
using System.Net.Http.Json;
using Itmas3.Application.Agenda;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Itmas3.IntegrationTests.Api.Agenda;

/// <summary>
/// SPEC-0006 CA-20: every endpoint answers 401 without a session and 403 without its permission. Every action also needs
/// <c>agenda.read</c> (§7), and a user with read but not the action cannot perform it. The authorization conventions of the
/// whole API are checked by the architecture tests; these tests exercise the real pipeline.
/// </summary>
public sealed class AgendaAuthorizationTests(PostgreSqlFixture database) : AgendaTestBase(database)
{
    private sealed record Endpoint(string Name, HttpMethod Method, string Path, string? Body, string? ActionPermission);

    private static readonly Endpoint[] Endpoints =
    [
        new("list", HttpMethod.Get, "/api/v1/agenda-events?from=2090-01-01&to=2090-02-01", null, null),
        new("export", HttpMethod.Get, "/api/v1/agenda-events/export.ics?from=2090-01-01&to=2090-02-01", null, null),
        new("summary", HttpMethod.Get, "/api/v1/agenda-events/summary?year=2090", null, null),
        new("detail", HttpMethod.Get, "/api/v1/agenda-events/{id}", null, null),
        new("tags", HttpMethod.Get, "/api/v1/agenda-tags", null, null),
        new("types", HttpMethod.Get, "/api/v1/agenda-event-types", null, null),
        new("people", HttpMethod.Get, "/api/v1/agenda-people", null, null),
        new("create-event", HttpMethod.Post, "/api/v1/agenda-events", "{}", AgendaPermissions.Create),
        new("update-event", HttpMethod.Put, "/api/v1/agenda-events/{id}", "{}", AgendaPermissions.Update),
        new("delete-event", HttpMethod.Delete, "/api/v1/agenda-events/{id}", null, AgendaPermissions.Update),
        new("set-tags", HttpMethod.Put, "/api/v1/agenda-events/{id}/tags", """{"tags":[]}""", AgendaPermissions.Update),
        new("schedule", HttpMethod.Post, "/api/v1/agenda-events/schedule", """{"ids":["00000000-0000-0000-0000-000000000001"]}""", AgendaPermissions.Publish),
        new("cancel", HttpMethod.Post, "/api/v1/agenda-events/{id}/cancel", """{"reason":"x"}""", AgendaPermissions.Publish),
        new("sync-one", HttpMethod.Post, "/api/v1/agenda-events/{id}/outlook-sync", null, AgendaPermissions.Publish),
        new("sync-all", HttpMethod.Post, "/api/v1/agenda-events/outlook-sync", null, AgendaPermissions.Publish),
        new("complete", HttpMethod.Put, "/api/v1/agenda-events/{id}/completion", """{"result":"succeeded"}""", AgendaPermissions.Complete),
        new("create-series", HttpMethod.Post, "/api/v1/agenda-series", "{}", AgendaPermissions.Create),
        new("update-series", HttpMethod.Put, "/api/v1/agenda-series/{id}", "{}", AgendaPermissions.Update),
        new("create-type", HttpMethod.Post, "/api/v1/agenda-event-types", "{}", AgendaPermissions.TypesManage),
        new("update-type", HttpMethod.Put, "/api/v1/agenda-event-types/{id}", "{}", AgendaPermissions.TypesManage),
    ];

    public static TheoryData<string> EndpointNames => [.. Endpoints.Select(endpoint => endpoint.Name)];

    private static HttpRequestMessage Request(Endpoint endpoint)
    {
        var request = new HttpRequestMessage(endpoint.Method, new Uri(endpoint.Path.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.Ordinal), UriKind.Relative));
        if (endpoint.Body is not null)
        {
            request.Content = Json(endpoint.Body);
        }

        return request;
    }

    [Theory]
    [MemberData(nameof(EndpointNames))]
    public async Task CA20_without_a_session_every_endpoint_answers_401(string name)
    {
        var endpoint = Endpoints.Single(item => item.Name == name);
        using var client = Factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-CSRF", "1");

        var response = await client.SendAsync(Request(endpoint), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(EndpointNames))]
    public async Task CA20_without_any_agenda_permission_every_endpoint_answers_403(string name)
    {
        var endpoint = Endpoints.Single(item => item.Name == name);
        var client = await Factory.CreateAuthenticatedClientAsync();

        var response = await client.SendAsync(Request(endpoint), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(EndpointNames))]
    public async Task CA20_read_alone_opens_the_queries_and_nothing_else(string name)
    {
        var endpoint = Endpoints.Single(item => item.Name == name);
        var client = await Factory.CreateAuthenticatedClientAsync(false, AgendaPermissions.Read);

        var response = await client.SendAsync(Request(endpoint), Ct);

        if (endpoint.ActionPermission is null)
        {
            Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Theory]
    [MemberData(nameof(EndpointNames))]
    public async Task CA20_the_action_permission_alone_is_not_enough_because_every_action_also_needs_read(string name)
    {
        var endpoint = Endpoints.Single(item => item.Name == name);
        if (endpoint.ActionPermission is null)
        {
            return;
        }

        var client = await Factory.CreateAuthenticatedClientAsync(false, endpoint.ActionPermission);

        var response = await client.SendAsync(Request(endpoint), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(EndpointNames))]
    public async Task CA20_with_read_and_the_permission_of_the_action_the_endpoint_is_reached(string name)
    {
        var endpoint = Endpoints.Single(item => item.Name == name);
        var permissions = endpoint.ActionPermission is null ? [AgendaPermissions.Read] : new[] { AgendaPermissions.Read, endpoint.ActionPermission };
        var client = await Factory.CreateAuthenticatedClientAsync(false, permissions);

        var response = await client.SendAsync(Request(endpoint), Ct);

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CA20_each_permission_opens_only_its_own_action()
    {
        var reader = await Factory.CreateAuthenticatedClientAsync(false, AgendaPermissions.Read, AgendaPermissions.Create);
        var responsible = await UserAsync();

        var created = await reader.PostAsJsonAsync(Events, EventBody(responsible.Id), Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        // Create does not allow to schedule, edit, delete or close.
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync(Url("/api/v1/agenda-events/schedule"), new { ids = new[] { id } }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.DeleteAsync(EventUrl(id), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.SendAsync(Put(EventUrl(id), EventBody(responsible.Id), 1), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync(Url($"/api/v1/agenda-events/{id}/cancel"), new { reason = "x" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync(Types, new { name = "X", defaultDurationMinutes = 60, defaultReminderMinutes = 0, isFreeze = false }, Ct)).StatusCode);

        var publisher = await Factory.CreateAuthenticatedClientAsync(false, AgendaPermissions.Read, AgendaPermissions.Publish);
        Assert.Equal(HttpStatusCode.OK, (await publisher.PostAsJsonAsync(Url("/api/v1/agenda-events/schedule"), new { ids = new[] { id } }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await publisher.PostAsJsonAsync(Events, EventBody(responsible.Id), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await publisher.SendAsync(Put(Url($"/api/v1/agenda-events/{id}/completion"), new { result = "succeeded" }, null), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await publisher.PostAsJsonAsync(Url($"/api/v1/agenda-events/{id}/cancel"), new { reason = "Ya no aplica" }, Ct)).StatusCode);
    }

    [Fact]
    public async Task P26_scheduling_cancelling_and_syncing_share_a_limit_per_hour_for_the_whole_api()
    {
        // The same host and database with a limit of three requests per hour.
        await using var limited = Factory.WithWebHostBuilder(builder => builder.UseSetting("Agenda:PublishPermitLimitPerHour", "3"));
        var client = ClientFor(limited, await Factory.CreateUserAsync(false, AllPermissions));
        var schedule = Url("/api/v1/agenda-events/schedule");
        var body = new { ids = new[] { Guid.NewGuid() } };

        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(schedule, body, Ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync(schedule, body, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync(Url("/api/v1/agenda-events/outlook-sync"), null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync(Url($"/api/v1/agenda-events/{Guid.NewGuid()}/cancel"), new { reason = "x" }, Ct)).StatusCode);

        // The limit counts everyone: another user hits the same counter. Other endpoints are not affected.
        var other = ClientFor(limited, await Factory.CreateUserAsync(false, AllPermissions));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await other.PostAsJsonAsync(schedule, body, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync(Types, Ct)).StatusCode);
    }

    [Fact]
    public async Task F1_requests_refused_by_the_session_the_csrf_check_or_the_permissions_do_not_spend_the_budget()
    {
        await using var limited = Factory.WithWebHostBuilder(builder => builder.UseSetting("Agenda:PublishPermitLimitPerHour", "2"));
        var schedule = Url("/api/v1/agenda-events/schedule");
        var body = new { ids = new[] { Guid.NewGuid() } };
        using var anonymous = limited.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        anonymous.DefaultRequestHeaders.Add("X-CSRF", "1");
        var reader = ClientFor(limited, await Factory.CreateUserAsync(false, AgendaPermissions.Read));
        var publisher = ClientFor(limited, await Factory.CreateUserAsync(false, AgendaPermissions.Read, AgendaPermissions.Publish));
        var withoutCsrf = ClientFor(limited, await Factory.CreateUserAsync(false, AgendaPermissions.Read, AgendaPermissions.Publish));
        withoutCsrf.DefaultRequestHeaders.Remove("X-CSRF");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(schedule, body, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync(schedule, body, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await withoutCsrf.PostAsJsonAsync(schedule, body, Ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await publisher.PostAsJsonAsync(schedule, body, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await publisher.PostAsJsonAsync(schedule, body, Ct)).StatusCode);
        var spent = await publisher.PostAsJsonAsync(schedule, body, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, spent.StatusCode);
        Assert.True(spent.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task F2_changing_a_scheduled_event_needs_agenda_publish_as_well_and_a_draft_does_not()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var draft = (await CreateEventAsync(admin, EventBody(responsible.Id))).GetProperty("id").GetGuid();
        var scheduled = (await CreateEventAsync(admin, EventBody(responsible.Id, start: Future(11)))).GetProperty("id").GetGuid();
        await ScheduleAsync(admin, scheduled);
        var revision = (await GetEventAsync(admin, scheduled)).GetProperty("revision").GetInt32();
        var editor = await Factory.CreateAuthenticatedClientAsync(false, AgendaPermissions.Read, AgendaPermissions.Update);

        var refused = await editor.SendAsync(Put(EventUrl(scheduled), EventBody(responsible.Id, title: "Editado", start: Future(11)), revision), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("agenda.publish", await ProblemDetailAsync(refused), StringComparison.Ordinal);
        Assert.Equal("Parches de SO", (await GetEventAsync(admin, scheduled)).GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.OK, (await editor.SendAsync(Put(EventUrl(draft), EventBody(responsible.Id, title: "Editado"), 1), Ct)).StatusCode);

        var publisher = await Factory.CreateAuthenticatedClientAsync(false, AgendaPermissions.Read, AgendaPermissions.Update, AgendaPermissions.Publish);
        var accepted = await publisher.SendAsync(Put(EventUrl(scheduled), EventBody(responsible.Id, title: "Editado", start: Future(11)), revision), Ct);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("Editado", (await GetEventAsync(admin, scheduled)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task F2_changing_a_scheduled_event_spends_the_budget_of_scheduling_and_cancelling()
    {
        await using var limited = Factory.WithWebHostBuilder(builder => builder.UseSetting("Agenda:PublishPermitLimitPerHour", "1"));
        var client = ClientFor(limited, await Factory.CreateUserAsync(false, AllPermissions));
        var responsible = await UserAsync();
        var created = await client.PostAsJsonAsync(Events, EventBody(responsible.Id), Ct);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Url("/api/v1/agenda-events/schedule"), new { ids = new[] { id } }, Ct)).StatusCode);
        var revision = (await GetEventAsync(client, id)).GetProperty("revision").GetInt32();

        var spent = await client.SendAsync(Put(EventUrl(id), EventBody(responsible.Id, title: "Editado"), revision), Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, spent.StatusCode);
        Assert.True(spent.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal("Parches de SO", (await GetEventAsync(client, id)).GetProperty("title").GetString());
    }

    private static HttpClient ClientFor(WebApplicationFactory<Program> factory, TestUser user)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, user.Id.ToString());
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.StampHeader, user.SecurityStamp);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.NameHeader, user.DisplayName);
        client.DefaultRequestHeaders.Add("X-CSRF", "1");
        return client;
    }
}
