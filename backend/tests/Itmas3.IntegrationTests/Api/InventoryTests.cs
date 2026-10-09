using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Itmas3.Application.Inventory.PurgeHistory;
using Itmas3.Domain.Inventory;
using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itmas3.IntegrationTests.Api;

/// <summary>SPEC-0002 acceptance criteria of the device endpoints, through the HTTP API.</summary>
public sealed class InventoryTests(PostgreSqlFixture database) : ApiTestBase(database)
{
    private static readonly Uri Enrollments = new("/api/v1/device-enrollments", UriKind.Relative);
    private static readonly Uri Reports = new("/api/v1/inventory-reports", UriKind.Relative);

    [Fact]
    public async Task CA04_valid_code_enrolls_the_device_and_is_audited()
    {
        var hardwareId = NewHardwareId();
        var response = await EnrollRawAsync(Factory.CreateAnonymousClient(), Factory.EnrollmentCode, hardwareId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var deviceId = body.GetProperty("deviceId").GetGuid();
        Assert.Equal(43, body.GetProperty("apiKey").GetString()!.Length);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await db.Devices.SingleAsync(item => item.Id == deviceId, Ct);
        Assert.Equal(hardwareId, device.HardwareId);
        Assert.Equal("1", device.TermsVersion);
        Assert.NotEqual(body.GetProperty("apiKey").GetString(), device.ApiKeyHash);

        var audit = await db.AuditEvents.SingleAsync(item => item.ResourceId == deviceId.ToString(), Ct);
        Assert.Equal("device.enrolled", audit.Action);
        Assert.Equal("success", audit.Result);
        var after = JsonNode.Parse(audit.After!)!;
        Assert.Equal("1", after["termsVersion"]!.GetValue<string>());
        Assert.Equal("srv-app-01", after["hostname"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("wrong-code-wrong-code-wrong-code-0000")]
    [InlineData("")]
    public async Task CA04_invalid_or_missing_code_is_rejected_without_creating_devices(string code)
    {
        var hardwareId = NewHardwareId();
        var client = Factory.CreateAnonymousClient();

        var response = code.Length == 0
            ? await client.PostAsJsonAsync(Enrollments, EnrollmentBody(hardwareId), Ct)
            : await EnrollRawAsync(client, code, hardwareId);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var scope = Factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Devices.AnyAsync(item => item.HardwareId == hardwareId, Ct));
    }

    [Fact]
    public async Task Enrollment_is_disabled_without_a_configured_code()
    {
        await using var factory = Factory.WithWebHostBuilder(builder => builder.UseSetting("Inventory:EnrollmentCode", string.Empty));

        var response = await EnrollRawAsync(factory.CreateClient(), Factory.EnrollmentCode, NewHardwareId());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RN03_terms_that_are_not_current_are_rejected()
    {
        var client = Factory.CreateAnonymousClient();
        var body = EnrollmentBody(NewHardwareId());
        body["termsVersion"] = "0";

        var response = await SendAsync(client, HttpMethod.Post, Enrollments, $"Enrollment {Factory.EnrollmentCode}", body.ToJsonString());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CA13_and_CA20_reports_are_stored_for_the_authenticated_device_with_its_connection_ip()
    {
        await using var factory = WithRemoteIp("203.0.113.77");
        var (deviceA, keyA) = await EnrollAsync(factory.CreateClient());
        var (deviceB, _) = await EnrollAsync(factory.CreateClient());

        var client = factory.CreateClient();
        // A client-supplied X-Forwarded-For from an untrusted address must be ignored.
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.200");
        var response = await SubmitAsync(client, keyA, ContractFixture());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var report = await db.InventoryReports.SingleAsync(item => item.DeviceId == deviceA, Ct);
        Assert.Equal("203.0.113.77", report.ConnectionIp);
        Assert.Equal("203.0.113.25", report.PublicIp);
        Assert.Equal("CO", report.CountryCode);
        Assert.Equal("Bogotá", report.City);
        Assert.False(await db.InventoryReports.AnyAsync(item => item.DeviceId == deviceB, Ct));

        var device = await db.Devices.SingleAsync(item => item.Id == deviceA, Ct);
        Assert.Equal(report.Id, device.LastReportId);
        Assert.Equal("srv-app-01", device.Hostname);
        Assert.Equal("encrypted", device.SystemVolumeEncryption);
        Assert.Equal("active", device.FirewallStatus);
        Assert.True(device.HasLocalAdmins);
        Assert.Equal("203.0.113.77", device.LastConnectionIp);

        var stored = JsonNode.Parse(report.Payload)!;
        Assert.Equal("nginx", stored["software"]!["applications"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task CA20_forwarded_for_is_honoured_only_from_the_trusted_proxy()
    {
        await using var factory = WithRemoteIp("127.0.0.1");
        var (deviceId, key) = await EnrollAsync(factory.CreateClient());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.20");

        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(client, key, ContractFixture())).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var report = await scope.ServiceProvider.GetRequiredService<AppDbContext>().InventoryReports.SingleAsync(item => item.DeviceId == deviceId, Ct);
        Assert.Equal("198.51.100.20", report.ConnectionIp);
    }

    [Fact]
    public async Task CA14_reenrollment_invalidates_the_previous_key()
    {
        var hardwareId = NewHardwareId();
        var (deviceId, oldKey) = await EnrollAsync(Factory.CreateAnonymousClient(), hardwareId);
        var (sameDevice, newKey) = await EnrollAsync(Factory.CreateAnonymousClient(), hardwareId);

        Assert.Equal(deviceId, sameDevice);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SubmitAsync(Factory.CreateAnonymousClient(), oldKey, ContractFixture())).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(Factory.CreateAnonymousClient(), newKey, ContractFixture())).StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var actions = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditEvents
            .Where(item => item.ResourceId == deviceId.ToString()).Select(item => item.Action).ToListAsync(Ct);
        Assert.Contains("device.relinked", actions);
    }

    [Theory]
    [InlineData("unknown-property")]
    [InlineData("too-many-applications")]
    [InlineData("control-characters")]
    [InlineData("null-section-not-declared")]
    [InlineData("invalid-public-ip")]
    [InlineData("missing-property")]
    [InlineData("future-schema")]
    public async Task CA15_invalid_reports_are_rejected_with_problem_details(string defect)
    {
        var (_, key) = await EnrollAsync(Factory.CreateAnonymousClient());
        var report = ContractFixture();
        switch (defect)
        {
            case "unknown-property": report["system"]!["serialNumber"] = "X"; break;
            case "too-many-applications": report["software"]!["applications"] = new JsonArray(Enumerable.Range(0, 5001).Select(i => (JsonNode)$"app{i}").ToArray()); break;
            case "control-characters": report["system"]!["hostname"] = "srv\u0007"; break;
            case "null-section-not-declared": report["security"]!["firewall"] = null; break;
            case "invalid-public-ip": report["network"]!["publicNetwork"]!["ip"] = "<script>"; break;
            case "missing-property": report["hardware"]!.AsObject().Remove("disks"); break;
            case "future-schema": report["schemaVersion"] = 2; break;
        }

        var response = await SubmitAsync(Factory.CreateAnonymousClient(), key, report);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CA15_oversized_reports_are_rejected()
    {
        // TestServer ignores Kestrel limits: run this host on real Kestrel so the 1 MB body limit applies.
        await using var kestrel = Factory.WithWebHostBuilder(_ => { });
        kestrel.UseKestrel(0);
        kestrel.StartServer();
        var (_, key) = await EnrollAsync(kestrel.CreateClient());
        var report = ContractFixture();
        report["software"]!["applications"] = new JsonArray(Enumerable.Range(0, 4500).Select(i => (JsonNode)new string('x', 250)).ToArray());

        var response = await SubmitAsync(kestrel.CreateClient(), key, report);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Unavailable_sections_are_accepted_when_declared()
    {
        var (_, key) = await EnrollAsync(Factory.CreateAnonymousClient());
        var report = ContractFixture();
        report["network"]!["publicNetwork"] = null;
        report["unavailable"] = new JsonArray(new JsonObject { ["section"] = "network.publicNetwork", ["reason"] = "timeout" });

        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(Factory.CreateAnonymousClient(), key, report)).StatusCode);
    }

    [Fact]
    public async Task CA16_device_and_user_credentials_are_not_interchangeable()
    {
        var (_, key) = await EnrollAsync(Factory.CreateAnonymousClient());
        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);

        // A user session (even Root) cannot use device endpoints.
        Assert.Equal(HttpStatusCode.Unauthorized, (await root.PostAsJsonAsync(Reports, ContractFixture(), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await root.PostAsJsonAsync(Enrollments, EnrollmentBody(NewHardwareId()), Ct)).StatusCode);

        // A device key cannot use user endpoints, and the enrollment code is not a device credential.
        var deviceClient = Factory.CreateAnonymousClient();
        deviceClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Device", key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await deviceClient.GetAsync(new Uri("/api/v1/users", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(Factory.CreateAnonymousClient(), HttpMethod.Post, Reports, $"Device {Factory.EnrollmentCode}", ContractFixture().ToJsonString())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(Factory.CreateAnonymousClient(), HttpMethod.Post, Reports, $"Enrollment {Factory.EnrollmentCode}", ContractFixture().ToJsonString())).StatusCode);
    }

    [Fact]
    public async Task Device_requests_require_the_anti_csrf_header()
    {
        var (_, key) = await EnrollAsync(Factory.CreateAnonymousClient());
        using var request = new HttpRequestMessage(HttpMethod.Post, Reports) { Content = Json(ContractFixture().ToJsonString()) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Device", key);

        Assert.Equal(HttpStatusCode.Forbidden, (await Factory.CreateAnonymousClient().SendAsync(request, Ct)).StatusCode);
    }

    [Fact]
    public async Task Reports_are_rate_limited_per_device()
    {
        await using var factory = Factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:InventoryReportsPermitLimitPerHour", "2"));
        var (_, key) = await EnrollAsync(factory.CreateClient());
        var (_, otherKey) = await EnrollAsync(factory.CreateClient());

        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(factory.CreateClient(), key, ContractFixture())).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(factory.CreateClient(), key, ContractFixture())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SubmitAsync(factory.CreateClient(), key, ContractFixture())).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(factory.CreateClient(), otherKey, ContractFixture())).StatusCode);
    }

    [Fact]
    public async Task RN05_older_reports_are_kept_in_history_without_replacing_the_latest()
    {
        var (deviceId, key) = await EnrollAsync(Factory.CreateAnonymousClient());
        var latest = ContractFixture();
        latest["collectedAt"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");
        var older = ContractFixture();
        older["collectedAt"] = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        older["system"]!["hostname"] = "old-name";

        await SubmitAsync(Factory.CreateAnonymousClient(), key, latest);
        await SubmitAsync(Factory.CreateAnonymousClient(), key, older);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.InventoryReports.CountAsync(item => item.DeviceId == deviceId, Ct));
        Assert.Equal("srv-app-01", (await db.Devices.SingleAsync(item => item.Id == deviceId, Ct)).Hostname);
    }

    [Fact]
    public async Task RF17_purge_removes_expired_history_but_keeps_the_latest_report()
    {
        var (deviceId, key) = await EnrollAsync(Factory.CreateAnonymousClient());
        var expired = ContractFixture();
        expired["collectedAt"] = DateTimeOffset.UtcNow.AddDays(-200).ToString("O");
        var stillLatest = ContractFixture();
        stillLatest["collectedAt"] = DateTimeOffset.UtcNow.AddDays(-100).ToString("O");

        // The newest of these is older than the retention period, but it is the latest one: it must survive.
        await SubmitAsync(Factory.CreateAnonymousClient(), key, expired);
        await SubmitAsync(Factory.CreateAnonymousClient(), key, stillLatest);

        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PurgeInventoryHistoryHandler>().HandleAsync(Ct);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var remaining = await db.InventoryReports.Where(item => item.DeviceId == deviceId).ToListAsync(Ct);
        var report = Assert.Single(remaining);
        Assert.Equal((await db.Devices.SingleAsync(item => item.Id == deviceId, Ct)).LastReportId, report.Id);
    }

    [Fact]
    public async Task Contract_fixture_shared_with_the_agent_is_accepted()
    {
        var (_, key) = await EnrollAsync(Factory.CreateAnonymousClient());

        var response = await SubmitAsync(Factory.CreateAnonymousClient(), key, ContractFixture());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private ApiFactoryWithRemoteIp WithRemoteIp(string ip) => new(Factory, ip);

    private async Task<(Guid DeviceId, string ApiKey)> EnrollAsync(HttpClient client, string? hardwareId = null)
    {
        var response = await EnrollRawAsync(client, Factory.EnrollmentCode, hardwareId ?? NewHardwareId());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        return (body.GetProperty("deviceId").GetGuid(), body.GetProperty("apiKey").GetString()!);
    }

    private static Task<HttpResponseMessage> EnrollRawAsync(HttpClient client, string code, string hardwareId) =>
        SendAsync(client, HttpMethod.Post, Enrollments, $"Enrollment {code}", EnrollmentBody(hardwareId).ToJsonString());

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, string apiKey, JsonNode report) =>
        SendAsync(client, HttpMethod.Post, Reports, $"Device {apiKey}", report.ToJsonString());

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, Uri uri, string authorization, string json)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = Json(json) };
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.Add("X-CSRF", "1");
        return await client.SendAsync(request, Ct);
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static JsonObject EnrollmentBody(string hardwareId) => new()
    {
        ["hardwareId"] = hardwareId,
        ["hostname"] = "srv-app-01",
        ["osFamily"] = "linux",
        ["agentVersion"] = "1.0.0",
        ["termsVersion"] = "1",
        ["termsAcceptedAt"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"),
    };

    private static string NewHardwareId() => $"hw-{Guid.NewGuid():N}";

    /// <summary>Contract v1 example shared with the agent tests (agent/tests/fixtures/contract).</summary>
    private static JsonNode ContractFixture()
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "agent", "tests", "fixtures", "contract", "inventory-report.v1.json")))!;
        node["collectedAt"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");
        return node;
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "constitution.md")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    /// <summary>Test host whose requests arrive from a fixed client address (TestServer has none by default).</summary>
    private sealed class ApiFactoryWithRemoteIp(ApiFactory factory, string ip) : IAsyncDisposable
    {
        private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _inner = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter(IPAddress.Parse(ip)))));

        public IServiceProvider Services => _inner.Services;

        public HttpClient CreateClient() => _inner.CreateClient();

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    private sealed class RemoteIpStartupFilter(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((HttpContext context, Func<Task> nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware();
            });
            next(app);
        };
    }
}
