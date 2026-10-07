using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Itmas3.Api.Authentication;

/// <summary>
/// Backend-for-Frontend (BFF) authentication. The API is a confidential OpenID Connect client of
/// Microsoft Entra ID (authorization code + PKCE). Tokens never reach the browser: the SPA only holds an
/// encrypted, HttpOnly, SameSite=Strict session cookie. See specs/architecture/adr-0002-authentication-bff.md.
/// </summary>
internal static class AuthenticationSetup
{
    public const string SessionCookieName = "__Host-itmas3-session";
    private const string AbsoluteExpiryKey = ".itmas3.absolute-expiry";

    public static IServiceCollection AddBffAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<AuthSessionOptions>()
            .Bind(configuration.GetSection(AuthSessionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var entraSection = configuration.GetSection(EntraIdSettings.SectionName);
        var entraConfigured = EntraIdSettings.HasApplicationIdentity(entraSection);

        if (!environment.IsDevelopment()
            && (!entraConfigured || !EntraIdSettings.HasClientCredential(entraSection)))
        {
            // Fail fast: outside Development the API never starts without an identity provider.
            throw new InvalidOperationException(
                "Microsoft Entra ID is not configured. Set EntraId__TenantId, EntraId__ClientId and a client credential.");
        }

        var authentication = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme);

        if (entraConfigured)
        {
            authentication.AddMicrosoftIdentityWebApp(
                entraSection,
                OpenIdConnectDefaults.AuthenticationScheme,
                CookieAuthenticationDefaults.AuthenticationScheme);

            services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, ConfigureOpenIdConnect);
        }
        else
        {
            // Development without an Entra app registration: the API runs, every protected endpoint
            // answers 401 and /auth/login reports that sign-in is unavailable.
            authentication.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthSessionOptions>, TimeProvider>(ConfigureSessionCookie);

        // Only /auth/login starts the interactive OIDC flow. Every other unauthenticated request gets a
        // 401/403 status (never a redirect), which is what an SPA expects.
        services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        });

        return services;
    }

    private static void ConfigureOpenIdConnect(OpenIdConnectOptions options)
    {
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        // Tokens are not needed by the foundation and are never persisted in the cookie.
        options.SaveTokens = false;
        options.MapInboundClaims = false;
        options.Scope.Remove("offline_access");
        options.TokenValidationParameters.NameClaimType = "name";

        // Authentication answers "who"; the application decides "what" (SPEC-0001 §17): provision the app user.
        var previousTokenValidated = options.Events.OnTokenValidated;
        options.Events.OnTokenValidated = async context =>
        {
            await previousTokenValidated(context);
            if (context.Result is null)
            {
                await SignInProvisioning.OnTokenValidatedAsync(context);
            }
        };
    }

    private static void ConfigureSessionCookie(
        CookieAuthenticationOptions options, IOptions<AuthSessionOptions> sessionOptions, TimeProvider timeProvider)
    {
        var session = sessionOptions.Value;

        options.Cookie.Name = SessionCookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(session.IdleTimeoutMinutes);
        options.SlidingExpiration = true;

        var events = options.Events;

        events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };

        var previousSigningIn = events.OnSigningIn;
        events.OnSigningIn = async context =>
        {
            await previousSigningIn(context);
            var absoluteExpiry = timeProvider.GetUtcNow().AddHours(session.AbsoluteTimeoutHours);
            context.Properties.SetString(AbsoluteExpiryKey, absoluteExpiry.ToString("O", CultureInfo.InvariantCulture));
        };

        var previousValidatePrincipal = events.OnValidatePrincipal;
        events.OnValidatePrincipal = async context =>
        {
            await previousValidatePrincipal(context);

            var value = context.Properties.GetString(AbsoluteExpiryKey);
            var expired = !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var absoluteExpiry)
                || timeProvider.GetUtcNow() >= absoluteExpiry;

            if (expired)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    }
}
