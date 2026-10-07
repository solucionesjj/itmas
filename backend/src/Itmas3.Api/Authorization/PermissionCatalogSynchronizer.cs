using Itmas3.Application.Authorization.Permissions.SynchronizeCatalog;

namespace Itmas3.Api.Authorization;

/// <summary>Synchronises the permission catalog declared in code with the database at start-up (SPEC-0001 RF-6).</summary>
internal sealed partial class PermissionCatalogSynchronizer(IServiceScopeFactory scopeFactory, ILogger<PermissionCatalogSynchronizer> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var changes = await scope.ServiceProvider.GetRequiredService<SynchronizePermissionCatalogHandler>().HandleAsync(cancellationToken);
        LogSynchronized(logger, changes);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Permission catalog synchronised ({Changes} changes)")]
    private static partial void LogSynchronized(ILogger logger, int changes);
}
