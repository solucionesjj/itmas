using System.Net;
using System.Text.Json.Nodes;
using Itmas3.Agent.Api;
using Itmas3.Agent.Collectors.Model;

namespace Itmas3.Agent.Tests;

public sealed class ApiClientTests
{
    private static readonly Uri Server = new("https://itmas.example.test");

    [Fact]
    public async Task Enrollment_sends_the_code_in_the_authorization_header_with_the_csrf_header()
    {
        var handler = StubHandler.Returning(HttpStatusCode.Created, """{"deviceId":"0199b5a0-0000-7000-8000-000000000001","apiKey":"key-placeholder"}""");
        using var http = new HttpClient(handler);

        var response = await new ItmasApiClient(http).EnrollAsync(Server, "code-placeholder", Request(), Fixtures.Ct);

        Assert.Equal("key-placeholder", response.ApiKey);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://itmas.example.test/api/v1/device-enrollments"), request.RequestUri);
        Assert.Equal(("Enrollment", "code-placeholder"), (request.Headers.Authorization!.Scheme, request.Headers.Authorization.Parameter));
        Assert.Equal("1", Assert.Single(request.Headers.GetValues("X-CSRF")));
        var body = JsonNode.Parse(handler.Bodies[0])!;
        Assert.Equal("hw-12345678", body["hardwareId"]!.GetValue<string>());
        Assert.DoesNotContain("code-placeholder", handler.Bodies[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "código de autorización no es válido")]
    [InlineData(HttpStatusCode.NotFound, "no está habilitado")]
    [InlineData(HttpStatusCode.TooManyRequests, "Demasiados intentos")]
    public async Task Enrollment_errors_are_explained(HttpStatusCode status, string message)
    {
        using var http = new HttpClient(StubHandler.Returning(status));

        var exception = await Assert.ThrowsAsync<EnrollmentException>(() => new ItmasApiClient(http).EnrollAsync(Server, "c", Request(), Fixtures.Ct));
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted, SubmitOutcome.Accepted)]
    [InlineData(HttpStatusCode.Unauthorized, SubmitOutcome.CredentialRejected)]
    [InlineData(HttpStatusCode.TooManyRequests, SubmitOutcome.Failed)]
    [InlineData(HttpStatusCode.BadRequest, SubmitOutcome.Failed)]
    public async Task Inventory_is_sent_with_the_device_key(HttpStatusCode status, SubmitOutcome expected)
    {
        var handler = StubHandler.Returning(status);
        using var http = new HttpClient(handler);
        var report = InventoryJson.Deserialize(Fixtures.Read("contract/inventory-report.v1.json"))!;

        var (outcome, _) = await new ItmasApiClient(http).SubmitAsync(Server, "device-key-placeholder", report, Fixtures.Ct);

        Assert.Equal(expected, outcome);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://itmas.example.test/api/v1/inventory-reports"), request.RequestUri);
        Assert.Equal(("Device", "device-key-placeholder"), (request.Headers.Authorization!.Scheme, request.Headers.Authorization.Parameter));
        Assert.Equal("1", Assert.Single(request.Headers.GetValues("X-CSRF")));
        Assert.Equal("srv-app-01", JsonNode.Parse(handler.Bodies[0])!["system"]!["hostname"]!.GetValue<string>());
    }

    [Fact]
    public async Task Server_check_uses_the_public_health_endpoint_and_keeps_a_path_prefix()
    {
        var handler = StubHandler.Returning(HttpStatusCode.OK, "ok");
        using var http = new HttpClient(handler);

        Assert.Null(await new ItmasApiClient(http).CheckServerAsync(Itmas3.Agent.Configuration.AgentConfiguration.BaseUri("https://host.test/itmas"), Fixtures.Ct));

        Assert.Equal(new Uri("https://host.test/itmas/healthz"), Assert.Single(handler.Requests).RequestUri);
        Assert.Null(handler.Requests[0].Headers.Authorization);
    }

    [Fact]
    public async Task Server_check_reports_servers_that_are_not_itmas()
    {
        using var http = new HttpClient(StubHandler.Returning(HttpStatusCode.NotFound));

        var problem = await new ItmasApiClient(http).CheckServerAsync(Server, Fixtures.Ct);

        Assert.Contains("no parece un servidor ITMas", problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Certificate_errors_are_explained()
    {
        using var http = new HttpClient(new StubHandler((_, _) =>
            throw new HttpRequestException("The SSL connection could not be established, see inner exception.",
                new System.Security.Authentication.AuthenticationException("The remote certificate is invalid because of errors in the certificate chain: RemoteCertificateNameMismatch"))));

        var problem = await new ItmasApiClient(http).CheckServerAsync(Server, Fixtures.Ct);

        Assert.Contains("certificado HTTPS", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Dns_errors_are_explained() =>
        Assert.Contains("resolver el nombre", ItmasApiClient.DescribeConnectionError(
            new HttpRequestException("x", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.HostNotFound))), StringComparison.Ordinal);

    [Fact]
    public void Http_client_keeps_default_tls_validation()
    {
        using var client = ItmasApiClient.CreateHttpClient("1.0.0");

        Assert.Equal("ITMas-Agent/1.0.0", client.DefaultRequestHeaders.UserAgent.ToString());
        Assert.Equal(TimeSpan.FromSeconds(60), client.Timeout);
    }

    private static EnrollmentRequest Request() =>
        new("hw-12345678", "srv-app-01", "linux", "1.0.0", "1", DateTimeOffset.UtcNow);
}
