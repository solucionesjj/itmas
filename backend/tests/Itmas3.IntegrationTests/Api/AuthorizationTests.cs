using System.Net;
using Itmas3.IntegrationTests.Infrastructure;

namespace Itmas3.IntegrationTests.Api;

/// <summary>Default deny: authentication alone never grants access to application resources.</summary>
public sealed class AuthorizationTests(PostgreSqlFixture database) : ApiTestBase(database)
{
    [Theory]
    [InlineData("/api/test/no-authorization-metadata")]
    [InlineData("/api/test/authenticated")]
    [InlineData("/api/test/permission")]
    public async Task Anonymous_callers_get_401_problem_details(string path)
    {
        var response = await Factory.CreateAnonymousClient().GetAsync(new Uri(path, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Endpoints_without_authorization_metadata_are_denied_to_authenticated_users()
    {
        var client = await Factory.CreateAuthenticatedClientAsync(isRoot: false, "users.read");
        var response = await client.GetAsync(new Uri("/api/test/no-authorization-metadata", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Permissions_are_denied_until_explicitly_granted()
    {
        var client = await Factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync(new Uri("/api/test/permission", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_without_authorization_metadata_are_denied_even_to_root()
    {
        var client = await Factory.CreateAuthenticatedClientAsync(isRoot: true);
        var response = await client.GetAsync(new Uri("/api/test/no-authorization-metadata", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_policy_allows_signed_in_users()
    {
        var client = await Factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync(new Uri("/api/test/authenticated", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
