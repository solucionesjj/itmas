using Itmas3.Domain.Authorization;

namespace Itmas3.Application.Authorization.Policies;

internal static class PolicyAuditView
{
    public static object Of(Policy policy) => new
    {
        policy.Name,
        policy.Description,
        policy.IsActive,
        Permissions = policy.PermissionNames.Order(StringComparer.Ordinal).ToArray(),
    };
}
