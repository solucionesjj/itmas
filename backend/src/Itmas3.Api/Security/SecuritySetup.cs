using System.Net;
using System.Threading.RateLimiting;
using Itmas3.Api.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Itmas3.Api.Security;

internal static class SecuritySetup
{
    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.SectionName));
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);

        AddForwardedHeaders(services, configuration);
        AddCors(services, configuration);
        AddRateLimiting(services);

        return services;
    }

    /// <summary>
    /// Trust X-Forwarded-* only from the reverse proxy networks declared in <c>ReverseProxy:KnownNetworks</c>
    /// (plus loopback). Needed so that OIDC redirect URIs, cookie security and client IPs are correct.
    /// </summary>
    private static void AddForwardedHeaders(IServiceCollection services, IConfiguration configuration)
    {
        var knownNetworks = configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [];
        var allowedHosts = (configuration["AllowedHosts"] ?? "*")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = 1;

            foreach (var network in knownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }

            if (!allowedHosts.Contains("*"))
            {
                foreach (var host in allowedHosts)
                {
                    options.AllowedHosts.Add(host);
                }
            }
        });
    }

    /// <summary>
    /// The SPA is served from the same origin (reverse proxy), so CORS is closed by default.
    /// Origins are only opened explicitly through <c>Cors:AllowedOrigins</c>.
    /// </summary>
    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Length == 0)
            {
                return;
            }

            policy.WithOrigins(allowedOrigins)
                .AllowCredentials()
                .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete)
                .WithHeaders("Content-Type", CsrfProtectionMiddleware.HeaderName);
        }));
    }

    private static void AddRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.Request.Path.StartsWithSegments("/health"))
                {
                    return RateLimitPartition.GetNoLimiter("health");
                }

                var limits = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                var partitionKey = new ClaimsCurrentUser(context.User).UserId is { } userId
                    ? $"user:{userId}"
                    : $"ip:{ClientAddress(context)}";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.PermitLimitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });

            options.AddPolicy(RateLimitingPolicies.Authentication, context =>
            {
                var limits = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter($"ip:{ClientAddress(context)}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.AuthenticationPermitLimitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });

            options.OnRejected = async (rejection, cancellationToken) =>
            {
                var context = rejection.HttpContext;
                if (rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Demasiadas solicitudes.",
                    },
                });
            };
        });
    }

    private static string ClientAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString();
}
