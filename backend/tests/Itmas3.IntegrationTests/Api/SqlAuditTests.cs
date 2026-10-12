using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Itmas3.Application.SqlAudit;
using Itmas3.Application.SqlAudit.Maintenance;
using Itmas3.Domain.SqlAudit;
using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Itmas3.IntegrationTests.Infrastructure.AgentSimulator;

namespace Itmas3.IntegrationTests.Api;

/// <summary>
/// SPEC-0005 acceptance criteria of the API: snapshot reception and processing, risk, validation, screens' queries and
/// authorization. Snapshots start from the contract example shared with the agent (sql-audit.v1.json).
/// </summary>
public sealed class SqlAuditTests(PostgreSqlFixture database) : ApiTestBase(database)
{
    private static readonly Uri Instances = new("/api/v1/sql-instances", UriKind.Relative);

    [Fact]
    public async Task CA08_a_snapshot_is_processed_once_and_older_ones_do_not_change_the_state()
    {
        var (deviceId, key) = await EnrollAsync();
        var snapshot = SqlAuditFixture();
        var submissionId = snapshot["submissionId"]!.GetValue<string>();

        var response = await SubmitSqlSnapshotAsync(Factory.CreateAnonymousClient(), key, snapshot);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(submissionId, (await ReadJsonAsync(response)).GetProperty("submissionId").GetString());

        // RF-10: the same submission again is accepted without being processed.
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitSqlSnapshotAsync(Factory.CreateAnonymousClient(), key, snapshot)).StatusCode);

        // RF-12: an older snapshot is recorded but does not change the current state.
        var older = SqlAuditFixture(DateTimeOffset.UtcNow.AddDays(-1));
        older["accounts"]!.AsArray().Add(Account("a9", "server", null, "tardia", "sql-login"));
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitSqlSnapshotAsync(Factory.CreateAnonymousClient(), key, older)).StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instance = await db.SqlInstances.SingleAsync(item => item.DeviceId == deviceId, Ct);
        Assert.Equal("ventas-prod", instance.Alias);
        Assert.Equal("sql01.example.internal", instance.Host);
        Assert.Equal(SqlTlsMode.Verify, instance.TlsMode);
        Assert.Equal(1873, instance.OmittedBuiltinPermissions);
        var receipts = await db.SqlSnapshotReceipts.Where(item => item.DeviceId == deviceId).OrderBy(item => item.ReceivedAt).ToListAsync(Ct);
        Assert.Equal(2, receipts.Count);
        Assert.True(receipts[0].Applied);
        Assert.False(receipts[1].Applied);
        Assert.Equal(8, await db.SqlAccounts.CountAsync(item => item.InstanceId == instance.Id, Ct));
        Assert.Equal(13, await db.SqlAccessItems.CountAsync(item => item.InstanceId == instance.Id, Ct));
        Assert.False(await db.SqlAccounts.AnyAsync(item => item.InstanceId == instance.Id && item.Name == "tardia", Ct));

        var row = await InstanceRowAsync(await ReaderAsync(), instance.Id);
        Assert.Equal(8, row.GetProperty("accountCount").GetInt32());
        Assert.Equal(21, row.GetProperty("pendingCount").GetInt32());
        Assert.Equal(8, row.GetProperty("pendingCriticalCount").GetInt32());
        Assert.True(row.GetProperty("hasUnavailable").GetBoolean());
        Assert.Equal("verify", row.GetProperty("tls").GetString());
        Assert.Equal("active", row.GetProperty("device").GetProperty("status").GetString());
    }

    [Fact]
    public async Task CA09_two_snapshots_mark_added_pending_removed_and_write_the_change_log()
    {
        var (_, key) = await EnrollAsync();
        var first = SqlAuditFixture(DateTimeOffset.UtcNow.AddMinutes(-10));
        await SubmitOkAsync(key, first);

        var second = SqlAuditFixture();
        var permissions = second["permissions"]!.AsArray();
        permissions.Remove(permissions.Single(item => item!["permission"]!.GetValue<string>() == "DELETE"));
        permissions.Add(Permission("a5", "object", "Ventas", "dbo", "Facturas", "table", null, "INSERT"));
        AccountByRef(second, "a2")["enabled"] = false;
        await SubmitOkAsync(key, second);

        var reader = await ReaderAsync();
        var instanceId = await InstanceIdAsync(key);
        var user = await AccountRowAsync(reader, instanceId, "app_ventas", "database");
        var items = await ItemsAsync(reader, user.GetProperty("id").GetGuid(), "validation=removed");
        Assert.Equal("DELETE", Assert.Single(items).GetProperty("permission").GetString());
        Assert.Equal("removed", items[0].GetProperty("validation").GetProperty("status").GetString());
        var current = await ItemsAsync(reader, user.GetProperty("id").GetGuid(), "kind=permission");
        var insert = current.Single(item => item.GetProperty("permission").GetString() == "INSERT");
        Assert.Equal("pending", insert.GetProperty("validation").GetProperty("status").GetString());
        Assert.Equal("high", insert.GetProperty("risk").GetString());

        var history = await HistoryAsync(reader, user.GetProperty("id").GetGuid());
        Assert.Contains(history, entry => entry.GetProperty("type").GetString() == "added" && entry.GetProperty("item").GetProperty("permission").GetString() == "INSERT");
        Assert.Contains(history, entry => entry.GetProperty("type").GetString() == "removed" && entry.GetProperty("item").GetProperty("permission").GetString() == "DELETE");

        var login = await AccountRowAsync(reader, instanceId, "app_ventas", "server");
        var loginHistory = await HistoryAsync(reader, login.GetProperty("id").GetGuid());
        var changed = Assert.Single(loginHistory);
        Assert.Equal("changed", changed.GetProperty("type").GetString());
        var field = Assert.Single(changed.GetProperty("changes").EnumerateArray());
        Assert.Equal("enabled", field.GetProperty("field").GetString());
        Assert.Equal("true", field.GetProperty("before").GetString());
        Assert.Equal("false", field.GetProperty("after").GetString());
    }

    [Fact]
    public async Task CA10_risk_comes_from_the_catalog_and_accounts_inherit_it_from_their_roles()
    {
        var (_, key) = await EnrollAsync();
        var snapshot = SqlAuditFixture();
        // rol_lectura gets CONTROL on the database: its members inherit Critical (P-24).
        snapshot["permissions"]!.AsArray().Add(Permission("a8", "database", "Ventas", null, null, null, null, "CONTROL"));
        await SubmitOkAsync(key, snapshot);

        var reader = await ReaderAsync();
        var instanceId = await InstanceIdAsync(key);
        Assert.Equal("critical", (await AccountRowAsync(reader, instanceId, "sa", "server")).GetProperty("risk").GetString());
        Assert.Equal("critical", (await AccountRowAsync(reader, instanceId, "DOMINIO\\dba-equipo", "server")).GetProperty("risk").GetString());
        Assert.Equal("critical", (await AccountRowAsync(reader, instanceId, "antiguo_reportes", "database")).GetProperty("risk").GetString());

        var user = await AccountRowAsync(reader, instanceId, "app_ventas", "database");
        Assert.Equal("critical", user.GetProperty("risk").GetString());
        Assert.True(user.GetProperty("riskInherited").GetBoolean());
        Assert.Equal(["rol_lectura"], user.GetProperty("roles").EnumerateArray().Select(role => role.GetString()!).ToArray());
        var detail = await reader.GetFromJsonAsync<JsonElement>(AccountUri(user.GetProperty("id").GetGuid()), Ct);
        Assert.Equal("high", detail.GetProperty("ownRisk").GetString());
        Assert.Equal("critical", detail.GetProperty("inheritedRisk").GetString());

        var items = await ItemsAsync(reader, user.GetProperty("id").GetGuid());
        Assert.Equal("high", items.Single(item => item.GetProperty("kind").GetString() == "role-membership").GetProperty("risk").GetString());
        Assert.Equal("general", items.Single(item => item.GetProperty("permission").GetString() == "SELECT" && item.GetProperty("state").GetString() == "grant").GetProperty("risk").GetString());
        Assert.Equal("denied", items.Single(item => item.GetProperty("state").GetString() == "deny").GetProperty("risk").GetString());
        // WITH GRANT OPTION, unknown permissions: High (RN-9).
        Assert.Equal("high", items.Single(item => item.GetProperty("state").GetString() == "grant-with-grant-option").GetProperty("risk").GetString());
        var login = await AccountRowAsync(reader, instanceId, "app_ventas", "server");
        var loginItems = await ItemsAsync(reader, login.GetProperty("id").GetGuid());
        Assert.Equal("high", loginItems.Single(item => item.GetProperty("permission").GetString() == "VIEW SERVER STATE").GetProperty("risk").GetString());
        Assert.Equal("general", loginItems.Single(item => item.GetProperty("permission").GetString() == "CONNECT SQL").GetProperty("risk").GetString());
    }

    [Fact]
    public async Task CA11_one_click_marks_and_unmarks_accounts_roles_and_permissions_and_is_audited()
    {
        var (_, key) = await EnrollAsync();
        await SubmitOkAsync(key, SqlAuditFixture());
        var validator = await Factory.CreateAuthenticatedClientAsync(false, SqlAuditPermissions.Read, SqlAuditPermissions.AccessesValidate);
        var instanceId = await InstanceIdAsync(key);
        var user = await AccountRowAsync(validator, instanceId, "app_ventas", "database");
        var userId = user.GetProperty("id").GetGuid();
        var items = await ItemsAsync(validator, userId);
        var membership = items.Single(item => item.GetProperty("kind").GetString() == "role-membership").GetProperty("id").GetGuid();
        var permission = items.First(item => item.GetProperty("kind").GetString() == "permission").GetProperty("id").GetGuid();

        foreach (var itemId in new[] { membership, permission })
        {
            var marked = await validator.PutAsync(ItemValidationUri(itemId), null, Ct);
            Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
            var body = await ReadJsonAsync(marked);
            Assert.Equal("valid", body.GetProperty("status").GetString());
            Assert.StartsWith("Usuario ", body.GetProperty("by").GetString(), StringComparison.Ordinal);
            // Idempotent.
            Assert.Equal(HttpStatusCode.OK, (await validator.PutAsync(ItemValidationUri(itemId), null, Ct)).StatusCode);
        }

        var accountMarked = await validator.PutAsJsonAsync(AccountValidationUri(userId), new { fingerprint = user.GetProperty("fingerprint").GetString() }, Ct);
        Assert.Equal(HttpStatusCode.OK, accountMarked.StatusCode);

        var reloaded = await AccountRowAsync(validator, instanceId, "app_ventas", "database");
        Assert.Equal("valid", reloaded.GetProperty("validation").GetProperty("status").GetString());
        Assert.Equal(user.GetProperty("pendingCount").GetInt32() - 3, reloaded.GetProperty("pendingCount").GetInt32());
        Assert.Equal("valid", (await ItemsAsync(validator, userId)).Single(item => item.GetProperty("id").GetGuid() == permission).GetProperty("validation").GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await validator.DeleteAsync(ItemValidationUri(permission), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await validator.DeleteAsync(ItemValidationUri(permission), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await validator.DeleteAsync(AccountValidationUri(userId), Ct)).StatusCode);
        Assert.Equal("pending", (await ItemsAsync(validator, userId)).Single(item => item.GetProperty("id").GetGuid() == permission).GetProperty("validation").GetProperty("status").GetString());

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actions = await db.AuditEvents
            .Where(item => item.ResourceId == permission.ToString() || item.ResourceId == userId.ToString() || item.ResourceId == membership.ToString())
            .Select(item => item.Action).ToListAsync(Ct);
        Assert.Equal(2, actions.Count(action => action == SqlAuditAuditActions.ItemValidated));
        Assert.Equal(1, actions.Count(action => action == SqlAuditAuditActions.ItemUnvalidated));
        Assert.Equal(1, actions.Count(action => action == SqlAuditAuditActions.AccountValidated));
        Assert.Equal(1, actions.Count(action => action == SqlAuditAuditActions.AccountUnvalidated));

        var history = await HistoryAsync(validator, userId);
        Assert.Contains(history, entry => entry.GetProperty("type").GetString() == "validated" && entry.GetProperty("target").GetString() == "permission");
        Assert.Contains(history, entry => entry.GetProperty("type").GetString() == "unvalidated" && entry.GetProperty("target").GetString() == "account");
    }

    [Fact]
    public async Task CA12_changed_accounts_need_revalidation_and_stale_or_removed_elements_are_rejected()
    {
        var (_, key) = await EnrollAsync();
        await SubmitOkAsync(key, SqlAuditFixture(DateTimeOffset.UtcNow.AddMinutes(-10)));
        var validator = await Factory.CreateAuthenticatedClientAsync(false, SqlAuditPermissions.Read, SqlAuditPermissions.AccessesValidate);
        var instanceId = await InstanceIdAsync(key);
        var login = await AccountRowAsync(validator, instanceId, "app_ventas", "server");
        var loginId = login.GetProperty("id").GetGuid();
        var oldFingerprint = login.GetProperty("fingerprint").GetString();
        Assert.Equal(HttpStatusCode.OK, (await validator.PutAsJsonAsync(AccountValidationUri(loginId), new { fingerprint = oldFingerprint }, Ct)).StatusCode);
        var user = await AccountRowAsync(validator, instanceId, "app_ventas", "database");
        var delete = (await ItemsAsync(validator, user.GetProperty("id").GetGuid())).Single(item => item.GetProperty("permission").GetString() == "DELETE").GetProperty("id").GetGuid();

        var second = SqlAuditFixture();
        AccountByRef(second, "a2")["password"]!["expirationEnforced"] = false;
        var permissions = second["permissions"]!.AsArray();
        permissions.Remove(permissions.Single(item => item!["permission"]!.GetValue<string>() == "DELETE"));
        await SubmitOkAsync(key, second);

        var changed = await AccountRowAsync(validator, instanceId, "app_ventas", "server");
        Assert.Equal("changed", changed.GetProperty("validation").GetProperty("status").GetString());
        Assert.NotEqual(oldFingerprint, changed.GetProperty("fingerprint").GetString());

        var stale = await validator.PutAsJsonAsync(AccountValidationUri(loginId), new { fingerprint = oldFingerprint }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("application/problem+json", stale.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, (await validator.PutAsJsonAsync(AccountValidationUri(loginId), new { fingerprint = changed.GetProperty("fingerprint").GetString() }, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await validator.PutAsync(ItemValidationUri(delete), null, Ct)).StatusCode);

        // A removed account cannot be validated either.
        var third = SqlAuditFixture(DateTimeOffset.UtcNow.AddSeconds(-10));
        RemoveAccount(third, "a6");
        await SubmitOkAsync(key, third);
        var orphan = Assert.Single(await AccountsAsync(validator, instanceId, "validation=removed"));
        var rejected = await validator.PutAsJsonAsync(
            AccountValidationUri(orphan.GetProperty("id").GetGuid()), new { fingerprint = orphan.GetProperty("fingerprint").GetString() }, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
    }

    [Fact]
    public async Task CA13_lists_show_pending_counters_and_filters_work()
    {
        var (_, key) = await EnrollAsync();
        await SubmitOkAsync(key, SqlAuditFixture());
        var reader = await ReaderAsync();
        var instanceId = await InstanceIdAsync(key);

        Assert.Equal(8, (await AccountsAsync(reader, instanceId, "validation=pending")).Count);
        Assert.Equal(5, (await AccountsAsync(reader, instanceId, "risk=critical")).Count);
        Assert.Equal(4, (await AccountsAsync(reader, instanceId, "level=database&database=Ventas")).Count);
        Assert.Equal(["db_owner", "rol_lectura"], (await AccountsAsync(reader, instanceId, "type=database-role&sort=name")).Select(item => item.GetProperty("name").GetString()!).ToArray());
        Assert.Equal("sysadmin", Assert.Single(await AccountsAsync(reader, instanceId, "search=SYSAD")).GetProperty("name").GetString());
        Assert.Empty(await AccountsAsync(reader, instanceId, "validation=valid"));

        var criticalPending = await PermissionsAsync(reader, instanceId, "risk=critical&validation=pending");
        Assert.Equal("OWNERSHIP", Assert.Single(criticalPending).GetProperty("permission").GetString());
        Assert.Equal("sa", criticalPending[0].GetProperty("account").GetProperty("name").GetString());
        Assert.Equal(10, (await PermissionsAsync(reader, instanceId, string.Empty)).Count);
        Assert.Equal(["procedure"], (await PermissionsAsync(reader, instanceId, "objectType=procedure")).Select(item => item.GetProperty("securable").GetProperty("objectType").GetString()!).ToArray());
        Assert.Equal(2, (await PermissionsAsync(reader, instanceId, "account=ventas&database=Ventas&search=Facturas")).Count);
        var ordered = await PermissionsAsync(reader, instanceId, string.Empty);
        Assert.Equal("critical", ordered[0].GetProperty("risk").GetString());

        var detail = await reader.GetFromJsonAsync<JsonElement>(new Uri($"/api/v1/sql-instances/{instanceId}", UriKind.Relative), Ct);
        Assert.Equal(3, detail.GetProperty("databases").GetArrayLength());
        Assert.Equal("database:Archivo", detail.GetProperty("unavailable")[0].GetProperty("section").GetString());
        Assert.Contains("table", detail.GetProperty("objectTypes").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("windows-group", detail.GetProperty("accountTypes").EnumerateArray().Select(item => item.GetString()));

        Assert.Equal(HttpStatusCode.BadRequest, (await reader.GetAsync(new Uri($"/api/v1/sql-instances/{instanceId}/accounts?risk=extreme", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reader.GetAsync(new Uri("/api/v1/sql-instances?pageSize=101", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync(new Uri($"/api/v1/sql-instances/{Guid.NewGuid()}/accounts", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task CA14_user_endpoints_require_their_permission_and_device_credentials_are_not_interchangeable()
    {
        var (_, key) = await EnrollAsync();
        await SubmitOkAsync(key, SqlAuditFixture());
        var instanceId = await InstanceIdAsync(key);
        var reader = await ReaderAsync();
        var account = await AccountRowAsync(reader, instanceId, "sa", "server");
        var accountId = account.GetProperty("id").GetGuid();
        var itemId = (await ItemsAsync(reader, accountId))[0].GetProperty("id").GetGuid();
        var reads = new[]
        {
            Instances, new($"/api/v1/sql-instances/{instanceId}", UriKind.Relative), new($"/api/v1/sql-instances/{instanceId}/accounts", UriKind.Relative),
            new($"/api/v1/sql-instances/{instanceId}/permissions", UriKind.Relative), AccountUri(accountId),
            new($"/api/v1/sql-accounts/{accountId}/items", UriKind.Relative), new($"/api/v1/sql-accounts/{accountId}/changes", UriKind.Relative),
        };
        var anonymous = Factory.CreateAnonymousClient();
        var withoutPermission = await Factory.CreateAuthenticatedClientAsync();
        var device = Factory.CreateAnonymousClient();
        device.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Device", key);

        foreach (var uri in reads)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(uri, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await withoutPermission.GetAsync(uri, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync(uri, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync(uri, Ct)).StatusCode);
        }

        anonymous.DefaultRequestHeaders.Add("X-CSRF", "1");
        device.DefaultRequestHeaders.Add("X-CSRF", "1");
        var body = new { fingerprint = account.GetProperty("fingerprint").GetString() };
        foreach (var client in new[] { anonymous, device })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync(AccountValidationUri(accountId), body, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsync(ItemValidationUri(itemId), null, Ct)).StatusCode);
        }

        // Reading is not enough to validate.
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync(AccountValidationUri(accountId), body, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.DeleteAsync(AccountValidationUri(accountId), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsync(ItemValidationUri(itemId), null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.DeleteAsync(ItemValidationUri(itemId), Ct)).StatusCode);

        // A user session (even Root) cannot send snapshots.
        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);
        Assert.Equal(HttpStatusCode.Unauthorized, (await root.PostAsJsonAsync(SqlSnapshots, SqlAuditFixture(), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SubmitSqlSnapshotAsync(Factory.CreateAnonymousClient(), "not-a-key", SqlAuditFixture())).StatusCode);
    }

    [Theory]
    [InlineData("unknown-property")]
    [InlineData("unknown-reference")]
    [InlineData("unknown-enum")]
    [InlineData("control-characters")]
    [InlineData("instance-null-without-unavailable")]
    [InlineData("duplicate-account")]
    [InlineData("duplicate-permission")]
    [InlineData("securable-shape")]
    [InlineData("missing-property")]
    [InlineData("future-collection")]
    [InlineData("too-many-accounts")]
    [InlineData("malformed")]
    public async Task CA15_invalid_snapshots_are_rejected_without_changes(string defect)
    {
        var (deviceId, key) = await EnrollAsync();
        var snapshot = SqlAuditFixture();
        switch (defect)
        {
            case "unknown-property": snapshot["connection"]!["password"] = "placeholder-not-a-secret"; break;
            case "unknown-reference": snapshot["roleMemberships"]![0]!["roleRef"] = "a99"; break;
            case "unknown-enum": snapshot["permissions"]![0]!["state"] = "revoke"; break;
            case "control-characters": AccountByRef(snapshot, "a2")["name"] = "app\u0007"; break;
            case "instance-null-without-unavailable": snapshot["instance"] = null; break;
            case "duplicate-account": snapshot["accounts"]!.AsArray().Add(Account("a9", "server", null, "sa", "sql-login")); break;
            case "duplicate-permission": snapshot["permissions"]!.AsArray().Add(snapshot["permissions"]![0]!.DeepClone()); break;
            case "securable-shape": snapshot["permissions"]![0]!["securable"]!["column"] = "Email"; break;
            case "missing-property": AccountByRef(snapshot, "a2").Remove("isOrphaned"); break;
            case "future-collection": snapshot["collectedAt"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O"); break;
            case "too-many-accounts":
                snapshot["accounts"] = new JsonArray(Enumerable.Range(0, 20_001).Select(i => (JsonNode)Account($"x{i}", "server", null, $"cuenta{i}", "sql-login")).ToArray());
                snapshot["roleMemberships"] = new JsonArray();
                snapshot["permissions"] = new JsonArray();
                snapshot["databases"]!.AsArray().ToList().ForEach(database => database!["ownerRef"] = null);
                break;
        }

        var response = defect == "malformed"
            ? await SendAsync(Factory.CreateAnonymousClient(), HttpMethod.Post, SqlSnapshots, $"Device {key}", "{\"schemaVersion\": 1,")
            : await SubmitSqlSnapshotAsync(Factory.CreateAnonymousClient(), key, snapshot);

        Assert.Equal(defect == "future-collection" ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await using var scope = Factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().SqlInstances.AnyAsync(item => item.DeviceId == deviceId, Ct));
    }

    [Fact]
    public async Task CA15_snapshots_up_to_16_MB_are_accepted_and_larger_ones_rejected()
    {
        // TestServer ignores Kestrel limits: real Kestrel, so the global 1 MB limit and this route's 16 MB apply.
        await using var kestrel = Factory.WithWebHostBuilder(_ => { });
        kestrel.UseKestrel(0);
        kestrel.StartServer();
        var (_, key) = await EnrollAsync(kestrel.CreateClient());

        var accepted = SqlAuditFixture();
        AddFillerAccounts(accepted, 9_000);
        Assert.InRange(accepted.ToJsonString().Length, 2 * 1024 * 1024, 15 * 1024 * 1024);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitSqlSnapshotAsync(kestrel.CreateClient(), key, accepted)).StatusCode);

        var oversized = SqlAuditFixture();
        AddFillerAccounts(oversized, 19_000);
        foreach (var account in oversized["accounts"]!.AsArray().Skip(8))
        {
            account!["defaultSchema"] = new string('s', 256);
            account["defaultDatabase"] = new string('d', 256);
        }

        Assert.True(oversized.ToJsonString().Length > SqlAuditContractMaxBody);
        // Expect: 100-continue lets Kestrel answer 413 from the Content-Length before the client streams 16 MB.
        using var request = new HttpRequestMessage(HttpMethod.Post, SqlSnapshots) { Content = Json(oversized.ToJsonString()) };
        request.Headers.TryAddWithoutValidation("Authorization", $"Device {key}");
        request.Headers.Add("X-CSRF", "1");
        request.Headers.ExpectContinue = true;
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await kestrel.CreateClient().SendAsync(request, Ct)).StatusCode);

        // The rest of the API keeps the 1 MB limit.
        var inventory = ContractFixture();
        inventory["software"]!["applications"] = new JsonArray(Enumerable.Range(0, 4500).Select(_ => (JsonNode)new string('x', 250)).ToArray());
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await SubmitAsync(kestrel.CreateClient(), key, inventory)).StatusCode);
    }

    [Fact]
    public async Task CA16_a_deleted_device_cannot_send_and_its_instances_remain_visible()
    {
        var (deviceId, key) = await EnrollAsync();
        await SubmitOkAsync(key, SqlAuditFixture());
        var admin = await Factory.CreateAuthenticatedClientAsync(false, Application.Inventory.InventoryPermissions.DevicesDelete, SqlAuditPermissions.Read);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(new Uri($"/api/v1/devices/{deviceId}", UriKind.Relative), Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SubmitSqlSnapshotAsync(Factory.CreateAnonymousClient(), key, SqlAuditFixture())).StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var instanceId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().SqlInstances.Where(item => item.DeviceId == deviceId).Select(item => item.Id).SingleAsync(Ct);
        var row = await InstanceRowAsync(admin, instanceId);
        Assert.Equal("deleted", row.GetProperty("device").GetProperty("status").GetString());
        Assert.Equal(8, (await AccountsAsync(admin, instanceId, string.Empty)).Count);
    }

    [Fact]
    public async Task CA17_names_with_markup_and_quotes_are_stored_and_returned_literally()
    {
        var (_, key) = await EnrollAsync();
        var snapshot = SqlAuditFixture();
        const string Markup = "<script>alert(1)</script>";
        const string Quotes = "o'brien\"]; DROP TABLE x;--";
        AccountByRef(snapshot, "a2")["name"] = Markup;
        snapshot["permissions"]![4]!["securable"]!["name"] = Quotes;
        await SubmitOkAsync(key, snapshot);

        var reader = await ReaderAsync();
        var instanceId = await InstanceIdAsync(key);
        Assert.Equal(Markup, (await AccountRowAsync(reader, instanceId, Markup, "server")).GetProperty("name").GetString());
        Assert.Contains(await PermissionsAsync(reader, instanceId, string.Empty), item => item.GetProperty("securable").GetProperty("name").GetString() == Quotes);
    }

    [Fact]
    public async Task MySql_anonymous_accounts_and_grants_on_database_patterns_are_accepted()
    {
        var (_, key) = await EnrollAsync();
        var snapshot = SqlAuditFixture();
        snapshot["connection"]!["engine"] = "mysql";
        var anonymous = Account("a9", "server", null, string.Empty, "mysql-account");
        anonymous["host"] = "localhost";
        snapshot["accounts"]!.AsArray().Add(anonymous);
        snapshot["permissions"]!.AsArray().Add(Permission("a9", "database", "app\\_%", null, null, null, null, "SELECT"));
        await SubmitOkAsync(key, snapshot);

        var reader = await ReaderAsync();
        var instanceId = await InstanceIdAsync(key);
        var row = (await AccountsAsync(reader, instanceId, "type=mysql-account")).Single();
        Assert.Equal(string.Empty, row.GetProperty("name").GetString());
        Assert.Equal("localhost", row.GetProperty("host").GetString());
        Assert.Equal("high", row.GetProperty("risk").GetString());
        Assert.Contains(await PermissionsAsync(reader, instanceId, string.Empty), item => item.GetProperty("securable").GetProperty("database").GetString() == "app\\_%");

        // An empty name is only valid for that type.
        var invalid = SqlAuditFixture();
        AccountByRef(invalid, "a2")["name"] = string.Empty;
        Assert.Equal(HttpStatusCode.BadRequest, (await SubmitSqlSnapshotAsync(Factory.CreateAnonymousClient(), key, invalid)).StatusCode);
    }

    [Fact]
    public async Task CA06_RF13_unavailable_parts_keep_their_state_and_are_flagged()
    {
        var (_, key) = await EnrollAsync();
        await SubmitOkAsync(key, SqlAuditFixture(DateTimeOffset.UtcNow.AddMinutes(-10)));

        // The database Ventas could not be read: its accounts and permissions are not in the snapshot.
        var partial = SqlAuditFixture(DateTimeOffset.UtcNow.AddMinutes(-5));
        foreach (var reference in new[] { "a5", "a6", "a7", "a8" })
        {
            RemoveAccount(partial, reference);
        }

        partial["unavailable"]!.AsArray().Add(new JsonObject { ["section"] = "database:Ventas", ["reason"] = "permission-denied" });
        await SubmitOkAsync(key, partial);

        var reader = await ReaderAsync();
        var instanceId = await InstanceIdAsync(key);
        var user = await AccountRowAsync(reader, instanceId, "app_ventas", "database");
        Assert.True(user.GetProperty("stale").GetBoolean());
        Assert.Equal(JsonValueKind.Null, user.GetProperty("removedAt").ValueKind);
        Assert.All(await ItemsAsync(reader, user.GetProperty("id").GetGuid()), item => Assert.True(item.GetProperty("stale").GetBoolean()));
        Assert.Empty(await AccountsAsync(reader, instanceId, "validation=removed"));
        // The OWNERSHIP of sa on Ventas belongs to that database: kept as well.
        Assert.Contains(await PermissionsAsync(reader, instanceId, "risk=critical"), item => item.GetProperty("permission").GetString() == "OWNERSHIP");

        // The connection fails entirely: nothing is removed and the instance keeps its version.
        var failed = SqlAuditFixture();
        failed["instance"] = null;
        failed["databases"] = new JsonArray();
        failed["accounts"] = new JsonArray();
        failed["roleMemberships"] = new JsonArray();
        failed["permissions"] = new JsonArray();
        failed["unavailable"] = new JsonArray(new JsonObject { ["section"] = "instance", ["reason"] = "connection-failed" });
        await SubmitOkAsync(key, failed);

        var detail = await reader.GetFromJsonAsync<JsonElement>(new Uri($"/api/v1/sql-instances/{instanceId}", UriKind.Relative), Ct);
        Assert.Equal("16.0.4175.1", detail.GetProperty("version").GetString());
        Assert.Equal(3, detail.GetProperty("databases").GetArrayLength());
        Assert.Equal(8, detail.GetProperty("accountCount").GetInt32());
        Assert.All(await AccountsAsync(reader, instanceId, string.Empty), account => Assert.True(account.GetProperty("stale").GetBoolean()));
    }

    [Fact]
    public async Task Each_device_has_its_own_instances_and_tls_mode_is_reported()
    {
        var (_, keyA) = await EnrollAsync();
        var (_, keyB) = await EnrollAsync();
        var snapshot = SqlAuditFixture();
        snapshot["connection"]!["tls"] = "unverified";
        snapshot["connection"]!["host"] = "SQL01.Example.Internal.";
        await SubmitOkAsync(keyA, snapshot);
        await SubmitOkAsync(keyB, SqlAuditFixture());

        var idA = await InstanceIdAsync(keyA);
        var idB = await InstanceIdAsync(keyB);
        Assert.NotEqual(idA, idB);
        var row = await InstanceRowAsync(await ReaderAsync(), idA);
        Assert.Equal("unverified", row.GetProperty("tls").GetString());
        Assert.Equal("sql01.example.internal", row.GetProperty("host").GetString());

        // Same host with different letter case and trailing dot: same instance (P-23 normalisation).
        await SubmitOkAsync(keyA, SqlAuditFixture());
        Assert.Equal(idA, await InstanceIdAsync(keyA));
    }

    [Fact]
    public async Task Purge_deletes_history_and_removed_elements_older_than_the_retention()
    {
        var (_, key) = await EnrollAsync();
        await SubmitOkAsync(key, SqlAuditFixture(DateTimeOffset.UtcNow.AddMinutes(-10)));
        var second = SqlAuditFixture();
        RemoveAccount(second, "a6");
        await SubmitOkAsync(key, second);
        var instanceId = await InstanceIdAsync(key);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var old = DateTimeOffset.UtcNow.AddDays(-400);
        await db.SqlAccessChanges.Where(item => item.InstanceId == instanceId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DetectedAt, old), Ct);
        await db.SqlAccessItems.Where(item => item.InstanceId == instanceId && item.RemovedAt != null).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RemovedAt, old), Ct);
        await db.SqlAccounts.Where(item => item.InstanceId == instanceId && item.RemovedAt != null).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RemovedAt, old), Ct);

        Assert.True(await scope.ServiceProvider.GetRequiredService<PurgeSqlAuditHistoryHandler>().HandleAsync(Ct) > 0);

        Assert.False(await db.SqlAccessChanges.AnyAsync(item => item.InstanceId == instanceId, Ct));
        Assert.False(await db.SqlAccounts.AnyAsync(item => item.InstanceId == instanceId && item.Name == "antiguo_reportes", Ct));
        Assert.Equal(7, await db.SqlAccounts.CountAsync(item => item.InstanceId == instanceId, Ct));
        Assert.Equal(12, await db.SqlAccessItems.CountAsync(item => item.InstanceId == instanceId, Ct));
    }

    [Fact]
    public async Task RF15_risks_calculated_with_an_older_catalog_are_recalculated()
    {
        var (_, key) = await EnrollAsync();
        await SubmitOkAsync(key, SqlAuditFixture());
        var instanceId = await InstanceIdAsync(key);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.SqlAccessItems.Where(item => item.InstanceId == instanceId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Risk, RiskLevel.General), Ct);
        await db.SqlAccounts.Where(item => item.InstanceId == instanceId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.OwnRisk, RiskLevel.General).SetProperty(item => item.InheritedRisk, RiskLevel.General), Ct);
        await db.SqlInstances.Where(item => item.Id == instanceId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RiskCatalogVersion, 0), Ct);

        Assert.True(await scope.ServiceProvider.GetRequiredService<RecalculateSqlRiskHandler>().HandleAsync(Ct) >= 1);

        var reader = await ReaderAsync();
        Assert.Equal("critical", (await AccountRowAsync(reader, instanceId, "DOMINIO\\dba-equipo", "server")).GetProperty("risk").GetString());
        Assert.Equal(8, (await InstanceRowAsync(reader, instanceId)).GetProperty("pendingCriticalCount").GetInt32());
    }

    // ---- helpers -----------------------------------------------------------------------------------------------

    private const int SqlAuditContractMaxBody = 16 * 1024 * 1024;

    private async Task<(Guid DeviceId, string ApiKey)> EnrollAsync(HttpClient? client = null)
    {
        var (_, code) = await Factory.RegisterDeviceAsync();
        var response = await EnrollRawAsync(client ?? Factory.CreateAnonymousClient(), code, NewHardwareId());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        return (body.GetProperty("deviceId").GetGuid(), body.GetProperty("apiKey").GetString()!);
    }

    private async Task SubmitOkAsync(string key, JsonNode snapshot)
    {
        var response = await SubmitSqlSnapshotAsync(Factory.CreateAnonymousClient(), key, snapshot);
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(Ct));
    }

    private Task<HttpClient> ReaderAsync() => Factory.CreateAuthenticatedClientAsync(false, SqlAuditPermissions.Read);

    private async Task<Guid> InstanceIdAsync(string apiKey)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = Application.Inventory.DeviceSecrets.Hash(apiKey);
        return await db.SqlInstances
            .Where(instance => db.Devices.Any(device => device.Id == instance.DeviceId && device.ApiKeyHash == hash))
            .Select(instance => instance.Id).SingleAsync(Ct);
    }

    private static async Task<JsonElement> InstanceRowAsync(HttpClient client, Guid instanceId)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/sql-instances?pageSize=100&sort=-lastCollectedAt", UriKind.Relative), Ct);
        return page.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == instanceId);
    }

    private static async Task<List<JsonElement>> AccountsAsync(HttpClient client, Guid instanceId, string query)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/v1/sql-instances/{instanceId}/accounts?pageSize=100&{query}", UriKind.Relative), Ct);
        return [.. page.GetProperty("items").EnumerateArray()];
    }

    private static async Task<JsonElement> AccountRowAsync(HttpClient client, Guid instanceId, string name, string level) =>
        (await AccountsAsync(client, instanceId, $"level={level}&search={Uri.EscapeDataString(name)}"))
            .Single(item => item.GetProperty("name").GetString() == name);

    private static async Task<List<JsonElement>> PermissionsAsync(HttpClient client, Guid instanceId, string query)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/v1/sql-instances/{instanceId}/permissions?pageSize=100&{query}", UriKind.Relative), Ct);
        return [.. page.GetProperty("items").EnumerateArray()];
    }

    private static async Task<List<JsonElement>> ItemsAsync(HttpClient client, Guid accountId, string query = "")
    {
        var page = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/v1/sql-accounts/{accountId}/items?pageSize=100&{query}", UriKind.Relative), Ct);
        return [.. page.GetProperty("items").EnumerateArray()];
    }

    private static async Task<List<JsonElement>> HistoryAsync(HttpClient client, Guid accountId)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/v1/sql-accounts/{accountId}/changes?pageSize=100", UriKind.Relative), Ct);
        return [.. page.GetProperty("items").EnumerateArray()];
    }

    private static Uri AccountUri(Guid id) => new($"/api/v1/sql-accounts/{id}", UriKind.Relative);

    private static Uri AccountValidationUri(Guid id) => new($"/api/v1/sql-accounts/{id}/validation", UriKind.Relative);

    private static Uri ItemValidationUri(Guid id) => new($"/api/v1/sql-access-items/{id}/validation", UriKind.Relative);

    private static JsonObject AccountByRef(JsonNode snapshot, string reference) =>
        snapshot["accounts"]!.AsArray().Single(account => account!["ref"]!.GetValue<string>() == reference)!.AsObject();

    /// <summary>Removes an account and everything that references it, as if it had been dropped in the engine.</summary>
    private static void RemoveAccount(JsonNode snapshot, string reference)
    {
        var accounts = snapshot["accounts"]!.AsArray();
        accounts.Remove(AccountByRef(snapshot, reference));
        foreach (var membership in snapshot["roleMemberships"]!.AsArray().Where(item => item!["memberRef"]!.GetValue<string>() == reference || item["roleRef"]!.GetValue<string>() == reference).ToList())
        {
            snapshot["roleMemberships"]!.AsArray().Remove(membership);
        }

        foreach (var permission in snapshot["permissions"]!.AsArray().Where(item => item!["granteeRef"]!.GetValue<string>() == reference).ToList())
        {
            snapshot["permissions"]!.AsArray().Remove(permission);
        }

        foreach (var permission in snapshot["permissions"]!.AsArray().Where(item => item!["grantorRef"]?.GetValue<string>() == reference))
        {
            permission!["grantorRef"] = null;
        }

        foreach (var account in accounts.Where(item => item!["loginRef"]?.GetValue<string>() == reference))
        {
            account!["loginRef"] = null;
        }
    }

    private static void AddFillerAccounts(JsonNode snapshot, int count)
    {
        var accounts = snapshot["accounts"]!.AsArray();
        for (var i = 0; i < count; i++)
        {
            var account = Account($"f{i}", "server", null, $"cuenta_de_relleno_con_nombre_largo_{i:D6}_{new string('n', 150)}", "sql-login");
            account["defaultDatabase"] = "master";
            accounts.Add(account);
        }
    }

    private static JsonObject Account(string reference, string level, string? database, string name, string type) => new()
    {
        ["ref"] = reference,
        ["level"] = level,
        ["database"] = database,
        ["name"] = name,
        ["host"] = null,
        ["type"] = type,
        ["enabled"] = true,
        ["locked"] = false,
        ["createdAt"] = "2026-01-01T00:00:00+00:00",
        ["modifiedAt"] = null,
        ["defaultDatabase"] = null,
        ["defaultSchema"] = null,
        ["loginRef"] = null,
        ["isOrphaned"] = false,
        ["password"] = null,
    };

    private static JsonObject Permission(
        string grantee, string securableClass, string? database, string? schema, string? name, string? objectType, string? column, string permission) => new()
    {
        ["granteeRef"] = grantee,
        ["securable"] = new JsonObject
        {
            ["class"] = securableClass,
            ["database"] = database,
            ["schema"] = schema,
            ["name"] = name,
            ["objectType"] = objectType,
            ["column"] = column,
        },
        ["permission"] = permission,
        ["state"] = "grant",
        ["grantorRef"] = null,
    };
}
