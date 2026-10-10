using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Itmas3.Application.Inventory;
using Itmas3.Domain.Authorization;
using Itmas3.Domain.Inventory;
using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Itmas3.IntegrationTests.Infrastructure.AgentSimulator;

namespace Itmas3.IntegrationTests.Api;

/// <summary>SPEC-0003 acceptance criteria: device registry, enrollment with per-device codes and inventory queries.</summary>
public sealed class DeviceRegistryTests(PostgreSqlFixture database) : ApiTestBase(database)
{
    private static readonly Uri Devices = new("/api/v1/devices", UriKind.Relative);

    private static readonly string[] AllPermissions =
    [
        InventoryPermissions.DevicesRead, InventoryPermissions.DevicesCreate, InventoryPermissions.DevicesUpdate,
        InventoryPermissions.DevicesDelete, InventoryPermissions.DevicesReportsRead,
    ];

    [Fact]
    public async Task CA01_CA02_a_new_device_is_pending_and_its_code_is_returned_only_once()
    {
        var admin = await AdminAsync();
        var name = Unique("Portátil");

        var response = await admin.PostAsJsonAsync(Devices, new { name, type = "workstation" }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.NotNull(response.Headers.ETag);
        var created = await ReadJsonAsync(response);
        var id = created.GetProperty("id").GetGuid();
        var code = created.GetProperty("enrollmentCode").GetString()!;
        Assert.Matches("^[0-9A-Z]{5}(-[0-9A-Z]{5}){3}$", code);
        Assert.Equal("pending", created.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("owner").ValueKind);

        var detail = await admin.GetStringAsync(new Uri($"/api/v1/devices/{id}", UriKind.Relative), Ct);
        Assert.DoesNotContain(code, detail, StringComparison.Ordinal);
        Assert.Contains("\"hasPendingEnrollmentCode\":true", detail, StringComparison.Ordinal);

        var row = await ListOneAsync(admin, name);
        Assert.Equal("workstation", row.GetProperty("type").GetString());
        Assert.Equal("pending", row.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("owner").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("lastCollectedAt").ValueKind);
        Assert.Equal(0, row.GetProperty("reportCount").GetInt32());

        await using var scope = Factory.Services.CreateAsyncScope();
        var device = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Devices.SingleAsync(item => item.Id == id, Ct);
        Assert.Equal(DeviceSecrets.HashEnrollmentCode(code), device.EnrollmentCodeHash);
    }

    [Theory]
    [InlineData("{\"name\":\"x\"}")]
    [InlineData("{\"name\":\"x\",\"type\":\"laptop\"}")]
    [InlineData("{\"name\":\"x\",\"type\":\"server\",\"serial\":\"1\"}")]
    public async Task CA01_type_is_required_and_closed(string body)
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsync(Devices, Json(body), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CA03_the_code_enrolls_the_agent_and_its_reports_appear_in_the_history()
    {
        var admin = await AdminAsync();
        var (id, code) = await CreateAsync(admin, Unique("srv"), "server");

        var apiKey = await EnrollAsync(code, NewHardwareId());
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(Factory.CreateAnonymousClient(), apiKey, ContractFixture())).StatusCode);

        var detail = await ReadJsonAsync(await admin.GetAsync(new Uri($"/api/v1/devices/{id}", UriKind.Relative), Ct));
        Assert.Equal("active", detail.GetProperty("status").GetString());
        Assert.False(detail.GetProperty("hasPendingEnrollmentCode").GetBoolean());
        Assert.Equal(1, detail.GetProperty("reportCount").GetInt32());

        var history = await ReadJsonAsync(await admin.GetAsync(ReportsOf(id), Ct));
        Assert.Equal(1, history.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task CA04_consumed_regenerated_and_deleted_codes_do_not_enroll()
    {
        var admin = await AdminAsync();
        var (consumedId, consumed) = await CreateAsync(admin, Unique("pc"));
        await EnrollAsync(consumed, NewHardwareId());
        var (replacedId, replaced) = await CreateAsync(admin, Unique("pc"));
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsync(new Uri($"/api/v1/devices/{replacedId}/enrollment-code", UriKind.Relative), null, Ct)).StatusCode);
        var (deletedId, deleted) = await CreateAsync(admin, Unique("pc"));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(DeviceUri(deletedId), Ct)).StatusCode);

        foreach (var code in new[] { consumed, replaced, deleted })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await EnrollRawAsync(Factory.CreateAnonymousClient(), code, NewHardwareId())).StatusCode);
        }

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(DeviceStatus.Active, (await db.Devices.SingleAsync(item => item.Id == consumedId, Ct)).Status);
        Assert.Equal(DeviceStatus.Pending, (await db.Devices.SingleAsync(item => item.Id == replacedId, Ct)).Status);
        Assert.Equal(DeviceStatus.Deleted, (await db.Devices.SingleAsync(item => item.Id == deletedId, Ct)).Status);
    }

    [Fact]
    public async Task CA05_after_regenerating_the_code_and_reinstalling_only_the_new_key_works()
    {
        var admin = await AdminAsync();
        var (id, code) = await CreateAsync(admin, Unique("pc"));
        var hardwareId = NewHardwareId();
        var oldKey = await EnrollAsync(code, hardwareId);

        var regenerated = await ReadJsonAsync(await admin.PostAsync(new Uri($"/api/v1/devices/{id}/enrollment-code", UriKind.Relative), null, Ct));
        // Until the agent is reinstalled, the current key keeps working.
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(Factory.CreateAnonymousClient(), oldKey, ContractFixture())).StatusCode);
        var newKey = await EnrollAsync(regenerated.GetProperty("enrollmentCode").GetString()!, hardwareId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SubmitAsync(Factory.CreateAnonymousClient(), oldKey, ContractFixture())).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(Factory.CreateAnonymousClient(), newKey, ContractFixture())).StatusCode);
        Assert.Contains("device.relinked", await AuditActionsAsync(id));
    }

    [Fact]
    public async Task CA06_hardware_of_another_device_is_rejected_with_409_and_audited()
    {
        var admin = await AdminAsync();
        var (_, first) = await CreateAsync(admin, Unique("pc"));
        var (secondId, second) = await CreateAsync(admin, Unique("pc"));
        var hardwareId = NewHardwareId();
        await EnrollAsync(first, hardwareId);

        var response = await EnrollRawAsync(Factory.CreateAnonymousClient(), second, hardwareId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("device.enrollment.rejected", await AuditActionsAsync(secondId));
        // The rejected code is not consumed: it can still enroll a different machine.
        Assert.Equal(HttpStatusCode.Created, (await EnrollRawAsync(Factory.CreateAnonymousClient(), second, NewHardwareId())).StatusCode);
    }

    [Fact]
    public async Task CA07_a_deleted_device_stops_receiving_data_and_its_history_stays_available()
    {
        var admin = await AdminAsync();
        var name = Unique("pc");
        var (id, code) = await CreateAsync(admin, name);
        var hardwareId = NewHardwareId();
        var apiKey = await EnrollAsync(code, hardwareId);
        await SubmitAsync(Factory.CreateAnonymousClient(), apiKey, ContractFixture());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(DeviceUri(id), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(DeviceUri(id), Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SubmitAsync(Factory.CreateAnonymousClient(), apiKey, ContractFixture())).StatusCode);
        Assert.Equal(0, (await SearchAsync(admin, $"search={Uri.EscapeDataString(name)}")).GetProperty("total").GetInt32());
        var deleted = await ListOneAsync(admin, name, status: "deleted");
        Assert.Equal("deleted", deleted.GetProperty("status").GetString());
        Assert.Equal(1, deleted.GetProperty("reportCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(new Uri($"/api/v1/devices/{id}/reports/latest", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsync(new Uri($"/api/v1/devices/{id}/enrollment-code", UriKind.Relative), null, Ct)).StatusCode);
        Assert.Equal(1, (await AuditActionsAsync(id)).Count(action => action == "device.deleted"));

        // RN-1 / RN-4: the name and the hardware are free again for a new registration.
        var (_, again) = await CreateAsync(admin, name);
        Assert.Equal(HttpStatusCode.Created, (await EnrollRawAsync(Factory.CreateAnonymousClient(), again, hardwareId)).StatusCode);
    }

    [Fact]
    public async Task CA08_name_type_and_email_are_updated_with_optimistic_concurrency()
    {
        var admin = await AdminAsync();
        var (id, _) = await CreateAsync(admin, Unique("pc"));
        var taken = Unique("taken");
        await CreateAsync(admin, taken);
        var etag = (await admin.GetAsync(DeviceUri(id), Ct)).Headers.ETag!.Tag;
        var renamed = Unique("srv");

        var updated = await PutAsync(admin, id, etag, new { name = renamed, type = "server", ownerEmail = " Ops.Team@Example.com " });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await ReadJsonAsync(updated);
        Assert.Equal(renamed, body.GetProperty("name").GetString());
        Assert.Equal("server", body.GetProperty("type").GetString());
        Assert.Equal("ops.team@example.com", body.GetProperty("owner").GetProperty("email").GetString());
        var newEtag = updated.Headers.ETag!.Tag;
        Assert.NotEqual(etag, newEtag);

        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(admin, id, etag, new { name = renamed, type = "server" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PutAsync(admin, id, newEtag, new { name = taken.ToUpperInvariant(), type = "server" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PutAsync(admin, id, newEtag, new { name = renamed, type = "server", ownerEmail = "not-an-email" })).StatusCode);
        Assert.Equal((HttpStatusCode)428, (await PutAsync(admin, id, null, new { name = renamed, type = "server" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(admin, Guid.NewGuid(), newEtag, new { name = renamed, type = "server" })).StatusCode);

        var cleared = await PutAsync(admin, id, newEtag, new { name = renamed, type = "server", ownerEmail = (string?)null });
        Assert.Equal(JsonValueKind.Null, (await ReadJsonAsync(cleared)).GetProperty("owner").ValueKind);
    }

    [Fact]
    public async Task CA09_CA10_the_list_summarises_each_device_and_can_be_searched_filtered_sorted_and_paged()
    {
        var admin = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..10];
        var (withReports, code) = await CreateAsync(admin, $"b-{tag}", "server", $"owner-{tag}@example.com");
        await CreateAsync(admin, $"a-{tag}");
        await CreateAsync(admin, $"c-{tag}");
        var apiKey = await EnrollAsync(code, NewHardwareId(), hostname: $"host-{tag}");
        var older = ContractFixture();
        older["collectedAt"] = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        older["system"]!["hostname"] = $"host-{tag}";
        var latest = ContractFixture();
        latest["system"]!["hostname"] = $"host-{tag}";
        await SubmitAsync(Factory.CreateAnonymousClient(), apiKey, older);
        await SubmitAsync(Factory.CreateAnonymousClient(), apiKey, latest);

        var all = await SearchAsync(admin, $"search={tag}");
        Assert.Equal(3, all.GetProperty("total").GetInt32());
        Assert.Equal([$"a-{tag}", $"b-{tag}", $"c-{tag}"], Names(all));
        var summary = all.GetProperty("items")[1];
        Assert.Equal(2, summary.GetProperty("reportCount").GetInt32());
        Assert.Equal($"host-{tag}", summary.GetProperty("hostname").GetString());
        Assert.Equal($"owner-{tag}@example.com", summary.GetProperty("owner").GetProperty("email").GetString());
        var expected = DateTimeOffset.Parse(latest["collectedAt"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True((summary.GetProperty("lastCollectedAt").GetDateTimeOffset() - expected).Duration() < TimeSpan.FromMilliseconds(1));

        Assert.Equal([$"b-{tag}"], Names(await SearchAsync(admin, $"search=host-{tag}")));
        Assert.Equal([$"b-{tag}"], Names(await SearchAsync(admin, $"search=owner-{tag}")));
        Assert.Equal([$"b-{tag}"], Names(await SearchAsync(admin, $"search={tag}&type=server")));
        Assert.Equal([$"a-{tag}", $"c-{tag}"], Names(await SearchAsync(admin, $"search={tag}&status=pending")));
        Assert.Equal([$"c-{tag}", $"b-{tag}", $"a-{tag}"], Names(await SearchAsync(admin, $"search={tag}&sort=-name")));
        Assert.Equal($"b-{tag}", Names(await SearchAsync(admin, $"search={tag}&sort=-lastCollectedAt"))[0]);
        var page = await SearchAsync(admin, $"search={tag}&page=2&pageSize=2");
        Assert.Equal([$"c-{tag}"], Names(page));
        Assert.Equal(3, page.GetProperty("total").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(new Uri("/api/v1/devices?status=unknown", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(new Uri("/api/v1/devices?pageSize=101", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task CA10b_the_owner_name_comes_from_the_user_with_the_same_email()
    {
        var admin = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..10];
        var email = $"ana.{tag}@example.com";
        var (id, _) = await CreateAsync(admin, $"pc-{tag}", ownerEmail: email.ToUpperInvariant());
        var (unmatched, _) = await CreateAsync(admin, $"pc2-{tag}", ownerEmail: $"nobody.{tag}@example.com");

        Assert.Equal(JsonValueKind.Null, (await OwnerOfAsync(admin, unmatched)).GetProperty("name").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await OwnerOfAsync(admin, id)).GetProperty("name").ValueKind);

        // Users sign in for the first time after the device was registered, one with a differently cased e-mail:
        // with several matches, the most recent sign-in wins.
        await AddUserAsync("Ana Antigua", $"Ana.{tag}@Example.com", DateTimeOffset.UtcNow.AddDays(-10));
        var recent = await AddUserAsync("Ana Pérez", email, DateTimeOffset.UtcNow);

        Assert.Equal("Ana Pérez", (await OwnerOfAsync(admin, id)).GetProperty("name").GetString());
        Assert.Equal([$"pc-{tag}"], Names(await SearchAsync(admin, $"search={Uri.EscapeDataString("Ana Pérez")}&status=all")));

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(item => item.Id == recent, Ct);
            user.RecordSignIn("Ana Pérez Gómez", email, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(Ct);
        }

        Assert.Equal("Ana Pérez Gómez", (await OwnerOfAsync(admin, id)).GetProperty("name").GetString());
        // RF-14: a device without records has no latest report.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(new Uri($"/api/v1/devices/{id}/reports/latest", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task CA11_CA12_history_and_reports_show_every_section_of_a_record()
    {
        var admin = await AdminAsync();
        var tag = Unique("pc");
        var (id, code) = await CreateAsync(admin, tag, ownerEmail: "owner@example.com");
        var apiKey = await EnrollAsync(code, NewHardwareId());
        var older = ContractFixture();
        older["collectedAt"] = DateTimeOffset.UtcNow.AddDays(-2).ToString("O");
        var latest = ContractFixture();
        latest["network"]!["publicNetwork"] = null;
        latest["unavailable"] = new JsonArray(new JsonObject { ["section"] = "network.publicNetwork", ["reason"] = "timeout" });
        await SubmitAsync(Factory.CreateAnonymousClient(), apiKey, latest);
        await SubmitAsync(Factory.CreateAnonymousClient(), apiKey, older);

        var history = await ReadJsonAsync(await admin.GetAsync(ReportsOf(id), Ct));
        var items = history.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.True(items[0].GetProperty("collectedAt").GetDateTimeOffset() > items[1].GetProperty("collectedAt").GetDateTimeOffset());
        Assert.True(items[0].GetProperty("isLatest").GetBoolean());
        Assert.False(items[1].GetProperty("isLatest").GetBoolean());
        Assert.Equal(1, items[0].GetProperty("unavailableCount").GetInt32());
        Assert.Equal(0, items[1].GetProperty("unavailableCount").GetInt32());
        Assert.Equal("203.0.113.25", items[1].GetProperty("publicIp").GetString());
        Assert.True(items[1].TryGetProperty("connectionIp", out _));
        Assert.False(string.IsNullOrEmpty(items[1].GetProperty("agentVersion").GetString()));

        var report = await ReadJsonAsync(await admin.GetAsync(new Uri($"/api/v1/devices/{id}/reports/latest", UriKind.Relative), Ct));
        Assert.Equal(items[0].GetProperty("id").GetGuid(), report.GetProperty("id").GetGuid());
        Assert.True(report.GetProperty("isLatest").GetBoolean());
        Assert.Equal(tag, report.GetProperty("device").GetProperty("name").GetString());
        Assert.Equal("owner@example.com", report.GetProperty("device").GetProperty("owner").GetProperty("email").GetString());
        Assert.True(report.TryGetProperty("connectionIp", out _));
        var inventory = report.GetProperty("inventory");
        foreach (var section in new[] { "system", "hardware", "network", "software", "accounts", "security", "unavailable" })
        {
            Assert.True(inventory.TryGetProperty(section, out _), $"Missing section {section}");
        }

        Assert.Equal("timeout", inventory.GetProperty("unavailable")[0].GetProperty("reason").GetString());

        var olderReport = await ReadJsonAsync(await admin.GetAsync(new Uri($"/api/v1/devices/{id}/reports/{items[1].GetProperty("id").GetGuid()}", UriKind.Relative), Ct));
        Assert.False(olderReport.GetProperty("isLatest").GetBoolean());
        Assert.Equal("203.0.113.25", olderReport.GetProperty("inventory").GetProperty("network").GetProperty("publicNetwork").GetProperty("ip").GetString());

        // A report of another device is not reachable through this one.
        var (otherId, _) = await CreateAsync(admin, Unique("pc"));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(new Uri($"/api/v1/devices/{otherId}/reports/{items[0].GetProperty("id").GetGuid()}", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(ReportsOf(Guid.NewGuid()), Ct)).StatusCode);
    }

    [Fact]
    public async Task CA14_every_endpoint_requires_a_session_and_its_permission()
    {
        var (id, _) = await Factory.RegisterDeviceAsync();
        var endpoints = new (HttpMethod Method, string Path, string Permission, object? Body)[]
        {
            (HttpMethod.Get, "/api/v1/devices", InventoryPermissions.DevicesRead, null),
            (HttpMethod.Get, $"/api/v1/devices/{id}", InventoryPermissions.DevicesRead, null),
            (HttpMethod.Post, "/api/v1/devices", InventoryPermissions.DevicesCreate, new { name = Unique("pc"), type = "server" }),
            (HttpMethod.Put, $"/api/v1/devices/{id}", InventoryPermissions.DevicesUpdate, new { name = Unique("pc"), type = "server" }),
            (HttpMethod.Post, $"/api/v1/devices/{id}/enrollment-code", InventoryPermissions.DevicesUpdate, null),
            (HttpMethod.Delete, $"/api/v1/devices/{Guid.NewGuid()}", InventoryPermissions.DevicesDelete, null),
            (HttpMethod.Get, $"/api/v1/devices/{id}/reports", InventoryPermissions.DevicesReportsRead, null),
            (HttpMethod.Get, $"/api/v1/devices/{id}/reports/latest", InventoryPermissions.DevicesReportsRead, null),
            (HttpMethod.Get, $"/api/v1/devices/{id}/reports/{Guid.NewGuid()}", InventoryPermissions.DevicesReportsRead, null),
        };

        var anonymous = Factory.CreateAnonymousClient();
        anonymous.DefaultRequestHeaders.Add("X-CSRF", "1");

        foreach (var (method, path, permission, body) in endpoints)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(anonymous, method, path, body)).StatusCode);

            var withoutIt = await Factory.CreateAuthenticatedClientAsync(false, AllPermissions.Where(item => item != permission).ToArray());
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(withoutIt, method, path, body)).StatusCode);

            var withOnlyIt = await Factory.CreateAuthenticatedClientAsync(false, permission);
            Assert.NotEqual(HttpStatusCode.Forbidden, (await SendAsync(withOnlyIt, method, path, body)).StatusCode);
        }
    }

    [Fact]
    public async Task CA15_every_operation_is_audited_without_secrets()
    {
        var admin = await AdminAsync();
        var created = await ReadJsonAsync(await admin.PostAsJsonAsync(Devices, new { name = Unique("pc"), type = "workstation", ownerEmail = "a@example.com" }, Ct));
        var id = created.GetProperty("id").GetGuid();
        var firstCode = created.GetProperty("enrollmentCode").GetString()!;
        var etag = (await admin.GetAsync(DeviceUri(id), Ct)).Headers.ETag!.Tag;
        await PutAsync(admin, id, etag, new { name = Unique("pc"), type = "server", ownerEmail = "b@example.com" });
        var regenerated = await ReadJsonAsync(await admin.PostAsync(new Uri($"/api/v1/devices/{id}/enrollment-code", UriKind.Relative), null, Ct));
        var secondCode = regenerated.GetProperty("enrollmentCode").GetString()!;
        var apiKey = await EnrollAsync(secondCode, NewHardwareId());
        await admin.DeleteAsync(DeviceUri(id), Ct);

        await using var scope = Factory.Services.CreateAsyncScope();
        var events = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditEvents
            .Where(item => item.ResourceType == "device" && item.ResourceId == id.ToString())
            .OrderBy(item => item.OccurredAt).ThenBy(item => item.Id)
            .ToListAsync(Ct);

        Assert.Equal(
            ["device.created", "device.updated", "device.enrollment-code.regenerated", "device.enrolled", "device.deleted"],
            events.Select(item => item.Action));
        var created1 = events[0];
        Assert.NotNull(created1.ActorUserId);
        Assert.Null(events[3].ActorUserId);
        var update = JsonNode.Parse(events[1].After!)!;
        Assert.Equal("server", update["type"]!.GetValue<string>());
        Assert.Equal("a@example.com", JsonNode.Parse(events[1].Before!)!["ownerEmail"]!.GetValue<string>());

        var secrets = new[] { firstCode, secondCode, apiKey, DeviceSecrets.HashEnrollmentCode(firstCode)!, DeviceSecrets.HashEnrollmentCode(secondCode)!, DeviceSecrets.Hash(apiKey) };
        foreach (var audit in events)
        {
            var text = $"{audit.Before} {audit.After}";
            Assert.DoesNotContain(secrets, secret => text.Contains(secret, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Device_api_keys_cannot_use_the_registry()
    {
        var (_, code) = await Factory.RegisterDeviceAsync();
        var apiKey = await EnrollAsync(code, NewHardwareId());
        var device = Factory.CreateAnonymousClient();
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Device", apiKey);

        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync(Devices, Ct)).StatusCode);
    }

    private async Task<HttpClient> AdminAsync() => await Factory.CreateAuthenticatedClientAsync(false, AllPermissions);

    private async Task<(Guid Id, string Code)> CreateAsync(HttpClient admin, string name, string type = "workstation", string? ownerEmail = null)
    {
        var response = await admin.PostAsJsonAsync(Devices, new { name, type, ownerEmail }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        return (body.GetProperty("id").GetGuid(), body.GetProperty("enrollmentCode").GetString()!);
    }

    private async Task<string> EnrollAsync(string code, string hardwareId, string hostname = "srv-app-01")
    {
        var response = await EnrollRawAsync(Factory.CreateAnonymousClient(), code, hardwareId, hostname);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("apiKey").GetString()!;
    }

    private async Task<Guid> AddUserAsync(string displayName, string email, DateTimeOffset signedInAt)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.Provision("test", ApiFactory.TenantId, Guid.NewGuid().ToString(), displayName, email, signedInAt);
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return user.Id;
    }

    private async Task<HashSet<string>> AuditActionsAsync(Guid deviceId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditEvents
            .Where(item => item.ResourceId == deviceId.ToString()).Select(item => item.Action).ToListAsync(Ct)).ToHashSet();
    }

    private static async Task<JsonElement> OwnerOfAsync(HttpClient admin, Guid id) =>
        (await ReadJsonAsync(await admin.GetAsync(DeviceUri(id), Ct))).GetProperty("owner");

    private static async Task<JsonElement> SearchAsync(HttpClient admin, string query)
    {
        var response = await admin.GetAsync(new Uri($"/api/v1/devices?{query}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> ListOneAsync(HttpClient admin, string name, string status = "all")
    {
        var result = await SearchAsync(admin, $"search={Uri.EscapeDataString(name)}&status={status}");
        return Assert.Single(result.GetProperty("items").EnumerateArray());
    }

    private static string[] Names(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString()!).ToArray();

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, string? etag, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, DeviceUri(id)) { Content = JsonContent.Create(body) };
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        return await client.SendAsync(request, Ct);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (method == HttpMethod.Put)
        {
            request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        }

        return await client.SendAsync(request, Ct);
    }

    private static Uri DeviceUri(Guid id) => new($"/api/v1/devices/{id}", UriKind.Relative);

    private static Uri ReportsOf(Guid id) => new($"/api/v1/devices/{id}/reports", UriKind.Relative);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, 100)];
}
