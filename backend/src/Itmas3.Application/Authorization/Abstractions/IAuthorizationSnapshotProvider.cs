namespace Itmas3.Application.Authorization.Abstractions;

public interface IAuthorizationSnapshotProvider
{
    /// <summary>Snapshot for the current authorization version (cached). Null when the user does not exist.</summary>
    Task<AuthorizationSnapshot?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Snapshot read straight from the database, bypassing the cache (used inside administrative transactions).</summary>
    Task<AuthorizationSnapshot?> LoadAsync(Guid userId, CancellationToken cancellationToken);
}
