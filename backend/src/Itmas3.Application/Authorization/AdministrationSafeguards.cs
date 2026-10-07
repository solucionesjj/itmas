using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Authorization;

namespace Itmas3.Application.Authorization;

/// <summary>
/// Anti-escalation rules for delegated (non-Root) administrators — SPEC-0001 RN-9..RN-12.
/// Violations raise <see cref="ForbiddenException"/> (HTTP 403, audited as denied).
/// </summary>
public sealed class AdministrationSafeguards(ICurrentUser currentUser, IAuthorizationSnapshotProvider snapshots)
{
    /// <summary>Fresh (uncached) snapshot of the acting administrator.</summary>
    public async Task<AuthorizationSnapshot> LoadActorAsync(CancellationToken cancellationToken)
    {
        var actor = currentUser.ApplicationUserId is { } id ? await snapshots.LoadAsync(id, cancellationToken) : null;
        return actor is { IsActive: true } ? actor : throw new ForbiddenException("Tu usuario no puede realizar operaciones de administración.");
    }

    /// <summary>RN-9: a non-Root administrator cannot change their own assignments or state.</summary>
    public static void EnsureNotSelf(AuthorizationSnapshot actor, Guid targetUserId)
    {
        if (!actor.IsRoot && actor.UserId == targetUserId)
        {
            throw new ForbiddenException("No puedes modificar tus propias políticas ni tu propio estado.");
        }
    }

    /// <summary>RN-12: only a Root can modify another Root.</summary>
    public static void EnsureTargetNotRoot(AuthorizationSnapshot actor, User target)
    {
        if (!actor.IsRoot && target.IsRoot)
        {
            throw new ForbiddenException("Solo un usuario Root puede modificar a otro usuario Root.");
        }
    }

    /// <summary>RN-10 / RN-11: a non-Root administrator cannot grant permissions they do not hold.</summary>
    public static void EnsureHoldsAll(AuthorizationSnapshot actor, IEnumerable<string> permissions)
    {
        if (actor.IsRoot)
        {
            return;
        }

        var missing = permissions.Where(permission => !actor.Grants.ContainsKey(permission)).Distinct().Order(StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            throw new ForbiddenException($"No puedes otorgar permisos que no tienes: {string.Join(", ", missing)}.");
        }
    }
}
