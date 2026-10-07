using Itmas3.Api.Contracts.Auth;
using Itmas3.Api.Security;
using Itmas3.Application.Identity.GetCurrentUserProfile;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Itmas3.Api.Controllers;

/// <summary>
/// Backend-for-Frontend endpoints used by the SPA to manage the browser session.
/// Sign-in and sign-out are top-level browser navigations (not XHR) because they redirect to Microsoft Entra ID.
/// </summary>
[ApiController]
[Route("auth")]
public sealed class AuthController(
    GetCurrentUserProfileHandler getCurrentUserProfile,
    IAuthenticationSchemeProvider authenticationSchemes) : ControllerBase
{
    private const string SignedOutRedirect = "/login?reason=signed-out";
    private const string SignInUnavailableRedirect = "/login?error=unavailable";

    /// <summary>Current session. Anonymous callers get <c>isAuthenticated=false</c> (not 401).</summary>
    [HttpGet("session")]
    [AllowAnonymous]
    public async Task<ActionResult<SessionResponse>> GetSession(CancellationToken cancellationToken)
    {
        var profile = await getCurrentUserProfile.HandleAsync(cancellationToken);

        return profile is null
            ? SessionResponse.Anonymous
            : new SessionResponse(true, new UserResponse(profile.DisplayName, profile.Username), profile.IsRoot, profile.Permissions);
    }

    /// <summary>Starts the OpenID Connect sign-in with Microsoft Entra ID.</summary>
    /// <param name="returnUrl">Local path to return to after sign-in. Absolute or protocol-relative URLs are rejected (open redirect).</param>
    [HttpGet("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicies.Authentication)]
    public async Task<IActionResult> Login([FromQuery] string? returnUrl)
    {
        var target = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
        if (!Url.IsLocalUrl(target))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "URL de retorno no permitida.");
        }

        if (await authenticationSchemes.GetSchemeAsync(OpenIdConnectDefaults.AuthenticationScheme) is null)
        {
            return LocalRedirect(SignInUnavailableRedirect);
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(target);
        }

        return Challenge(new AuthenticationProperties { RedirectUri = target }, OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>Ends the local session and the Microsoft Entra ID session. Submitted as a same-origin form post.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return LocalRedirect(SignedOutRedirect);
        }

        var properties = new AuthenticationProperties { RedirectUri = SignedOutRedirect };

        return await authenticationSchemes.GetSchemeAsync(OpenIdConnectDefaults.AuthenticationScheme) is null
            ? SignOut(properties, CookieAuthenticationDefaults.AuthenticationScheme)
            : SignOut(properties, CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);
    }
}
