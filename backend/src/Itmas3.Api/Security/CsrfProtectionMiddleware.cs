using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Itmas3.Api.Security;

/// <summary>
/// Cross-Site Request Forgery defence in depth, on top of the <c>SameSite=Strict</c> session cookie:
/// <list type="bullet">
/// <item>State-changing requests to <c>/api</c> must carry the custom <c>X-CSRF</c> header. Browsers cannot add
/// custom headers cross-origin without a CORS pre-flight, which the CORS policy rejects.</item>
/// <item>Any state-changing request that declares an <c>Origin</c> must come from this origin (or an allowed CORS origin).</item>
/// </list>
/// Runs after authentication, so the OpenID Connect callback (a cross-site form post from Entra ID) is unaffected.
/// </summary>
internal sealed class CsrfProtectionMiddleware(RequestDelegate next, ICorsPolicyProvider corsPolicyProvider)
{
    public const string HeaderName = "X-CSRF";

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetailsService)
    {
        var request = context.Request;

        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method))
        {
            await next(context);
            return;
        }

        if (request.Path.StartsWithSegments("/api") && !request.Headers.ContainsKey(HeaderName))
        {
            await RejectAsync(context, problemDetailsService, $"Falta la cabecera {HeaderName}.");
            return;
        }

        if (request.Headers.Origin is { Count: > 0 } origin && !await IsTrustedOriginAsync(context, origin.ToString()))
        {
            await RejectAsync(context, problemDetailsService, "Origen no permitido.");
            return;
        }

        await next(context);
    }

    private async Task<bool> IsTrustedOriginAsync(HttpContext context, string origin)
    {
        var self = $"{context.Request.Scheme}://{context.Request.Host}";
        if (string.Equals(origin, self, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var policy = await corsPolicyProvider.GetPolicyAsync(context, policyName: null);
        return policy?.IsOriginAllowed(origin) == true;
    }

    private static async Task RejectAsync(HttpContext context, IProblemDetailsService problemDetailsService, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Solicitud rechazada.",
                Detail = detail,
            },
        });
    }
}
