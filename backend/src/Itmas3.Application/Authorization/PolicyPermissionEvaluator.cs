using Itmas3.Application.Abstractions.Authorization;
using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Authorization.Abstractions;

namespace Itmas3.Application.Authorization;

/// <summary>
/// authorize(user, permission) — SPEC-0001 §8.1: unknown or inactive user → deny; Root → allow;
/// otherwise allow only if an active policy assigned to the user grants the (grantable, non-deprecated) permission.
/// </summary>
internal sealed class PolicyPermissionEvaluator(IAuthorizationSnapshotProvider snapshots) : IPermissionEvaluator
{
    public async ValueTask<bool> IsGrantedAsync(ICurrentUser user, string permission, CancellationToken cancellationToken)
    {
        if (!user.IsAuthenticated || user.ApplicationUserId is not { } userId)
        {
            return false;
        }

        var snapshot = await snapshots.GetAsync(userId, cancellationToken);
        return snapshot?.IsGranted(permission) == true;
    }
}
