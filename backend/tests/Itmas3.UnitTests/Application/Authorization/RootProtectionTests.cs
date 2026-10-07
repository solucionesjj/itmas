using Itmas3.Application.Authorization;
using Itmas3.Application.Authorization.Users.SetRootStatus;
using Itmas3.Application.Authorization.Users.SetUserStatus;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Common;

namespace Itmas3.UnitTests.Application.Authorization;

/// <summary>CA-19 / RF-19: the system can never be left without an active Root.</summary>
public sealed class RootProtectionTests
{
    private readonly InMemoryUsers _users = new();
    private readonly FakeSnapshots _snapshots = new();
    private readonly RecordingUnitOfWork _unitOfWork = new();

    private AdministrationSafeguards SafeguardsFor(Guid actorId) => new(new FakeCurrentUser(actorId), _snapshots);

    [Fact]
    public async Task The_last_active_root_cannot_revoke_its_own_root_status()
    {
        var root = _users.AddUser(isRoot: true);
        _snapshots.Set(FakeSnapshots.For(root));
        var handler = new SetRootStatusHandler(_users, SafeguardsFor(root.Id), _unitOfWork);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(new SetRootStatusCommand(root.Id, false), TestContext.Current.CancellationToken));
        Assert.True(root.IsRoot);
    }

    [Fact]
    public async Task The_last_active_root_cannot_be_deactivated()
    {
        var root = _users.AddUser(isRoot: true);
        _snapshots.Set(FakeSnapshots.For(root));
        var handler = new SetUserStatusHandler(_users, SafeguardsFor(root.Id), _unitOfWork);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(new SetUserStatusCommand(root.Id, false), TestContext.Current.CancellationToken));
        Assert.True(root.IsActive);
    }

    [Fact]
    public async Task A_root_can_step_down_when_another_active_root_exists()
    {
        var root = _users.AddUser(isRoot: true);
        _users.AddUser(isRoot: true);
        _snapshots.Set(FakeSnapshots.For(root));
        var handler = new SetRootStatusHandler(_users, SafeguardsFor(root.Id), _unitOfWork);

        await handler.HandleAsync(new SetRootStatusCommand(root.Id, false), TestContext.Current.CancellationToken);

        Assert.False(root.IsRoot);
        Assert.Equal(AuditActions.RootRevoked, Assert.Single(_unitOfWork.Entries).Action);
    }

    [Fact]
    public async Task Only_root_users_can_manage_root()
    {
        var admin = _users.AddUser();
        var target = _users.AddUser();
        _snapshots.Set(FakeSnapshots.For(admin, AuthorizationPermissions.UsersUpdate));
        var handler = new SetRootStatusHandler(_users, SafeguardsFor(admin.Id), _unitOfWork);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.HandleAsync(new SetRootStatusCommand(target.Id, true), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Deactivation_rotates_the_security_stamp()
    {
        var root = _users.AddUser(isRoot: true);
        var user = _users.AddUser();
        var stamp = user.SecurityStamp;
        _snapshots.Set(FakeSnapshots.For(root));

        await new SetUserStatusHandler(_users, SafeguardsFor(root.Id), _unitOfWork)
            .HandleAsync(new SetUserStatusCommand(user.Id, false), TestContext.Current.CancellationToken);

        Assert.False(user.IsActive);
        Assert.NotEqual(stamp, user.SecurityStamp);
    }
}
