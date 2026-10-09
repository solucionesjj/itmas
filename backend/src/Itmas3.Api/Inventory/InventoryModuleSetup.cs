using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Itmas3.Api.Authorization;
using Itmas3.Api.Security;
using Itmas3.Application.Inventory;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Itmas3.Api.Inventory;

/// <summary>Composition of the device inventory module (SPEC-0002, ADR-0009).</summary>
internal static class InventoryModuleSetup
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(InventoryOptions.SectionName);
        services.AddOptions<InventoryOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .Validate(
                options => !options.EnrollmentEnabled || options.EnrollmentCode!.Length >= InventoryOptions.MinEnrollmentCodeLength,
                $"Inventory:EnrollmentCode must have at least {InventoryOptions.MinEnrollmentCodeLength} characters (or be empty to disable enrollment).")
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<InventoryOptions>>().Value;
            return new InventorySettings
            {
                HistoryRetentionDays = options.HistoryRetentionDays,
                AcceptedTermsVersions = options.AcceptedTermsVersions
                    .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.Ordinal),
            };
        });

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, EnrollmentCodeAuthenticationHandler>(DeviceAuthenticationSchemes.Enrollment, _ => { })
            .AddScheme<AuthenticationSchemeOptions, DeviceApiKeyAuthenticationHandler>(DeviceAuthenticationSchemes.Device, _ => { });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.DeviceEnrollment, policy => policy
                .AddAuthenticationSchemes(DeviceAuthenticationSchemes.Enrollment)
                .RequireAuthenticatedUser()
                .RequireClaim(DeviceAuthenticationSchemes.EnrollmentClaim))
            .AddPolicy(AuthorizationPolicies.EnrolledDevice, policy => policy
                .AddAuthenticationSchemes(DeviceAuthenticationSchemes.Device)
                .RequireAuthenticatedUser()
                .RequireClaim(DeviceAuthenticationSchemes.DeviceIdClaim));

        services.AddRateLimiter(options =>
        {
            // Per client IP: also slows down brute force against the authorization code.
            options.AddPolicy(RateLimitingPolicies.DeviceEnrollment, context =>
            {
                var limits = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"enroll:{context.Connection.RemoteIpAddress}",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = limits.DeviceEnrollmentPermitLimitPerMinute, Window = TimeSpan.FromMinutes(1) });
            });

            // Per credential: the limiter runs before endpoint authentication, so the partition is a hash of the
            // presented credential (never the credential itself). Invalid keys are bounded by the global per-IP limit.
            options.AddPolicy(RateLimitingPolicies.InventoryReports, context =>
            {
                var limits = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                var credential = context.Request.Headers.Authorization.ToString();
                var partition = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential)), 0, 16);
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"device:{partition}",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = limits.InventoryReportsPermitLimitPerHour, Window = TimeSpan.FromHours(1) });
            });
        });

        services.AddHostedService<InventoryHistoryPurgeService>();
        return services;
    }
}
