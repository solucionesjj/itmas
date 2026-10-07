using Itmas3.Application.Authorization;
using Itmas3.Application.Authorization.Users.ProvisionUser;

namespace Itmas3.UnitTests.Application.Authorization;

public sealed class ProvisionUserHandlerTests
{
    private const string BootstrapOid = "0f8fad5b-d9cb-469f-a165-70867728950e";

    private readonly InMemoryUsers _users = new();
    private readonly RecordingUnitOfWork _unitOfWork = new();

    private ProvisionUserHandler Handler(bool allowGuests = false) => new(
        _users,
        _unitOfWork,
        new AuthorizationSettings
        {
            BootstrapRootObjectIds = new HashSet<string>([BootstrapOid], StringComparer.OrdinalIgnoreCase),
            AllowGuestUsers = allowGuests,
        },
        TimeProvider.System);

    private static SignInIdentity Identity(string subject, bool isGuest = false) =>
        new("entra-id", "tenant", subject, "Ada Lovelace", "ada@example.com", isGuest);

    [Fact]
    public async Task RF01_first_sign_in_creates_an_active_user_without_privileges()
    {
        var result = await Handler().HandleAsync(Identity(Guid.NewGuid().ToString()), TestContext.Current.CancellationToken);

        var user = Assert.Single(_users.Items);
        Assert.True(result.Succeeded);
        Assert.Equal(user.Id, result.UserId);
        Assert.True(user.IsActive);
        Assert.False(user.IsRoot);
        Assert.Empty(user.PolicyAssignments);
        Assert.Equal(AuditActions.UserProvisioned, Assert.Single(_unitOfWork.Entries).Action);
    }

    [Fact]
    public async Task Later_sign_ins_reuse_the_user_and_are_not_audited()
    {
        var subject = Guid.NewGuid().ToString();
        await Handler().HandleAsync(Identity(subject), TestContext.Current.CancellationToken);
        _unitOfWork.Entries.Clear();

        await Handler().HandleAsync(Identity(subject), TestContext.Current.CancellationToken);

        Assert.Single(_users.Items);
        Assert.Empty(_unitOfWork.Entries);
    }

    [Fact]
    public async Task RF02_disabled_users_cannot_sign_in()
    {
        var subject = Guid.NewGuid().ToString();
        _users.AddUser(isActive: false, subject: subject);

        var result = await Handler().HandleAsync(Identity(subject), TestContext.Current.CancellationToken);

        Assert.Equal(SignInRejection.UserDisabled, result.Rejection);
    }

    [Fact]
    public async Task P2_guests_are_rejected_unless_allowed()
    {
        var rejected = await Handler().HandleAsync(Identity(Guid.NewGuid().ToString(), isGuest: true), TestContext.Current.CancellationToken);
        var allowed = await Handler(allowGuests: true).HandleAsync(Identity(Guid.NewGuid().ToString(), isGuest: true), TestContext.Current.CancellationToken);

        Assert.Equal(SignInRejection.GuestNotAllowed, rejected.Rejection);
        Assert.True(allowed.Succeeded);
    }

    [Fact]
    public async Task CA20_bootstrap_grants_root_only_while_no_active_root_exists()
    {
        await Handler().HandleAsync(Identity(BootstrapOid), TestContext.Current.CancellationToken);

        var user = Assert.Single(_users.Items);
        Assert.True(user.IsRoot);
        Assert.Contains(_unitOfWork.Entries, entry => entry.Action == AuditActions.RootBootstrapped);
    }

    [Fact]
    public async Task CA20_bootstrap_does_not_override_existing_roots()
    {
        _users.AddUser(isRoot: true);

        await Handler().HandleAsync(Identity(BootstrapOid), TestContext.Current.CancellationToken);

        Assert.False(_users.Items.Single(user => user.Identities.Any(identity => identity.Subject == BootstrapOid)).IsRoot);
    }
}
