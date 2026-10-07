using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Domain.Authorization;
using Itmas3.Domain.Common;

namespace Itmas3.Application.Authorization.Policies;

internal static class PermissionResolver
{
    /// <summary>Loads the requested permissions; unknown names are a business-rule violation (CA-3).</summary>
    public static async Task<IReadOnlyList<PermissionDefinition>> ResolveAsync(
        this IPermissionRepository permissions, IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
        var distinct = names.Select(name => name.Trim()).Distinct(StringComparer.Ordinal).ToList();
        var found = await permissions.GetByNamesAsync(distinct, cancellationToken);
        var missing = distinct.Except(found.Select(permission => permission.Name), StringComparer.Ordinal).ToList();

        return missing.Count == 0
            ? found
            : throw new DomainException($"Permisos inexistentes: {string.Join(", ", missing)}.");
    }
}
