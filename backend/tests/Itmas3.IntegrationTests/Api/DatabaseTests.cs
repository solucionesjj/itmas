using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Itmas3.IntegrationTests.Api;

public sealed class DatabaseTests(PostgreSqlFixture database) : ApiTestBase(database)
{
    [Fact]
    public async Task All_migrations_are_applied()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        DbContextConfigurator.Configure(options, Database.ConnectionStringFor(PostgreSqlFixture.MigratorUser, Database.MigratorPassword));
        await using var context = new AppDbContext(options.Options);

        Assert.Empty(await context.Database.GetPendingMigrationsAsync(Ct));
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync(Ct));
    }

    [Fact]
    public async Task Runtime_role_cannot_read_or_alter_the_migration_history()
    {
        await using var connection = new NpgsqlConnection(Database.ConnectionStringFor(PostgreSqlFixture.AppUser, Database.AppPassword));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand($"DELETE FROM {AppDbContext.MigrationsHistorySchema}.{AppDbContext.MigrationsHistoryTable}", connection);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    [Fact]
    public async Task Runtime_role_can_read_and_write_application_tables()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _ = await context.DataProtectionKeys.CountAsync(Ct);
    }

    [Fact]
    public async Task Runtime_role_cannot_change_the_schema()
    {
        await using var connection = new NpgsqlConnection(Database.ConnectionStringFor(PostgreSqlFixture.AppUser, Database.AppPassword));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("CREATE TABLE app.should_not_exist (id int)", connection);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }
}
