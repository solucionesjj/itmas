using Itmas3.Api.Composition;
using Itmas3.Api.Observability;
using Itmas3.Application;
using Itmas3.Infrastructure;

// Composition root. Business logic never lives here or in controllers.
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 1 * 1024 * 1024;
});

builder.AddObservability();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApiPresentation(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseApiPipeline();

await app.RunAsync();

/// <summary>Entry point; exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
