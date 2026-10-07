using System.Security.Claims;
using Itmas3.Api.Authentication;

namespace Itmas3.UnitTests.Api;

public sealed class ClaimsCurrentUserTests
{
    [Fact]
    public void Anonymous_principal_exposes_no_identity_data()
    {
        var user = new ClaimsCurrentUser(new ClaimsPrincipal(new ClaimsIdentity([new Claim("oid", "u1")])));

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.UserId);
    }

    [Fact]
    public void Reads_short_entra_claims()
    {
        var user = Authenticated(new Claim("oid", "u1"), new Claim("tid", "t1"), new Claim("name", "Ada"), new Claim("preferred_username", "ada@example.com"));

        Assert.Equal(("u1", "t1", "Ada", "ada@example.com"), (user.UserId, user.TenantId, user.DisplayName, user.Username));
    }

    [Fact]
    public void Reads_long_form_object_and_tenant_claims()
    {
        var user = Authenticated(
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "u1"),
            new Claim("http://schemas.microsoft.com/identity/claims/tenantid", "t1"));

        Assert.Equal(("u1", "t1"), (user.UserId, user.TenantId));
    }

    private static ClaimsCurrentUser Authenticated(params Claim[] claims) =>
        new(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test")));
}
