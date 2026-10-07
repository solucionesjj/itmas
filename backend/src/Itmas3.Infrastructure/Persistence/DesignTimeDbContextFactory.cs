using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Itmas3.Infrastructure.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c> and by the migration bundle executed by the <c>migrator</c> container.
/// Reads the same <c>Database__*</c> environment variables as the API, so no connection string
/// (and therefore no password) is ever passed on the command line.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    // Creating migrations does not open a connection; this placeholder contains no credentials.
    private const string OfflineConnectionString = "Host=localhost;Database=design_time";

    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>();

        var connectionString = string.IsNullOrWhiteSpace(options?.Host)
            ? OfflineConnectionString
            : options.BuildConnectionString();

        var builder = new DbContextOptionsBuilder<AppDbContext>();
        DbContextConfigurator.Configure(builder, connectionString);
        return new AppDbContext(builder.Options);
    }
}
