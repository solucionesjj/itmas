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
                // The path of a call to Graph carries the address of the mailbox: its spans are not exported (SPEC-0006 RNF-8).
                // The operations with Outlook are counted by itmas.agenda.outlook.operations and the duration by the HTTP metrics.
                .AddHttpClientInstrumentation(options =>
                    options.FilterHttpRequestMessage = request => !string.Equals(request.RequestUri?.Host, "graph.microsoft.com", StringComparison.OrdinalIgnoreCase))
                .AddSource("Npgsql"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(
                    "System.Runtime",
                    "Npgsql",
                    Application.SqlAudit.SubmitSnapshot.SqlAuditMetrics.MeterName,
                    Application.Agenda.Outlook.AgendaMetrics.MeterName))
            .WithLogging()
            .UseOtlpExporter();

        return builder;
    }
}
