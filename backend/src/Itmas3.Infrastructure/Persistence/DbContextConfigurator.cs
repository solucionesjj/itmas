using Microsoft.EntityFrameworkCore;

namespace Itmas3.Infrastructure.Persistence;

/// <summary>Single place where the EF Core provider is configured (runtime and design time).</summary>
internal static class DbContextConfigurator
{
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable(AppDbContext.MigrationsHistoryTable, AppDbContext.MigrationsHistorySchema);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            })
            .UseSnakeCaseNamingConvention();
}
