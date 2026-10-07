using Itmas3.Application.Authorization;

namespace Itmas3.UnitTests.Application.Authorization;

/// <summary>authorize(user, permission) rules — SPEC-0001 RN-1..RN-4.</summary>
public sealed class AuthorizationSnapshotTests
{
    private static AuthorizationSnapshot Snapshot(bool isActive, bool isRoot, params string[] permissions) =>
        new(Guid.NewGuid(), isActive, isRoot, "stamp",
            permissions.ToDictionary(permission => permission, _ => (IReadOnlyList<PolicyReference>)[new PolicyReference(Guid.NewGuid(), "P")]));

    [Fact]
    public void Grants_only_explicit_permissions() =>
        Assert.Equal((true, false), (Snapshot(true, false, "customers.read").IsGranted("customers.read"), Snapshot(true, false, "customers.read").IsGranted("customers.delete")));

    [Fact]
    public void Root_is_granted_everything() =>
        Assert.True(Snapshot(true, true).IsGranted("anything.at.all"));

    [Fact]
    public void Inactive_users_are_granted_nothing_even_if_root() =>
        Assert.False(Snapshot(false, true, "customers.read").IsGranted("customers.read"));
}
