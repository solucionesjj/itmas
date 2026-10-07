using Itmas3.Domain.Authorization;

namespace Itmas3.UnitTests.Domain.Authorization;

public sealed class UserTests
{
    private static User NewUser() => User.Provision("entra-id", "tenant", "subject", "  Ada  ", "ada@example.com", DateTimeOffset.UtcNow);

    [Fact]
    public void New_users_are_active_without_root_or_policies()
    {
        var user = NewUser();

        Assert.Equal(("Ada", true, false), (user.DisplayName, user.IsActive, user.IsRoot));
        Assert.Empty(user.PolicyAssignments);
    }

    [Fact]
    public void Policy_assignment_is_idempotent()
    {
        var user = NewUser();
        var policyId = Guid.NewGuid();

        Assert.True(user.AssignPolicy(policyId, null, DateTimeOffset.UtcNow));
        Assert.False(user.AssignPolicy(policyId, null, DateTimeOffset.UtcNow));
        Assert.True(user.RevokePolicy(policyId));
        Assert.False(user.RevokePolicy(policyId));
    }

    [Fact]
    public void Deactivation_invalidates_sessions_but_root_revocation_does_not()
    {
        var user = NewUser();
        user.SetRoot(true);
        var stamp = user.SecurityStamp;

        user.SetRoot(false);
        Assert.Equal(stamp, user.SecurityStamp);

        user.SetActive(false);
        Assert.NotEqual(stamp, user.SecurityStamp);
    }
}
