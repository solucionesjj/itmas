using System.Net;
using Itmas3.IntegrationTests.Infrastructure;

namespace Itmas3.IntegrationTests.Api;

public sealed class ErrorHandlingAndHeadersTests(PostgreSqlFixture database) : ApiTestBase(database)
{
    [Fact]
    public async Task Unexpected_errors_return_generic_problem_details_without_internal_information()
    {
        var response = await Factory.CreateAnonymousClient().GetAsync(new Uri("/api/test/unexpected-error", UriKind.Relative), Ct);
        var raw = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("internal-detail", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", raw, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", raw, StringComparison.Ordinal);
        Assert.True((await ReadJsonAsync(response)).TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Domain_rule_violations_return_422_with_the_safe_message()
    {
        var response = await Factory.CreateAnonymousClient().GetAsync(new Uri("/api/test/domain-error", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("La operación no cumple la regla de negocio.", (await ReadJsonAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Responses_carry_security_headers_and_hide_the_server()
    {
        var response = await Factory.CreateAnonymousClient().GetAsync(new Uri("/auth/session", UriKind.Relative), Ct);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.False(response.Headers.Contains("Server"));
    }
}
