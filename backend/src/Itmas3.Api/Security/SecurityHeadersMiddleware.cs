namespace Itmas3.Api.Security;

/// <summary>
/// Defensive HTTP headers for API responses (JSON only, never rendered as a document).
/// The SPA's own headers (CSP for HTML) are set by the frontend web server (frontend/nginx).
/// </summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            // API responses may contain personal data: never store them in shared or browser caches.
            if (!headers.ContainsKey("Cache-Control"))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
