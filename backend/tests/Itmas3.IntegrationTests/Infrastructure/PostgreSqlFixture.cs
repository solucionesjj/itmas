using Itmas3.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Itmas3.IntegrationTests.Infrastructure.PostgreSqlFixture))]

namespace Itmas3.IntegrationTests.Infrastructure;

/// <summary>
/// Disposable PostgreSQL 18 initialised with the SAME bootstrap script used by Docker Compose
/// (database/init), migrated with the migrator role. Tests talk to it with the least-privilege runtime role.
/// Credentials are random per run.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public const string DatabaseName = "itmas3_test";
    public const string MigratorUser = "itmas3_migrator";
    public const string AppUser = "itmas3_app";

    public string MigratorPassword { get; } = Secret();

    public string AppPassword { get; } = Secret();

    private readonly PostgreSqlContainer _container;

    public PostgreSqlFixture()
    {
        _container = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase(DatabaseName)
            .WithPassword(Secret())
            .WithEnvironment("APP_DB_MIGRATOR_USER", MigratorUser)
            .WithEnvironment("APP_DB_MIGRATOR_PASSWORD", MigratorPassword)
            .WithEnvironment("APP_DB_USER", AppUser)
            .WithEnvironment("APP_DB_PASSWORD", AppPassword)
            .WithResourceMapping(new FileInfo(Path.Combine(RepositoryRoot(), "database", "init", "01-create-roles.sh")), "/docker-entrypoint-initdb.d/")
            .Build();
    }

    public string Host => _container.Hostname;

    public int Port => _container.GetMappedPublicPort(PostgreSqlBuilder.PostgreSqlPort);

    public string ConnectionStringFor(string username, string password) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = DatabaseName,
            Username = username,
            Password = password,
        }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>();
        DbContextConfigurator.Configure(options, ConnectionStringFor(MigratorUser, MigratorPassword));
        await using var context = new AppDbContext(options.Options);
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    private static string Secret() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "constitution.md")) || Directory.Exists(Path.Combine(directory.FullName, "database", "init")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (containing database/init) not found.");
    }
}
