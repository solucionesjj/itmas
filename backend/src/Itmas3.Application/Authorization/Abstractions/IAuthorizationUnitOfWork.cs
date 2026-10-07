namespace Itmas3.Application.Authorization.Abstractions;

/// <summary>Who performed an audited operation. <see cref="UserId"/> is null for the system.</summary>
public sealed record AuditActor(Guid? UserId, string DisplayName)
{
    public static AuditActor System { get; } = new(null, "Sistema");
}

/// <summary>Describes the operation being attempted; used to audit it if it is rejected.</summary>
public sealed record AuditContext(string Action, string ResourceType, string? ResourceId);

/// <summary>Audit entries produced by a successful administrative operation.</summary>
public sealed class AuditTrail
{
    private readonly List<AuditEntry> _entries = [];

    /// <summary>Overrides the actor taken from the current user (e.g. during sign-in provisioning).</summary>
    public AuditActor? Actor { get; set; }

    public IReadOnlyList<AuditEntry> Entries => _entries;

    public void Record(string action, string resourceType, string? resourceId, object? before, object? after) =>
        _entries.Add(new AuditEntry(action, resourceType, resourceId, before, after));
}

public sealed record AuditEntry(string Action, string ResourceType, string? ResourceId, object? Before, object? After);

/// <summary>
/// Executes an authorization-administration operation atomically: serialises it with every other authorization change,
/// persists it, writes its audit entries and invalidates cached authorization snapshots. Operations rejected by
/// business rules, safeguards or conflicts are rolled back and audited as rejected/denied, then rethrown.
/// </summary>
public interface IAuthorizationUnitOfWork
{
    Task<T> ExecuteAsync<T>(AuditContext context, Func<AuditTrail, CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}
