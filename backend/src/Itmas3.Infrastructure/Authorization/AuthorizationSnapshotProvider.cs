using Itmas3.Application.Authorization;
using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Infrastructure.Persistence;
using Itmas3.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Itmas3.Infrastructure.Authorization;

/// <summary>
/// Builds and caches <see cref="AuthorizationSnapshot"/>s keyed by (authorization version, user id). The version is
/// read once per request scope, so any committed change is visible on the next request on every replica (RNF-1/2).
/// </summary>
internal sealed class AuthorizationSnapshotProvider(AppDbContext db, IMemoryCache cache) : IAuthorizationSnapshotProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private long? _version;

    public async Task<AuthorizationSnapshot?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        _version ??= await db.AuthorizationState.AsNoTracking()
            .Where(state => state.Id == AuthorizationState.SingletonId)
            .Select(state => state.Version)
            .SingleAsync(cancellationToken);

        var key = $"authz:{_version}:{userId:N}";
        if (cache.TryGetValue(key, out AuthorizationSnapshot? cached))
        {
            return cached;
        }

        var snapshot = await LoadAsync(userId, cancellationToken);
        if (snapshot is not null)
        {
            cache.Set(key, snapshot, CacheDuration);
        }

        return snapshot;
    }

    public async Task<AuthorizationSnapshot?> LoadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new
            {
                candidate.IsActive,
                candidate.IsRoot,
                candidate.SecurityStamp,
                PolicyIds = candidate.PolicyAssignments.Select(assignment => assignment.PolicyId).ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        // Active policies → statements → grantable, non-deprecated permissions (RN-2, RN-8, RF-7).
        var grants = await db.Policies.AsNoTracking()
            .Where(policy => user.PolicyIds.Contains(policy.Id) && policy.IsActive)
            .SelectMany(policy => policy.Statements.Select(statement => new { statement.PermissionName, PolicyId = policy.Id, PolicyName = policy.Name }))
            .Where(grant => db.Permissions.Any(permission => permission.Id == grant.PermissionName && permission.IsGrantable && !permission.IsDeprecated))
            .ToListAsync(cancellationToken);

        var byPermission = grants
            .GroupBy(grant => grant.PermissionName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PolicyReference>)group.Select(grant => new PolicyReference(grant.PolicyId, grant.PolicyName))
                    .OrderBy(policy => policy.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList(),
                StringComparer.Ordinal);

        return new AuthorizationSnapshot(userId, user.IsActive, user.IsRoot, user.SecurityStamp, byPermission);
    }
}
