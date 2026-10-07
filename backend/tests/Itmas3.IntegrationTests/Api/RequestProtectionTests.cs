using System.Net;
using System.Net.Http.Json;
using System.Text;
using Itmas3.IntegrationTests.Infrastructure;

namespace Itmas3.IntegrationTests.Api;

public sealed class RequestProtectionTests(PostgreSqlFixture database) : ApiTestBase(database)
{
    private static readonly Uri Echo = new("/api/test/echo", UriKind.Relative);

    [Fact]
    public async Task State_changing_api_requests_without_csrf_header_are_rejected()
    {
        var response = await Factory.CreateAnonymousClient().PostAsJsonAsync(Echo, new { message = "hola" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Valid_requests_with_csrf_header_succeed()
    {
        var response = await Send(new { message = "hola" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Model_validation_errors_return_validation_problem_details()
    {
        var response = await Send(new { message = new string('x', 51) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Unknown_json_properties_are_rejected()
    {
        var response = await Send(new { message = "hola", isAdmin = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_json_does_not_leak_serializer_details()
    {
        var client = Factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-CSRF", "1");

        var response = await client.PostAsync(Echo, new StringContent("{\"message\": ", Encoding.UTF8, "application/json"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("System.Text.Json", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    private async Task<HttpResponseMessage> Send(object payload)
    {
        var client = Factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-CSRF", "1");
        return await client.PostAsJsonAsync(Echo, payload, Ct);
    }
}
