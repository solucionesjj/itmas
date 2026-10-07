using Itmas3.Api.Authorization;

namespace Itmas3.UnitTests.Api;

public sealed class RequirePermissionAttributeTests
{
    [Fact]
    public void Produces_a_permission_requirement()
    {
        var requirement = Assert.Single(new RequirePermissionAttribute("users.read").GetRequirements());

        Assert.Equal(new PermissionRequirement("users.read"), requirement);
    }

    [Fact]
    public void Rejects_malformed_permission_names()
    {
        Assert.Throws<ArgumentException>(() => new RequirePermissionAttribute("Users"));
    }
}
