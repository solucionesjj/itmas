using System.Security.Claims;
using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Authorization.Users.ProvisionUser;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Itmas3.Api.Authentication;

/// <summary>
/// Runs after Microsoft Entra ID validated the id token: provisions/refreshes the application user (SPEC-0001 RF-1),
/// refuses disabled users and guests, and adds the internal user id and security stamp to the session.
/// </summary>
internal static class SignInProvisioning
{
    public const string Provider = "entra-id";

    public static async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            context.Fail("The authenticated principal has no identity.");
            return;
        }

        var objectId = First(identity, "oid", "http://schemas.microsoft.com/identity/claims/objectidentifier");
        var tenantId = First(identity, "tid", "http://schemas.microsoft.com/identity/claims/tenantid");
        if (objectId is null || tenantId is null)
        {
            context.Fail("The id token does not contain the oid/tid claims.");
            return;
        }

        var signIn = new SignInIdentity(
            Provider,
            tenantId,
            objectId,
            First(identity, "name"),
            First(identity, "preferred_username", "email"),
            IsGuest(identity, tenantId));

        var handler = context.HttpContext.RequestServices.GetRequiredService<ProvisionUserHandler>();
        var result = await handler.HandleAsync(signIn, context.HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            context.HandleResponse();
            context.Response.Redirect(result.Rejection == SignInRejection.GuestNotAllowed ? "/login?error=guest" : "/login?error=disabled");
            return;
        }

        identity.AddClaim(new Claim(ApplicationClaimTypes.UserId, result.UserId!.Value.ToString()));
        identity.AddClaim(new Claim(ApplicationClaimTypes.SecurityStamp, result.SecurityStamp!));
    }

    /// <summary>
    /// B2B guests carry an <c>idp</c> claim pointing to their home identity provider (another tenant or a consumer
    /// provider). Members either have no <c>idp</c> claim or one that refers to the same tenant.
    /// </summary>
    private static bool IsGuest(ClaimsIdentity identity, string tenantId) =>
        First(identity, "idp") is { } idp && !idp.Contains(tenantId, StringComparison.OrdinalIgnoreCase);

    private static string? First(ClaimsIdentity identity, params string[] claimTypes) =>
        claimTypes.Select(type => identity.FindFirst(type)?.Value).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
