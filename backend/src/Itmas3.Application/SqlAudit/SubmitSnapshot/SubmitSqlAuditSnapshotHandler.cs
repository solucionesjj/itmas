using System.Diagnostics.Metrics;
using Itmas3.Application.SqlAudit.Abstractions;
using Itmas3.Domain.Common;
using Itmas3.Domain.SqlAudit;

namespace Itmas3.Application.SqlAudit.SubmitSnapshot;

public sealed record SqlSnapshotDatabase(string Name, string State, string? OwnerRef);

public sealed record SqlSnapshotAccount(
    string Ref, SqlAccountLevel Level, string? Database, string Name, string? Host, string Type, SqlAccountAttributes Attributes, string? LoginRef);

public sealed record SqlSnapshotMembership(string MemberRef, string RoleRef);

public sealed record SqlSnapshotPermission(string GranteeRef, SqlSecurable Securable, string Permission, SqlPermissionState State, string? GrantorRef);

/// <summary>
/// A validated snapshot of one connection (contract <c>sql-audit v1</c>). References (<c>ref</c>) are resolved here; the
/// API has already checked that they exist. <see cref="Instance"/> is null when the connection itself failed.
/// </summary>
public sealed record SubmitSqlAuditSnapshotCommand(
    Guid DeviceId,
    Guid SubmissionId,
    DateTimeOffset CollectedAt,
    string? ConnectionIp,
    string Alias,
    SqlInstanceKey Key,
    SqlTlsMode Tls,
    SqlInstanceMetadata? Instance,
    IReadOnlyList<SqlSnapshotDatabase> Databases,
    IReadOnlyList<SqlSnapshotAccount> Accounts,
    IReadOnlyList<SqlSnapshotMembership> Memberships,
    IReadOnlyList<SqlSnapshotPermission> Permissions,
    int OmittedBuiltinPermissions,
    IReadOnlyList<SqlUnavailablePart> Unavailable);

/// <summary>Outcome of a submission: whether it was processed now and whether it changed the current state.</summary>
public sealed record SqlSnapshotOutcome(bool Duplicate, bool Applied, Guid? InstanceId);

/// <summary>
/// Use case: process a snapshot in the request, in one transaction (SPEC-0005 RF-9..RF-13, §10.4): register the instance,
/// compute the risk of every element, compare with the current state (new, changed and removed elements) and write the
/// change log. Repeated submissions are ignored (RF-10) and older snapshots are only recorded (RF-12). The first
/// snapshot of an instance is its baseline: its elements are pending but not logged as additions.
/// </summary>
public sealed class SubmitSqlAuditSnapshotHandler(
    ISqlInstanceRepository instances,
    ISqlSnapshotReceiptRepository receipts,
    ISqlAccountRepository accounts,
    ISqlAccessItemStore items,
    ISqlAccessChangeRepository changes,
    ISqlAuditUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>Clock skew tolerated between the device and the server.</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    public async Task<SqlSnapshotOutcome> HandleAsync(SubmitSqlAuditSnapshotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var receivedAt = timeProvider.GetUtcNow();
        if (command.CollectedAt > receivedAt + MaxClockSkew)
        {
            throw new DomainException("La fecha de recolección de la instantánea no puede ser futura.");
        }

        var started = timeProvider.GetTimestamp();
        var outcome = await unitOfWork.ExecuteAsync(async (_, ct) =>
        {
            // RF-10: a retry of a processed submission changes nothing.
            if (await receipts.ExistsAsync(command.DeviceId, command.SubmissionId, ct))
            {
                return new SqlSnapshotOutcome(true, false, null);
            }

            // RN-1: the device comes from the authenticated credential, never from the payload.
            var instance = await instances.FindAsync(command.DeviceId, command.Key, ct);
            if (instance is null)
            {
                instance = SqlInstance.Register(command.DeviceId, command.Key, receivedAt);
                instance.MarkRiskCalculated(SqlRiskCatalog.Version);
                instances.Add(instance);
            }

            var applied = instance.Accepts(command.CollectedAt);
            if (applied)
            {
                await ApplyAsync(instance, command, receivedAt, ct);
            }
            else
            {
                instance.RecordLateSnapshot(receivedAt);
            }

            receipts.Add(SqlSnapshotReceipt.Record(
                command.DeviceId,
                instance.Id,
                command.SubmissionId,
                command.CollectedAt.ToUniversalTime(),
                receivedAt,
                command.ConnectionIp,
                applied,
                timeProvider.GetElapsedTime(started)));
            return new SqlSnapshotOutcome(false, applied, instance.Id);
        }, cancellationToken);

        if (!outcome.Duplicate)
        {
            SqlAuditMetrics.RecordProcessing(timeProvider.GetElapsedTime(started), outcome.Applied);
        }

        return outcome;
    }

    private async Task ApplyAsync(SqlInstance instance, SubmitSqlAuditSnapshotCommand command, DateTimeOffset receivedAt, CancellationToken ct)
    {
        var seenAt = command.CollectedAt.ToUniversalTime();
        var baseline = instance.LastCollectedAt is null;
        var log = new List<SqlAccessChange>();

        var byRef = command.Accounts.ToDictionary(account => account.Ref, StringComparer.Ordinal);
        string? NameOf(string? reference) => reference is not null && byRef.TryGetValue(reference, out var account) ? account.Name : null;

        instance.ApplySnapshot(
            command.Alias,
            command.Tls,
            command.Instance,
            command.Databases.Select(database => new SqlDatabaseInfo(database.Name, database.State, NameOf(database.OwnerRef))).ToList(),
            command.Unavailable,
            command.OmittedBuiltinPermissions,
            seenAt,
            receivedAt);

        // Accounts (RN-2, RN-4)
        var stored = await accounts.ListByInstanceAsync(instance.Id, ct);
        var storedByIdentity = stored.ToDictionary(account => account.IdentityHash, StringComparer.Ordinal);
        var resolved = new Dictionary<string, SqlAccount>(StringComparer.Ordinal);
        var discovered = new List<SqlAccount>();
        var seen = new HashSet<Guid>();
        foreach (var incoming in command.Accounts)
        {
            var attributes = Utc(incoming.Attributes) with { Login = NameOf(incoming.LoginRef) };
            var identity = SqlIdentity.Account(incoming.Level, incoming.Database, incoming.Name, incoming.Host, incoming.Type);
            if (storedByIdentity.TryGetValue(identity, out var account))
            {
                if (!seen.Add(account.Id))
                {
                    throw new DomainException("La instantánea contiene cuentas repetidas.");
                }

                var observation = account.Observe(attributes, seenAt);
                if (observation.Reappeared && !baseline)
                {
                    log.Add(SqlAccessChange.ForAccount(account, SqlChangeType.Added, [], seenAt));
                }
                else if (observation.Changes.Count > 0)
                {
                    log.Add(SqlAccessChange.ForAccount(account, SqlChangeType.Changed, observation.Changes, seenAt));
                }
            }
            else
            {
                account = SqlAccount.Discover(instance.Id, incoming.Level, incoming.Database, incoming.Name, incoming.Host, incoming.Type, attributes, seenAt);
                storedByIdentity.Add(identity, account);
                seen.Add(account.Id);
                discovered.Add(account);
                if (!baseline)
                {
                    log.Add(SqlAccessChange.ForAccount(account, SqlChangeType.Added, [], seenAt));
                }
            }

            resolved[incoming.Ref] = account;
        }

        foreach (var account in stored)
        {
            // RF-13: accounts of a section that could not be read keep their state.
            if (!seen.Contains(account.Id) && instance.WasRead(account.DatabaseName) && account.MarkRemoved(seenAt))
            {
                log.Add(SqlAccessChange.ForAccount(account, SqlChangeType.Removed, [], seenAt));
            }
        }

        accounts.AddRange(discovered);
        var allAccounts = stored.Concat(discovered).ToDictionary(account => account.Id);

        // Elements: role memberships and permissions (RN-3)
        var states = await items.ListStatesAsync(instance.Id, ct);
        var statesByIdentity = states.ToDictionary(state => state.IdentityHash, StringComparer.Ordinal);
        var present = new HashSet<Guid>();
        var restored = new List<Guid>();
        var riskUpdates = new Dictionary<RiskLevel, List<Guid>>();
        var grantorUpdates = new Dictionary<string, List<Guid>>(StringComparer.Ordinal);
        var created = new List<SqlAccessItem>();
        var current = new List<SqlRiskElement>();

        void Observe(string identity, Guid accountId, Guid? roleAccountId, RiskLevel risk, string? grantor, Func<SqlAccessItem> create)
        {
            if (statesByIdentity.TryGetValue(identity, out var state))
            {
                if (!present.Add(state.Id))
                {
                    throw new DomainException("La instantánea contiene elementos repetidos.");
                }

                if (!state.IsCurrent)
                {
                    restored.Add(state.Id);
                    if (!baseline)
                    {
                        log.Add(SqlAccessChange.ForItem(instance.Id, accountId, state.Id, state.Describe(RoleName(state.RoleAccountId)), SqlChangeType.Added, [], seenAt));
                    }
                }
                else if (!string.Equals(state.Grantor, grantor, StringComparison.Ordinal))
                {
                    log.Add(SqlAccessChange.ForItem(
                        instance.Id, accountId, state.Id, state.Describe(RoleName(state.RoleAccountId)), SqlChangeType.Changed, [new SqlFieldChange("grantor", state.Grantor, grantor)], seenAt));
                }

                if (!string.Equals(state.Grantor, grantor, StringComparison.Ordinal))
                {
                    Group(grantorUpdates, grantor ?? string.Empty, state.Id);
                }

                if (state.Risk != risk)
                {
                    Group(riskUpdates, risk, state.Id);
                }
            }
            else
            {
                var item = create();
                statesByIdentity.Add(identity, ToState(item));
                present.Add(item.Id);
                created.Add(item);
                if (!baseline)
                {
                    log.Add(SqlAccessChange.ForItem(instance.Id, accountId, item.Id, item.Describe(RoleName(roleAccountId)), SqlChangeType.Added, [], seenAt));
                }
            }

            current.Add(new SqlRiskElement(accountId, risk, roleAccountId));
        }

        string? RoleName(Guid? roleAccountId) =>
            roleAccountId is { } id && allAccounts.TryGetValue(id, out var role) ? role.Name : null;

        foreach (var membership in command.Memberships)
        {
            var member = resolved[membership.MemberRef];
            var role = resolved[membership.RoleRef];
            var risk = SqlRiskEvaluator.ForMembership(instance.Engine, role.Name);
            Observe(
                SqlIdentity.Membership(member.IdentityHash, role.IdentityHash),
                member.Id,
                role.Id,
                risk,
                null,
                () => SqlAccessItem.Membership(instance.Id, member, role, risk, seenAt));
        }

        foreach (var permission in command.Permissions)
        {
            var grantee = resolved[permission.GranteeRef];
            var grantor = NameOf(permission.GrantorRef);
            var risk = SqlRiskEvaluator.ForPermission(instance.Engine, permission.Securable.Class, permission.Permission, permission.State);
            Observe(
                SqlIdentity.Permission(grantee.IdentityHash, permission.Securable, permission.Permission, permission.State),
                grantee.Id,
                null,
                risk,
                grantor,
                () => SqlAccessItem.Grant(instance.Id, grantee, permission.Securable, permission.Permission, permission.State, grantor, risk, seenAt));
        }

        var removed = new List<Guid>();
        foreach (var state in states)
        {
            if (!state.IsCurrent || present.Contains(state.Id))
            {
                continue;
            }

            if (instance.WasRead(state.DatabaseName))
            {
                removed.Add(state.Id);
                log.Add(SqlAccessChange.ForItem(instance.Id, state.AccountId, state.Id, state.Describe(RoleName(state.RoleAccountId)), SqlChangeType.Removed, [], seenAt));
            }
            else
            {
                // Not read in this snapshot: it is still current with its stored risk (RF-13).
                current.Add(new SqlRiskElement(state.AccountId, state.Risk, state.RoleAccountId));
            }
        }

        // Set-based updates of existing elements (RNF-2), then the new rows on save.
        await items.MarkRemovedAsync(removed, seenAt, ct);
        await items.TouchAsync(instance.Id, instance.Unavailable, seenAt, ct);
        await items.RestoreAsync(restored, seenAt, ct);
        foreach (var (grantor, ids) in grantorUpdates)
        {
            await items.SetGrantorAsync(ids, grantor.Length == 0 ? null : grantor, ct);
        }

        foreach (var (risk, ids) in riskUpdates)
        {
            await items.SetRiskAsync(ids, risk, ct);
        }

        await items.InsertAsync(created, ct);

        // Account risk: own elements and inherited from roles (RF-14, P-24).
        SqlRiskEvaluator.ApplyAccountRisks(instance.Engine, allAccounts.Values, current.Where(element => allAccounts[element.AccountId].RemovedAt is null));
        changes.AddRange(log);
    }

    /// <summary>PostgreSQL stores instants in UTC; engines may report local offsets.</summary>
    private static SqlAccountAttributes Utc(SqlAccountAttributes attributes) => attributes with
    {
        CreatedAt = attributes.CreatedAt?.ToUniversalTime(),
        ModifiedAt = attributes.ModifiedAt?.ToUniversalTime(),
        Password = attributes.Password is { } password ? password with { ValidUntil = password.ValidUntil?.ToUniversalTime() } : null,
    };

    private static void Group<TKey>(Dictionary<TKey, List<Guid>> groups, TKey key, Guid id)
        where TKey : notnull
    {
        if (!groups.TryGetValue(key, out var ids))
        {
            groups[key] = ids = [];
        }

        ids.Add(id);
    }

    private static SqlAccessItemState ToState(SqlAccessItem item) => new(
        item.Id,
        item.AccountId,
        item.IdentityHash,
        item.Kind,
        item.RoleAccountId,
        item.SecurableClass,
        item.DatabaseName,
        item.SchemaName,
        item.ObjectName,
        item.ObjectType,
        item.ColumnName,
        item.Permission,
        item.State,
        item.Grantor,
        item.Risk,
        item.RemovedAt);
}

/// <summary>Processing duration of the snapshots, published for the queue criterion of RNF-3 (ADR-0006).</summary>
public static class SqlAuditMetrics
{
    public const string MeterName = "Itmas3.SqlAudit";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Histogram<double> ProcessingDuration = Meter.CreateHistogram<double>(
        "itmas.sql_audit.snapshot.processing.duration", unit: "s", description: "Time spent processing a SQL audit snapshot in the request.");

    public static void RecordProcessing(TimeSpan elapsed, bool applied) =>
        ProcessingDuration.Record(elapsed.TotalSeconds, new KeyValuePair<string, object?>("applied", applied));
}
