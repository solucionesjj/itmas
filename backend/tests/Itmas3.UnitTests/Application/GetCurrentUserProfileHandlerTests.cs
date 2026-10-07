using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Authorization;
using Itmas3.Application.Identity.GetCurrentUserProfile;
using Itmas3.UnitTests.Application.Authorization;

namespace Itmas3.UnitTests.Application;

public sealed class GetCurrentUserProfileHandlerTests
{
    private static readonly Guid AppUserId = Guid.NewGuid();

    private sealed record FakeUser(bool IsAuthenticated, string? DisplayName, string? Username) : ICurrentUser
    {
        public Guid? ApplicationUserId => IsAuthenticated ? AppUserId : null;

        public string? UserId => null;

        public string? TenantId => null;
    }

    [Fact]
    public async Task Returns_null_for_anonymous_callers()
    {
        var handler = new GetCurrentUserProfileHandler(new FakeUser(false, null, null), new FakeSnapshots());

        Assert.Null(await handler.HandleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Returns_profile_and_sorted_permissions_for_active_users()
    {
        var snapshots = new FakeSnapshots();
        snapshots.Set(new AuthorizationSnapshot(AppUserId, true, false, "stamp", Grants("users.read", "audit.read")));
        var handler = new GetCurrentUserProfileHandler(new FakeUser(true, "Ada Lovelace", "ada@example.com"), snapshots);

        var profile = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Ada Lovelace", profile?.DisplayName);
        Assert.Equal(["audit.read", "users.read"], profile?.Permissions);
        Assert.False(profile?.IsRoot);
    }

    [Fact]
    public async Task Returns_null_when_the_application_user_is_inactive()
    {
        var snapshots = new FakeSnapshots();
        snapshots.Set(new AuthorizationSnapshot(AppUserId, false, true, "stamp", Grants()));
        var handler = new GetCurrentUserProfileHandler(new FakeUser(true, "Ada", null), snapshots);

        Assert.Null(await handler.HandleAsync(TestContext.Current.CancellationToken));
    }

    private static Dictionary<string, IReadOnlyList<PolicyReference>> Grants(params string[] permissions) =>
        permissions.ToDictionary(permission => permission, _ => (IReadOnlyList<PolicyReference>)[new PolicyReference(Guid.NewGuid(), "P")]);
}
