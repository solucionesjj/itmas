using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Itmas3.Api.Security;
using Itmas3.Application.SqlAudit;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Itmas3.Api.SqlAudit;

/// <summary>Configuration section <c>SqlAudit</c> (SPEC-0005 §12.4). None of these values is a secret.</summary>
internal sealed class SqlAuditOptions
{
    public const string SectionName = "SqlAudit";

    /// <summary>Retention of the change log, the snapshot receipts and the elements removed from the engine (P-14).</summary>
    [Range(1, 3650)]
    public int ChangeLogRetentionDays { get; set; } = 365;

    /// <summary>Snapshots per hour per device credential: 50 connections plus their retries.</summary>
    [Range(1, 100_000)]
    public int SnapshotsPermitLimitPerHour { get; set; } = 120;
}

/// <summary>Composition of the SQL access audit module (SPEC-0005). Devices authenticate with the existing policy.</summary>
internal static class SqlAuditModuleSetup
{
    public static IServiceCollection AddSqlAuditModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SqlAuditOptions>()
            .Bind(configuration.GetSection(SqlAuditOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(serviceProvider => new SqlAuditSettings
        {
            ChangeLogRetentionDays = serviceProvider.GetRequiredService<IOptions<SqlAuditOptions>>().Value.ChangeLogRetentionDays,
        });

        services.AddRateLimiter(options =>
            // Per credential, as for inventories: a hash of the presented credential, never the credential itself.
            options.AddPolicy(RateLimitingPolicies.SqlAuditSnapshots, context =>
            {
                var limit = context.RequestServices.GetRequiredService<IOptions<SqlAuditOptions>>().Value.SnapshotsPermitLimitPerHour;
                var credential = context.Request.Headers.Authorization.ToString();
                var partition = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential)), 0, 16);
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"sql-audit:{partition}",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromHours(1) });
            }));

        services.AddHostedService<SqlAuditMaintenanceService>();
        return services;
    }
}
