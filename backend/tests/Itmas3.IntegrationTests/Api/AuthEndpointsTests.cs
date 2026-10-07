using System.Net;
using Itmas3.Api.Authentication;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Itmas3.IntegrationTests.Api;

public sealed class AuthEndpointsTests(PostgreSqlFixture database) : ApiTestBase(database)
{
    [Fact]
    public async Task Session_is_anonymous_without_authentication()
    {
        var response = await Factory.CreateAnonymousClient().GetAsync(new Uri("/auth/session", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.False(body.GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact]
    public async Task Session_returns_the_signed_in_user_without_tokens()
    {
        var user = await Factory.CreateUserAsync(permissions: "users.read");
        var response = await Factory.CreateClientFor(user).GetAsync(new Uri("/auth/session", UriKind.Relative), Ct);

        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal(user.DisplayName, body.GetProperty("user").GetProperty("displayName").GetString());
        Assert.False(body.GetProperty("isRoot").GetBoolean());
        Assert.Equal(["users.read"], body.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()));
        Assert.DoesNotContain("token", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://evil.example.com/")]
    [InlineData("//evil.example.com")]
    [InlineData("/\\evil.example.com")]
    public async Task Login_rejects_non_local_return_urls(string returnUrl)
    {
        var response = await Factory.CreateAnonymousClient()
            .GetAsync(new Uri($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_when_already_signed_in_returns_to_the_local_url()
    {
        var client = await Factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync(new Uri("/auth/login?returnUrl=%2Fhome", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/home", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Logout_without_session_redirects_to_login()
    {
        var response = await Factory.CreateAnonymousClient().PostAsync(new Uri("/auth/logout", UriKind.Relative), null, Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login?reason=signed-out", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Logout_from_a_foreign_origin_is_rejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        request.Headers.Add("Origin", "https://evil.example.com");

        var client = await Factory.CreateAuthenticatedClientAsync();
        var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void Session_cookie_is_hardened()
    {
        var cookie = Factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme).Cookie;

        Assert.Equal(AuthenticationSetup.SessionCookieName, cookie.Name);
        Assert.StartsWith("__Host-", cookie.Name, StringComparison.Ordinal);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal(CookieSecurePolicy.Always, cookie.SecurePolicy);
    }

    [Fact]
    public void OpenId_connect_uses_authorization_code_with_pkce_and_keeps_tokens_out_of_the_cookie()
    {
        var options = Factory.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);

        Assert.Equal("code", options.ResponseType);
        Assert.True(options.UsePkce);
        Assert.False(options.SaveTokens);
        Assert.Equal("/signin-oidc", options.CallbackPath);
    }
}
