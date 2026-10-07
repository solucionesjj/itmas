using System.Net;
using Itmas3.IntegrationTests.Infrastructure;

namespace Itmas3.IntegrationTests.Api;

public sealed class HealthCheckTests(PostgreSqlFixture database) : ApiTestBase(database)
{
    [Fact]
    public async Task Health_reports_api_and_postgresql_as_healthy()
    {
        var response = await Factory.CreateAnonymousClient().GetAsync(new Uri("/health", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("postgresql").GetString());
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Liveness_and_readiness_are_anonymous_and_healthy(string path)
    {
        var response = await Factory.CreateAnonymousClient().GetAsync(new Uri(path, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
