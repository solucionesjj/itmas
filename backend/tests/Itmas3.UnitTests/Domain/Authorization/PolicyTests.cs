using Itmas3.Domain.Authorization;
using Itmas3.Domain.Common;

namespace Itmas3.UnitTests.Domain.Authorization;

public sealed class PolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static PermissionDefinition Permission(string name, bool grantable = true) =>
        PermissionDefinition.Declare(name, name, string.Empty, "Pruebas", grantable);

    [Fact]
    public void Contains_each_permission_once()
    {
        var policy = Policy.Create("Clientes", string.Empty, [Permission("customers.read"), Permission("customers.read"), Permission("customers.update")], Now);

        Assert.Equal(["customers.read", "customers.update"], policy.PermissionNames.Order());
        Assert.Equal(1, policy.Revision);
    }

    [Fact]
    public void Rejects_non_grantable_and_deprecated_permissions()
    {
        var deprecated = Permission("legacy.read");
        deprecated.Deprecate();

        Assert.Throws<DomainException>(() => Policy.Create("A", string.Empty, [Permission("authorization.root.manage", grantable: false)], Now));
        Assert.Throws<DomainException>(() => Policy.Create("B", string.Empty, [deprecated], Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Requires_a_name(string name) =>
        Assert.Throws<DomainException>(() => Policy.Create(name, string.Empty, [], Now));

    [Fact]
    public void Updates_replace_permissions_and_increment_the_revision()
    {
        var policy = Policy.Create("Clientes", string.Empty, [Permission("customers.read"), Permission("customers.delete")], Now);

        policy.Update("Clientes", "Sin borrado", isActive: false, [Permission("customers.read"), Permission("customers.update")], Now);

        Assert.Equal(["customers.read", "customers.update"], policy.PermissionNames.Order());
        Assert.False(policy.IsActive);
        Assert.Equal(2, policy.Revision);
    }
}
