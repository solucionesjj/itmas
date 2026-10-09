using Itmas3.Domain.Authorization;
using Itmas3.Domain.Inventory;
using Itmas3.Infrastructure.Persistence.Records;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Itmas3.Infrastructure.Persistence;

/// <summary>
/// Single EF Core unit of work for the application. All tables live in the <see cref="Schema"/> schema,
/// which is owned by the migrator role; the runtime role only has DML privileges. The migration history
/// lives in <see cref="MigrationsHistorySchema"/>, which the runtime role cannot access.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public const string Schema = "app";
    public const string MigrationsHistorySchema = "app_migrations";
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>
    /// Technical (infrastructure) table: ASP.NET Core Data Protection key ring. Required so that
    /// session cookies remain valid across restarts and replicas. Not a business entity.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // Authorization (SPEC-0001)
    public DbSet<User> Users => Set<User>();

    public DbSet<Policy> Policies => Set<Policy>();

    public DbSet<PermissionDefinition> Permissions => Set<PermissionDefinition>();

    public DbSet<AuditEventRecord> AuditEvents => Set<AuditEventRecord>();

    public DbSet<AuthorizationState> AuthorizationState => Set<AuthorizationState>();

    // Inventory (SPEC-0002)
    public DbSet<Device> Devices => Set<Device>();

    public DbSet<InventoryReport> InventoryReports => Set<InventoryReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
