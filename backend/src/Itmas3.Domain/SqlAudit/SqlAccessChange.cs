using Itmas3.Domain.Common;

namespace Itmas3.Domain.SqlAudit;

/// <summary>
/// Entry of the change log of an instance (RF-11): an account or element that was added, removed or changed between two
/// snapshots. Kept for the retention period (P-14) with a description that survives the purge of the element.
/// </summary>
public sealed class SqlAccessChange : Entity<Guid>
{
    private SqlAccessChange(
        Guid id, Guid instanceId, Guid accountId, Guid? itemId, SqlChangeType changeType, SqlChangeTarget target, DateTimeOffset detectedAt, SqlChangeDetails details)
        : base(id)
    {
        InstanceId = instanceId;
        AccountId = accountId;
        ItemId = itemId;
        ChangeType = changeType;
        Target = target;
        DetectedAt = detectedAt;
        Details = details;
    }

    public Guid InstanceId { get; private set; }

    public Guid AccountId { get; private set; }

    public Guid? ItemId { get; private set; }

    public SqlChangeType ChangeType { get; private set; }

    public SqlChangeTarget Target { get; private set; }

    public DateTimeOffset DetectedAt { get; private set; }

    public SqlChangeDetails Details { get; private set; }

    public static SqlAccessChange ForAccount(SqlAccount account, SqlChangeType changeType, IReadOnlyList<SqlFieldChange> changes, DateTimeOffset detectedAt)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(changes);
        return new SqlAccessChange(
            Guid.CreateVersion7(detectedAt), account.InstanceId, account.Id, null, changeType, SqlChangeTarget.Account, detectedAt, new SqlChangeDetails(null, changes));
    }

    public static SqlAccessChange ForItem(
        Guid instanceId,
        Guid accountId,
        Guid itemId,
        SqlItemDescription item,
        SqlChangeType changeType,
        IReadOnlyList<SqlFieldChange> changes,
        DateTimeOffset detectedAt)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(changes);
        var target = item.Kind == SqlAccessItemKind.RoleMembership ? SqlChangeTarget.RoleMembership : SqlChangeTarget.Permission;
        return new SqlAccessChange(
            Guid.CreateVersion7(detectedAt), instanceId, accountId, itemId, changeType, target, detectedAt, new SqlChangeDetails(item, changes));
    }
}

/// <summary>
/// Receipt of a snapshot (RF-10, RF-12): makes retries idempotent per device and records how long processing took
/// (RNF-3). <see cref="Applied"/> is false for a snapshot older than the current state.
/// </summary>
public sealed class SqlSnapshotReceipt : Entity<Guid>
{
    private SqlSnapshotReceipt(Guid id, Guid deviceId, Guid instanceId, Guid submissionId, DateTimeOffset collectedAt, DateTimeOffset receivedAt)
        : base(id)
    {
        DeviceId = deviceId;
        InstanceId = instanceId;
        SubmissionId = submissionId;
        CollectedAt = collectedAt;
        ReceivedAt = receivedAt;
    }

    public Guid DeviceId { get; private set; }

    public Guid InstanceId { get; private set; }

    public Guid SubmissionId { get; private set; }

    public DateTimeOffset CollectedAt { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public string? ConnectionIp { get; private set; }

    public int ProcessingMs { get; private set; }

    public bool Applied { get; private set; }

    public static SqlSnapshotReceipt Record(
        Guid deviceId,
        Guid instanceId,
        Guid submissionId,
        DateTimeOffset collectedAt,
        DateTimeOffset receivedAt,
        string? connectionIp,
        bool applied,
        TimeSpan processing)
    {
        if (submissionId == Guid.Empty)
        {
            throw new DomainException("La instantánea no tiene identificador de envío.");
        }

        return new SqlSnapshotReceipt(Guid.CreateVersion7(receivedAt), deviceId, instanceId, submissionId, collectedAt, receivedAt)
        {
            ConnectionIp = connectionIp,
            Applied = applied,
            ProcessingMs = (int)Math.Clamp(processing.TotalMilliseconds, 0, int.MaxValue),
        };
    }
}
