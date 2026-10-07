using System.Net;
using System.Net.Http.Json;
using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Authorization.Permissions.SynchronizeCatalog;
using Itmas3.Domain.Authorization;
using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itmas3.IntegrationTests.Api;

[CollectionDefinition(nameof(PermissionCatalogCollection), DisableParallelization = true)]
public sealed class PermissionCatalogCollection;

/// <summary>
/// Runs alone: every API start-up synchronises the catalog, which would deprecate the test permission
/// while other test classes start in parallel.
/// </summary>
[Collection(nameof(PermissionCatalogCollection))]
public sealed class PermissionCatalogTests(PostgreSqlFixture database) : ApiTestBase(database)
{
    private static readonly Uri Policies = new("/api/v1/policies", UriKind.Relative);

    [Fact]
    public async Task CA22_deprecated_permissions_stop_granting_access()
    {
        const string legacy = "legacy.items.read";
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existing = await db.Permissions.SingleOrDefaultAsync(permission => permission.Id == legacy, Ct);
            if (existing is null)
            {
                db.Permissions.Add(PermissionDefinition.Declare(legacy, "Legado", string.Empty, "Pruebas", isGrantable: true));
            }
            else
            {
                existing.Synchronise(isGrantable: true);
            }

            await db.SaveChangesAsync(Ct);
        }

        var user = await Factory.CreateUserAsync(permissions: legacy);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var snapshots = scope.ServiceProvider.GetRequiredService<IAuthorizationSnapshotProvider>();
            Assert.True((await snapshots.LoadAsync(user.Id, Ct))!.IsGranted(legacy));

            // Not declared in code → the catalog synchronisation deprecates it.
            await scope.ServiceProvider.GetRequiredService<SynchronizePermissionCatalogHandler>().HandleAsync(Ct);
            Assert.False((await snapshots.LoadAsync(user.Id, Ct))!.IsGranted(legacy));
        }

        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);
        var response = await root.PostAsJsonAsync(Policies, new { name = $"Legado {Guid.NewGuid():N}", permissions = new[] { legacy } }, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

}
