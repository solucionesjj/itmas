using Itmas3.Application.Authorization;

namespace Itmas3.UnitTests.Application;

public sealed class PermissionNameTests
{
    [Theory]
    [InlineData("users.read")]
    [InlineData("billing.invoices.approve")]
    [InlineData("user-management.roles.assign")]
    public void Accepts_module_resource_action_names(string permission)
    {
        Assert.True(PermissionName.IsValid(permission));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("users")]
    [InlineData("Users.Read")]
    [InlineData("users.*")]
    [InlineData(".users.read")]
    [InlineData("users..read")]
    [InlineData("users.read ")]
    public void Rejects_invalid_names(string? permission)
    {
        Assert.False(PermissionName.IsValid(permission));
        Assert.Throws<ArgumentException>(() => PermissionName.EnsureValid(permission));
    }
}
