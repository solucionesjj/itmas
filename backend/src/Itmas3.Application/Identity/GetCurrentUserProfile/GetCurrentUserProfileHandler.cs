using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Authorization.Abstractions;

namespace Itmas3.Application.Identity.GetCurrentUserProfile;

/// <summary>
/// Use case: return the profile of the signed-in user, or <c>null</c> for anonymous callers and for sessions that
/// do not belong to an active application user.
/// </summary>
public sealed class GetCurrentUserProfileHandler(ICurrentUser currentUser, IAuthorizationSnapshotProvider snapshots)
{
    public async Task<CurrentUserProfile?> HandleAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.ApplicationUserId is not { } userId)
        {
            return null;
        }

        var snapshot = await snapshots.GetAsync(userId, cancellationToken);
        if (snapshot is not { IsActive: true })
        {
            return null;
        }

        var displayName = currentUser.DisplayName ?? currentUser.Username ?? "Usuario";
        var permissions = snapshot.IsRoot ? [] : snapshot.Grants.Keys.Order(StringComparer.Ordinal).ToList();
        return new CurrentUserProfile(displayName, currentUser.Username, snapshot.IsRoot, permissions);
    }
}
