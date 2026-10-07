using Itmas3.Api.Authentication;
using Itmas3.Api.HealthChecks;
using Itmas3.Api.Security;
using Microsoft.Extensions.Options;

namespace Itmas3.Api.Composition;

internal static class RequestPipeline
{
    /// <summary>Middleware order matters; see ARCHITECTURE.md ("Pipeline HTTP del backend").</summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        if (app.Services.GetRequiredService<IOptions<SecurityOptions>>().Value.UseHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseHttpLogging();

        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseMiddleware<SessionValidationMiddleware>();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.UseMiddleware<CsrfProtectionMiddleware>();

        app.MapControllers();
        app.MapHealthEndpoints();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        return app;
    }
}
