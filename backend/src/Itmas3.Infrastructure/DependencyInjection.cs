using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Authorization.Queries;
using Itmas3.Application.Inventory.Abstractions;
using Itmas3.Application.Inventory.Queries;
using Itmas3.Application.SqlAudit.Abstractions;
using Itmas3.Application.SqlAudit.Queries;
using Itmas3.Infrastructure.Authorization;
using Itmas3.Infrastructure.HealthChecks;
using Itmas3.Infrastructure.Inventory;
using Itmas3.Infrastructure.Persistence;
using Itmas3.Infrastructure.SqlAudit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Itmas3.Infrastructure;

public static class DependencyInjection
{
    public const string DataProtectionApplicationName = "itmas3";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>((serviceProvider, builder) =>
            DbContextConfigurator.Configure(
                builder,
                serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.BuildConnectionString()));

        // Key ring persisted in PostgreSQL so authentication cookies survive restarts and scale-out.
        // Production hardening (key encryption at rest) is described in SECURITY.md.
        services.AddDataProtection()
            .SetApplicationName(DataProtectionApplicationName)
            .PersistKeysToDbContext<AppDbContext>();

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(name: "postgresql", tags: [HealthCheckTags.Ready]);

        // Authorization (SPEC-0001, ADR-0008)
        services.AddMemoryCache();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPolicyRepository, PolicyRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IAuthorizationUnitOfWork, AuthorizationUnitOfWork>();
        services.AddScoped<IAuthorizationSnapshotProvider, AuthorizationSnapshotProvider>();
        services.AddScoped<IAuthorizationQueries, AuthorizationQueries>();

        // Inventory (SPEC-0002, SPEC-0003)
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IInventoryReportRepository, InventoryReportRepository>();
        services.AddScoped<IInventoryUnitOfWork, InventoryUnitOfWork>();
        services.AddScoped<IInventoryQueries, InventoryQueries>();

        // SQL access audit (SPEC-0005)
        services.AddScoped<ISqlInstanceRepository, SqlInstanceRepository>();
        services.AddScoped<ISqlSnapshotReceiptRepository, SqlSnapshotReceiptRepository>();
        services.AddScoped<ISqlAccountRepository, SqlAccountRepository>();
        services.AddScoped<ISqlAccessItemStore, SqlAccessItemStore>();
        services.AddScoped<ISqlAccessChangeRepository, SqlAccessChangeRepository>();
        services.AddScoped<ISqlAuditUnitOfWork, SqlAuditUnitOfWork>();
        services.AddScoped<ISqlAuditQueries, SqlAuditQueries>();

        return services;
    }
}
