namespace Itmas3.Domain.Authorization;

/// <summary>
/// Grants one permission inside a policy. Extension point (ADR-0008): resource patterns, conditions and Deny effects.
/// </summary>
public sealed class PolicyStatement
{
    internal PolicyStatement(string permissionName, PolicyEffect effect)
    {
        PermissionName = permissionName;
        Effect = effect;
    }

    public string PermissionName { get; private set; }

    public PolicyEffect Effect { get; private set; }
}
