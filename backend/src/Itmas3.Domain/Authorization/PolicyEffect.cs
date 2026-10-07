namespace Itmas3.Domain.Authorization;

/// <summary>Effect of a policy statement. Only <see cref="Allow"/> exists in v1; Deny is a planned extension.</summary>
public enum PolicyEffect
{
    Allow = 0,
}
