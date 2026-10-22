using System.Net;
using System.Net.Http.Json;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Domain.Agenda;
using Itmas3.Infrastructure.Agenda;
using Itmas3.Infrastructure.Agenda.Graph;
using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Itmas3.IntegrationTests.Api.Agenda;

/// <summary>SPEC-0006 CA-21 (audit of every write) and CA-17, CA-18 (Outlook off by default, start-up validation of the configuration).</summary>
public sealed class AgendaAuditAndConfigurationTests(PostgreSqlFixture database) : AgendaTestBase(database)
{
    private async Task<List<(string Action, string ResourceType, string ResourceId, Guid? Actor, string? Before, string? After)>> AuditOfAsync(IEnumerable<string> resourceIds)
    {
        var wanted = resourceIds.ToArray();
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.AuditEvents.Where(audit => audit.Action.StartsWith("agenda.") && wanted.Contains(audit.ResourceId!)).OrderBy(audit => audit.OccurredAt).ToListAsync(Ct);
        return [.. rows.Select(row => (row.Action, row.ResourceType, row.ResourceId!, row.ActorUserId, row.Before, row.After))];
    }

    [Fact]
    public async Task CA21_every_write_of_an_event_leaves_its_audit_entry_with_the_actor_and_without_secrets()
    {
        var actor = await Factory.CreateUserAsync(false, AllPermissions);
        var admin = Factory.CreateClientFor(actor);
        var responsible = await UserAsync();
        var created = await CreateEventAsync(admin, EventBody(responsible.Id, "Auditada", tags: ["audit"]));
        var id = created.GetProperty("id").GetGuid();
        await admin.SendAsync(Put(EventUrl(id), EventBody(responsible.Id, "Auditada y editada"), 1), Ct);
        await ScheduleAsync(admin, id);
        await admin.PostAsJsonAsync(Url($"/api/v1/agenda-events/{id}/cancel"), new { reason = "Para auditoría" }, Ct);
        await admin.SendAsync(Put(EventUrl(id, "/tags"), new { tags = new[] { "audit", "otra" } }, null), Ct);
        var draft = (await CreateEventAsync(admin, EventBody(responsible.Id, "Se borra"))).GetProperty("id").GetGuid();
        await admin.DeleteAsync(EventUrl(draft), Ct);
        var seeded = await SeedEventAsync(responsible.Id, LocalNow().AddDays(-2));
        await admin.SendAsync(Put(EventUrl(seeded, "/completion"), new { result = "succeeded", notes = "Hecho" }, null), Ct);
        await admin.SendAsync(Put(EventUrl(seeded, "/completion"), new { result = "succeeded", notes = "Hecho y revisado" }, null), Ct);

        var main = await AuditOfAsync([id.ToString()]);
        Assert.Equal(
            ["agenda.event.created", "agenda.event.updated", "agenda.event.scheduled", "agenda.event.cancelled", "agenda.event.tags-updated"],
            main.Select(row => row.Action));
        Assert.All(main, row =>
        {
            Assert.Equal("agenda-event", row.ResourceType);
            Assert.Equal(actor.Id, row.Actor);
        });
        Assert.Contains("Auditada", main[0].After, StringComparison.Ordinal);
        Assert.Contains("Auditada y editada", main[1].After, StringComparison.Ordinal);
        Assert.Contains("Para auditoría", main[3].After, StringComparison.Ordinal);

        Assert.Equal(["agenda.event.created", "agenda.event.deleted"], (await AuditOfAsync([draft.ToString()])).Select(row => row.Action));
        Assert.Equal(["agenda.event.completed", "agenda.event.completion-updated"], (await AuditOfAsync([seeded.ToString()])).Select(row => row.Action));

        var everything = await AuditOfAsync([id.ToString(), draft.ToString(), seeded.ToString()]);
        Assert.All(everything, row =>
        {
            var text = $"{row.Before}{row.After}";
            Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("graph", text, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task CA21_types_and_series_are_audited_with_their_own_resource_types()
    {
        var actor = await Factory.CreateUserAsync(false, AllPermissions);
        var admin = Factory.CreateClientFor(actor);
        var responsible = await UserAsync();

        var type = await ReadJsonAsync(await admin.PostAsJsonAsync(Types, new { name = Unique("Auditado"), defaultDurationMinutes = 60, defaultReminderMinutes = 0, isFreeze = false }, Ct));
        var typeId = type.GetProperty("id").GetGuid();
        await admin.SendAsync(Put(Url($"/api/v1/agenda-event-types/{typeId}"), new { name = Unique("Auditado v2"), defaultDurationMinutes = 30, defaultReminderMinutes = 0, isFreeze = false }, 1), Ct);

        var series = await ReadJsonAsync(await admin.PostAsJsonAsync(
            Series, new { @event = EventBody(responsible.Id), rule = new { unit = "week", interval = 1, count = 2 } }, Ct));
        var seriesId = series.GetProperty("seriesId").GetGuid();
        await admin.SendAsync(Put(Url($"/api/v1/agenda-series/{seriesId}"), new { typeId = PatchingTypeId, title = "Serie auditada", responsibleUserId = responsible.Id, impact = "none" }, null), Ct);

        var typeRows = await AuditOfAsync([typeId.ToString()]);
        Assert.Equal(["agenda.event-type.created", "agenda.event-type.updated"], typeRows.Select(row => row.Action));
        Assert.All(typeRows, row => Assert.Equal("agenda-event-type", row.ResourceType));
        var seriesRows = await AuditOfAsync([seriesId.ToString()]);
        Assert.Equal(["agenda.series.created", "agenda.series.updated"], seriesRows.Select(row => row.Action));
        Assert.All(seriesRows, row => Assert.Equal("agenda-series", row.ResourceType));
        Assert.Contains(series.GetProperty("eventIds")[0].GetGuid().ToString(), seriesRows[0].After, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CA21_a_rejected_operation_leaves_no_audit_entry_and_the_audit_screen_can_filter_by_the_agenda_resource_types()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var created = await CreateEventAsync(admin, EventBody(responsible.Id, "Una sola entrada"));
        var id = created.GetProperty("id").GetGuid();
        await admin.SendAsync(Put(EventUrl(id), EventBody(responsible.Id, ""), 1), Ct); // 422

        Assert.Equal(["agenda.event.created"], (await AuditOfAsync([id.ToString()])).Select(row => row.Action));

        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);
        var response = await root.GetAsync(Url($"/api/v1/audit-events?resourceType=agenda-event&resourceId={id}"), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal(1, json.GetProperty("total").GetInt32());
        Assert.Equal("agenda.event.created", json.GetProperty("items")[0].GetProperty("action").GetString());
    }

    [Fact]
    public async Task The_openapi_document_is_generated_with_every_agenda_endpoint()
    {
        await using var development = Factory.WithWebHostBuilder(builder => builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.EnvironmentKey, "Development"));
        using var client = development.CreateClient();

        var response = await client.GetAsync(Url("/openapi/v1.json"), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = (await ReadJsonAsync(response)).GetProperty("paths").EnumerateObject().Select(path => path.Name).ToHashSet(StringComparer.Ordinal);
        string[] expected =
        [
            "/api/v1/agenda-events", "/api/v1/agenda-events/summary", "/api/v1/agenda-events/{id}", "/api/v1/agenda-events/{id}/tags",
            "/api/v1/agenda-events/schedule", "/api/v1/agenda-events/{id}/cancel", "/api/v1/agenda-events/{id}/completion",
            "/api/v1/agenda-events/{id}/outlook-sync", "/api/v1/agenda-events/outlook-sync", "/api/v1/agenda-series",
            "/api/v1/agenda-series/{id}", "/api/v1/agenda-event-types", "/api/v1/agenda-event-types/{id}", "/api/v1/agenda-tags",
            "/api/v1/agenda-people",
        ];
        Assert.All(expected, path => Assert.Contains(path, paths));
    }

    [Fact]
    public async Task CA17_outlook_is_off_by_default_so_scheduling_only_changes_the_state()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var id = (await CreateEventAsync(admin, EventBody(responsible.Id))).GetProperty("id").GetGuid();

        var outcome = await ScheduleAsync(admin, id);

        Assert.Equal("not-published", outcome.Of(id).GetProperty("outlook").GetProperty("status").GetString());
        var detail = await GetEventAsync(admin, id);
        Assert.Equal("not-published", detail.GetProperty("outlook").GetProperty("status").GetString());
        var one = await ReadJsonAsync(await admin.PostAsync(Url($"/api/v1/agenda-events/{id}/outlook-sync"), null, Ct));
        Assert.Equal("not-published", one.GetProperty("outlook").GetProperty("status").GetString());
        Assert.Equal("not_configured", one.GetProperty("outlook").GetProperty("errorCode").GetString());
        var all = await ReadJsonAsync(await admin.PostAsync(Url("/api/v1/agenda-events/outlook-sync"), null, Ct));
        Assert.Equal(0, all.GetProperty("results").GetArrayLength());
        Assert.Equal("not-published", (await GetEventAsync(admin, id)).GetProperty("outlook").GetProperty("status").GetString());
    }

    [Fact]
    public async Task CA17_syncing_an_unknown_event_is_a_404_even_with_outlook_off()
    {
        // With Outlook off nothing is checked beyond the existence: a draft answers "not published, not configured" (previous test).
        // With Outlook on a draft is refused (AgendaOutlookFlowTests).
        var admin = await AdminAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync(Url($"/api/v1/agenda-events/{Guid.NewGuid()}/outlook-sync"), null, Ct)).StatusCode);
    }

    [Fact]
    public void CA18_the_api_does_not_start_when_outlook_is_enabled_with_an_incomplete_configuration_and_never_prints_a_secret()
    {
        const string secret = "placeholder-not-a-secret-agenda-xyz";
        using var broken = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Agenda:Outlook:Enabled", "true");
            builder.UseSetting("Agenda:Outlook:ClientSecret", secret);
            builder.UseSetting("Agenda:Outlook:Mailbox", "agenda@example.com");
        });

        var failure = Record.Exception(() => broken.CreateClient());

        Assert.NotNull(failure);
        var text = failure.ToString();
        Assert.Contains("Agenda:Outlook:TenantId", text, StringComparison.Ordinal);
        Assert.Contains("Agenda:Outlook:ClientId", text, StringComparison.Ordinal);
        Assert.Contains("Agenda:PublicUrl", text, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        Assert.DoesNotContain("agenda@example.com", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Agenda:TimeZone", "Marte/Olimpo", "Agenda:TimeZone")]
    [InlineData("Agenda:AllowedAttendeeDomains", "no es un dominio", "Agenda:AllowedAttendeeDomains")]
    [InlineData("Agenda:PublicUrl", "ftp://itmas.example.com", "Agenda:PublicUrl")]
    [InlineData("Agenda:PublishPermitLimitPerHour", "-5", "PublishPermitLimitPerHour")]
    public void CA18_an_invalid_setting_stops_the_start_up_and_the_message_names_the_setting_not_its_value(string setting, string value, string named)
    {
        using var broken = Factory.WithWebHostBuilder(builder => builder.UseSetting(setting, value));

        var failure = Record.Exception(() => broken.CreateClient());

        Assert.NotNull(failure);
        Assert.Contains(named, failure.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(value, failure.ToString().Replace(setting, string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void CA18_with_outlook_complete_and_valid_the_api_starts_and_the_team_address_must_belong_to_an_allowed_domain()
    {
        void Complete(IWebHostBuilder builder, string teamAddress)
        {
            builder.UseSetting("Agenda:Outlook:Enabled", "true");
            builder.UseSetting("Agenda:Outlook:TenantId", "33333333-3333-3333-3333-333333333333");
            builder.UseSetting("Agenda:Outlook:ClientId", "44444444-4444-4444-4444-444444444444");
            builder.UseSetting("Agenda:Outlook:ClientSecret", "placeholder-not-a-secret");
            builder.UseSetting("Agenda:Outlook:Mailbox", "agenda@example.com");
            builder.UseSetting("Agenda:Outlook:TeamAddress", teamAddress);
            builder.UseSetting("Agenda:PublicUrl", "https://itmas.example.com");
        }

        using var valid = Factory.WithWebHostBuilder(builder => Complete(builder, "infraestructura@example.com"));
        using var client = valid.CreateClient();
        Assert.NotNull(client);

        using var foreign = Factory.WithWebHostBuilder(builder => Complete(builder, "equipo@otro-dominio.com"));
        var failure = Record.Exception(() => foreign.CreateClient());
        Assert.NotNull(failure);
        Assert.Contains("Agenda:Outlook:TeamAddress", failure.ToString(), StringComparison.Ordinal);
    }

    private static void CompleteOutlookSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("Agenda:Outlook:Enabled", "true");
        builder.UseSetting("Agenda:Outlook:TenantId", "33333333-3333-3333-3333-333333333333");
        builder.UseSetting("Agenda:Outlook:ClientId", "44444444-4444-4444-4444-444444444444");
        builder.UseSetting("Agenda:Outlook:ClientSecret", "placeholder-not-a-secret");
        builder.UseSetting("Agenda:Outlook:Mailbox", "agenda@example.com");
        builder.UseSetting("Agenda:PublicUrl", "https://itmas.example.com");
    }

    [Fact]
    public async Task CA17_the_port_is_the_adapter_of_graph_only_when_outlook_is_enabled_and_nothing_is_called_to_resolve_it()
    {
        await using var enabled = Factory.WithWebHostBuilder(CompleteOutlookSettings);
        using var enabledClient = enabled.CreateClient();
        await using var enabledScope = enabled.Services.CreateAsyncScope();
        await using var disabledScope = Factory.Services.CreateAsyncScope();

        Assert.IsType<GraphOutlookCalendarGateway>(
            enabledScope.ServiceProvider.GetRequiredService<IOutlookCalendarGateway>());
        Assert.IsType<NotConfiguredOutlookCalendarGateway>(
            disabledScope.ServiceProvider.GetRequiredService<IOutlookCalendarGateway>());
    }

    [Fact]
    public async Task CA16_with_outlook_enabled_a_failure_of_the_port_never_fails_the_scheduling_and_leaves_only_a_code_in_the_audit()
    {
        // The adapter of Graph is replaced by a double that fails: no test calls Microsoft (constitution §9.4).
        await using var enabled = Factory.WithWebHostBuilder(builder =>
        {
            CompleteOutlookSettings(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOutlookCalendarGateway>();
                services.AddScoped<IOutlookCalendarGateway, UnavailableOutlookGateway>();
            });
        });
        var user = await Factory.CreateUserAsync(false, AllPermissions);
        var responsible = await UserAsync();
        var client = enabled.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, user.Id.ToString());
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.StampHeader, user.SecurityStamp);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.NameHeader, user.DisplayName);
        client.DefaultRequestHeaders.Add("X-CSRF", "1");
        var id = (await CreateEventAsync(client, EventBody(responsible.Id))).GetProperty("id").GetGuid();

        var outcome = await ScheduleAsync(client, id);

        Assert.Equal("scheduled", outcome.Of(id).GetProperty("status").GetString());
        Assert.Equal("error", outcome.Of(id).GetProperty("outlook").GetProperty("status").GetString());
        Assert.Equal("graph_unavailable", outcome.Of(id).GetProperty("outlook").GetProperty("errorCode").GetString());
        var detail = await GetEventAsync(client, id);
        Assert.Equal("scheduled", detail.GetProperty("status").GetString());
        Assert.Equal("error", detail.GetProperty("outlook").GetProperty("status").GetString());
        Assert.True(detail.GetProperty("outlookEnabled").GetBoolean());
        var audit = await AuditAsync("agenda.event.outlook-failed");
        Assert.Contains(audit, row => row.ResourceId == id.ToString() && row.After!.Contains("graph_unavailable", StringComparison.Ordinal));
        Assert.DoesNotContain(audit, row => row.After!.Contains("placeholder-not-a-secret", StringComparison.Ordinal));
    }

    private sealed class UnavailableOutlookGateway : IOutlookCalendarGateway
    {
        public Task<OutlookGatewayResult> PublishAsync(
            OutlookEventData data, string? outlookEventId, CancellationToken cancellationToken) =>
            Task.FromResult(OutlookGatewayResult.Failure(OutlookErrorCodes.Unavailable));

        public Task<OutlookGatewayResult> CancelAsync(string outlookEventId, string reason, CancellationToken cancellationToken) =>
            Task.FromResult(OutlookGatewayResult.Failure(OutlookErrorCodes.Unavailable));
    }
}
