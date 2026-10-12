using Microsoft.AspNetCore.HttpLogging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Itmas3.Api.Observability;

/// <summary>
/// Structured logging (JSON console, configured in appsettings) plus request logging that records only
/// method, path, status and duration: no query strings, headers, cookies or bodies.
/// OpenTelemetry (traces, metrics, logs over OTLP) is enabled only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set.
/// </summary>
internal static class ObservabilitySetup
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.RequestMethod | HttpLoggingFields.RequestPath
                | HttpLoggingFields.ResponseStatusCode | HttpLoggingFields.Duration;
            options.CombineLogs = true;
        });

        if (string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            return builder;
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "itmas3-api"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddSource("Npgsql"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("System.Runtime", "Npgsql", Application.SqlAudit.SubmitSnapshot.SqlAuditMetrics.MeterName))
            .WithLogging()
            .UseOtlpExporter();

        return builder;
    }
}
