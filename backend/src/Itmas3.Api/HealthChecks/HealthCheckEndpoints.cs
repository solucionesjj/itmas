using System.Text.Json;
using Itmas3.Infrastructure.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Itmas3.Api.HealthChecks;

/// <summary>
/// <c>/health</c>: overall status (API + PostgreSQL). <c>/health/live</c>: the process is up (no dependencies).
/// <c>/health/ready</c>: dependencies needed to serve traffic are reachable.
/// Responses expose only check names and statuses, never exception messages or connection details.
/// </summary>
internal static class HealthCheckEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        Map(endpoints, "/health", _ => true);
        Map(endpoints, "/health/live", _ => false);
        Map(endpoints, "/health/ready", registration => registration.Tags.Contains(HealthCheckTags.Ready));
        return endpoints;
    }

    private static void Map(IEndpointRouteBuilder endpoints, string pattern, Func<HealthCheckRegistration, bool> predicate) =>
        endpoints.MapHealthChecks(pattern, new HealthCheckOptions { Predicate = predicate, ResponseWriter = WriteResponseAsync })
            .AllowAnonymous()
            .DisableHttpMetrics()
            .DisableRateLimiting()
            .WithHttpLogging(HttpLoggingFields.None);

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString()),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
