using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Authorization;
using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Domain.Authorization;

namespace Itmas3.UnitTests.Application.Authorization;

internal sealed class InMemoryUsers : IUserRepository
{
    public List<User> Items { get; } = [];

    public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(user => user.Id == id));

    public Task<User?> FindByExternalIdentityAsync(string provider, string issuer, string subject, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(user =>
            user.Identities.Any(identity => identity.Provider == provider && identity.Issuer == issuer && identity.Subject == subject)));

    public Task<int> CountActiveRootsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count(user => user.IsRoot && user.IsActive));

    public void Add(User user) => Items.Add(user);

    public User AddUser(bool isRoot = false, bool isActive = true, string? subject = null)
    {
        var user = User.Provision("entra-id", "tenant", subject ?? Guid.NewGuid().ToString(), "Usuario", null, DateTimeOffset.UtcNow);
        user.SetRoot(isRoot);
        user.SetActive(isActive);
        Items.Add(user);
        return user;
    }
}

/// <summary>Runs the work directly and keeps the audit entries it produced.</summary>
internal sealed class RecordingUnitOfWork : IAuthorizationUnitOfWork
{
    public List<AuditEntry> Entries { get; } = [];

    public async Task<T> ExecuteAsync<T>(AuditContext context, Func<AuditTrail, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        var trail = new AuditTrail();
        var result = await work(trail, cancellationToken);
        Entries.AddRange(trail.Entries);
        return result;
    }
}

internal sealed class FakeSnapshots : IAuthorizationSnapshotProvider
{
    private readonly Dictionary<Guid, AuthorizationSnapshot> _snapshots = [];

    public void Set(AuthorizationSnapshot snapshot) => _snapshots[snapshot.UserId] = snapshot;

    public Task<AuthorizationSnapshot?> GetAsync(Guid userId, CancellationToken cancellationToken) => LoadAsync(userId, cancellationToken);

    public Task<AuthorizationSnapshot?> LoadAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_snapshots.GetValueOrDefault(userId));

    public static AuthorizationSnapshot For(User user, params string[] permissions) =>
        new(user.Id, user.IsActive, user.IsRoot, user.SecurityStamp,
            permissions.ToDictionary(permission => permission, _ => (IReadOnlyList<PolicyReference>)[new PolicyReference(Guid.NewGuid(), "P")]));
}

internal sealed class FakeCurrentUser(Guid? applicationUserId) : ICurrentUser
{
    public bool IsAuthenticated => applicationUserId is not null;

    public Guid? ApplicationUserId => applicationUserId;

    public string? UserId => applicationUserId?.ToString();

    public string? TenantId => "tenant";

    public string? DisplayName => "Administrador";

    public string? Username => "admin@example.com";
}
