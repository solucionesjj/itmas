using Itmas3.Api.Authentication;
using Itmas3.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Itmas3.Api.Authorization;

/// <summary>
/// Default-deny authorization. See specs/architecture/adr-0003-authorization-default-deny.md.
/// </summary>
internal static class AuthorizationSetup
{
    public static IServiceCollection AddDefaultDenyAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // Applied to [Authorize] and [RequirePermission] (which adds its own permission requirement).
            .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            // Endpoints that declare NO authorization metadata are denied to everybody, authenticated or not.
            // Every endpoint must explicitly opt in: [AllowAnonymous], [RequirePermission] or the AuthenticatedUser policy.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireAssertion(_ => false).Build())
            .AddPolicy(AuthorizationPolicies.AuthenticatedUser, policy => policy.RequireAuthenticatedUser());

        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser>(serviceProvider =>
            new ClaimsCurrentUser(serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext?.User));

        return services;
    }
}
