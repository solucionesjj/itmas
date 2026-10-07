using Itmas3.Application.Authorization;
using Itmas3.Application.Common.Exceptions;

namespace Itmas3.UnitTests.Application.Authorization;

public sealed class AdministrationSafeguardsTests
{
    private readonly InMemoryUsers _users = new();

    [Fact]
    public void RN09_non_root_administrators_cannot_target_themselves()
    {
        var admin = _users.AddUser();

        Assert.Throws<ForbiddenException>(() => AdministrationSafeguards.EnsureNotSelf(FakeSnapshots.For(admin), admin.Id));
    }

    [Fact]
    public void RN09_root_may_target_itself()
    {
        var root = _users.AddUser(isRoot: true);

        AdministrationSafeguards.EnsureNotSelf(FakeSnapshots.For(root), root.Id);
    }

    [Fact]
    public void RN10_RN11_cannot_grant_permissions_not_held()
    {
        var admin = _users.AddUser();
        var actor = FakeSnapshots.For(admin, "users.read");

        AdministrationSafeguards.EnsureHoldsAll(actor, ["users.read"]);
        var exception = Assert.Throws<ForbiddenException>(() => AdministrationSafeguards.EnsureHoldsAll(actor, ["users.read", "audit.read"]));
        Assert.Contains("audit.read", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RN12_non_root_cannot_modify_a_root()
    {
        var admin = _users.AddUser();
        var root = _users.AddUser(isRoot: true);

        Assert.Throws<ForbiddenException>(() => AdministrationSafeguards.EnsureTargetNotRoot(FakeSnapshots.For(admin), root));
        AdministrationSafeguards.EnsureTargetNotRoot(FakeSnapshots.For(_users.AddUser(isRoot: true)), root);
    }

    [Fact]
    public async Task Inactive_or_unknown_actors_cannot_administer()
    {
        var snapshots = new FakeSnapshots();
        var inactive = _users.AddUser(isActive: false);
        snapshots.Set(FakeSnapshots.For(inactive, "users.update"));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new AdministrationSafeguards(new FakeCurrentUser(inactive.Id), snapshots).LoadActorAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new AdministrationSafeguards(new FakeCurrentUser(null), snapshots).LoadActorAsync(TestContext.Current.CancellationToken));
    }
}
