using System.Security.Claims;
using System.Text.Encodings.Web;
using Itmas3.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Itmas3.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only authentication that emulates a session created by the BFF after sign-in: the request carries the
/// application user id and security stamp of a user that exists in the test database (see <see cref="ApiFactory"/>).
/// Never registered outside the test host.
/// </summary>
internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";
    public const string StampHeader = "X-Test-Stamp";
    public const string NameHeader = "X-Test-Name";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var userId) || string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Claim[] claims =
        [
            new(ApplicationClaimTypes.UserId, userId.ToString()),
            new(ApplicationClaimTypes.SecurityStamp, Request.Headers[StampHeader].ToString()),
            new("oid", userId.ToString()),
            new("tid", ApiFactory.TenantId),
            new("name", Request.Headers[NameHeader].ToString() is { Length: > 0 } name ? name : "Usuario de Prueba"),
            new("preferred_username", "test.user@example.com"),
        ];

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName, "name", "roles"));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
