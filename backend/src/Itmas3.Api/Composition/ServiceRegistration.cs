using System.Text.Json.Serialization;
using Itmas3.Api.Authentication;
using Itmas3.Api.Authorization;
using Itmas3.Api.ErrorHandling;
using Itmas3.Api.Inventory;
using Itmas3.Api.Security;
using Itmas3.Api.SqlAudit;

namespace Itmas3.Api.Composition;

internal static class ServiceRegistration
{
    public static IServiceCollection AddApiPresentation(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddBffAuthentication(configuration, environment);
        services.AddDefaultDenyAuthorization();
        services.AddAuthorizationModule(configuration);
        services.AddInventoryModule(configuration);
        services.AddSqlAuditModule(configuration);
        services.AddApiSecurity(configuration);

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                // Generic message instead of serializer internals for malformed JSON.
                options.AllowInputFormatterExceptionMessages = false;
                // Strict input: unknown JSON properties are rejected instead of silently ignored.
                options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            });

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Instance ??= $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}");
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddOpenApi();

        return services;
    }
}
