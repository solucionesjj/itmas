using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Domain.SqlAudit;

namespace Itmas3.Application.SqlAudit.Abstractions;

public interface ISqlInstanceRepository
{
    Task<SqlInstance?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<SqlInstance?> FindAsync(Guid deviceId, SqlInstanceKey key, CancellationToken cancellationToken);

    /// <summary>Ids of the instances whose risks were calculated with an older catalog (RF-15).</summary>
    Task<IReadOnlyList<Guid>> ListWithRiskCatalogBeforeAsync(int catalogVersion, CancellationToken cancellationToken);

    void Add(SqlInstance instance);
}

public interface ISqlSnapshotReceiptRepository
{
    Task<bool> ExistsAsync(Guid deviceId, Guid submissionId, CancellationToken cancellationToken);

    void Add(SqlSnapshotReceipt receipt);

    Task<int> DeleteReceivedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}

public interface ISqlAccountRepository
{
    Task<SqlAccount?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Every account of the instance, removed ones included, tracked for changes.</summary>
    Task<IReadOnlyList<SqlAccount>> ListByInstanceAsync(Guid instanceId, CancellationToken cancellationToken);

    void AddRange(IEnumerable<SqlAccount> accounts);

    /// <summary>Deletes accounts removed from the engine before <paramref name="cutoff"/> (their elements are purged first).</summary>
    Task<int> DeleteRemovedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}

/// <summary>
/// Elements of an instance. An instance may hold up to 200 000 (RNF-5), so the snapshot processing reads a compact state
/// and applies set-based updates instead of tracking every element (RNF-2).
/// </summary>
public interface ISqlAccessItemStore
{
    Task<SqlAccessItem?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<SqlAccessItemState>> ListStatesAsync(Guid instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts new elements in bulk. Changes recorded so far (e.g. new accounts they belong to) are saved first; the
    /// unit of work commits everything together.
    /// </summary>
    Task InsertAsync(IReadOnlyCollection<SqlAccessItem> items, CancellationToken cancellationToken);

    Task MarkRemovedAsync(IReadOnlyCollection<Guid> ids, DateTimeOffset removedAt, CancellationToken cancellationToken);

    /// <summary>Elements that appear again after being removed: new elements, pending validation (RF-17).</summary>
    Task RestoreAsync(IReadOnlyCollection<Guid> ids, DateTimeOffset seenAt, CancellationToken cancellationToken);

    Task SetRiskAsync(IReadOnlyCollection<Guid> ids, RiskLevel risk, CancellationToken cancellationToken);

    Task SetGrantorAsync(IReadOnlyCollection<Guid> ids, string? grantor, CancellationToken cancellationToken);

    /// <summary>Updates the last-seen date of the current elements of the sections that were read.</summary>
    Task TouchAsync(Guid instanceId, IReadOnlyList<SqlUnavailablePart> unavailable, DateTimeOffset seenAt, CancellationToken cancellationToken);

    Task<int> DeleteRemovedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}

/// <summary>Compact state of a stored element used to compare snapshots.</summary>
public sealed record SqlAccessItemState(
    Guid Id,
    Guid AccountId,
    string IdentityHash,
    SqlAccessItemKind Kind,
    Guid? RoleAccountId,
    SqlSecurableClass? SecurableClass,
    string? DatabaseName,
    string? SchemaName,
    string? ObjectName,
    string? ObjectType,
    string? ColumnName,
    string? Permission,
    SqlPermissionState? State,
    string? Grantor,
    RiskLevel Risk,
    DateTimeOffset? RemovedAt)
{
    public bool IsCurrent => RemovedAt is null;

    public SqlItemDescription Describe(string? roleName) => new(
        Kind,
        SecurableClass is { } securableClass ? new SqlSecurable(securableClass, DatabaseName, SchemaName, ObjectName, ObjectType, ColumnName) : null,
        Kind == SqlAccessItemKind.RoleMembership ? roleName : null,
        Permission,
        State);
}

public interface ISqlAccessChangeRepository
{
    void AddRange(IEnumerable<SqlAccessChange> changes);

    Task<int> DeleteDetectedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}

/// <summary>
/// Executes a SQL audit operation atomically: persists the changes, the set-based updates and the audit entries
/// recorded in the trail (shared audit log of SPEC-0001) in a single transaction. The actor is the current user, or the
/// system for snapshots sent by devices.
/// </summary>
public interface ISqlAuditUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<AuditTrail, CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}
