namespace Itmas3.Application.Authorization;

public sealed record PolicyReference(Guid Id, string Name);

/// <summary>
/// Authorization state of a user at a given authorization version: active flag, Root flag, security stamp and the
/// effective permissions, each with the active policies that grant it (traceability, SPEC-0001 RF-5).
/// </summary>
public sealed record AuthorizationSnapshot(
    Guid UserId,
    bool IsActive,
    bool IsRoot,
    string SecurityStamp,
    IReadOnlyDictionary<string, IReadOnlyList<PolicyReference>> Grants)
{
    /// <summary>RN-1..RN-4: inactive → nothing; Root → everything; otherwise only explicitly granted permissions.</summary>
    public bool IsGranted(string permission) => IsActive && (IsRoot || Grants.ContainsKey(permission));

    public bool HoldsAll(IEnumerable<string> permissions) => IsRoot || permissions.All(Grants.ContainsKey);
}
