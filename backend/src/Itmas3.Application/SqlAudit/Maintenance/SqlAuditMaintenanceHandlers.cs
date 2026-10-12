using Itmas3.Application.SqlAudit.Abstractions;
using Itmas3.Domain.SqlAudit;

namespace Itmas3.Application.SqlAudit.Maintenance;

/// <summary>
/// Use case: delete the change log, the snapshot receipts and the elements and accounts removed from the engine older
/// than the retention period (P-14). Current state is kept while the instance exists; validations stay in the audit log.
/// </summary>
public sealed class PurgeSqlAuditHistoryHandler(
    ISqlAccessChangeRepository changes,
    ISqlSnapshotReceiptRepository receipts,
    ISqlAccessItemStore items,
    ISqlAccountRepository accounts,
    SqlAuditSettings settings,
    TimeProvider timeProvider)
{
    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow().AddDays(-settings.ChangeLogRetentionDays);
        var deleted = await changes.DeleteDetectedBeforeAsync(cutoff, cancellationToken);
        deleted += await receipts.DeleteReceivedBeforeAsync(cutoff, cancellationToken);
        deleted += await items.DeleteRemovedBeforeAsync(cutoff, cancellationToken);
        deleted += await accounts.DeleteRemovedBeforeAsync(cutoff, cancellationToken);
        return deleted;
    }
}

/// <summary>
/// Use case: recalculate the stored risks of the instances evaluated with an older catalog, so a catalog change takes
/// effect on deployment without touching the agents (RF-15). Returns the number of instances updated.
/// </summary>
public sealed class RecalculateSqlRiskHandler(
    ISqlInstanceRepository instances,
    ISqlAccountRepository accounts,
    ISqlAccessItemStore items,
    ISqlAuditUnitOfWork unitOfWork)
{
    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var pending = await instances.ListWithRiskCatalogBeforeAsync(SqlRiskCatalog.Version, cancellationToken);
        foreach (var instanceId in pending)
        {
            await unitOfWork.ExecuteAsync(async (_, ct) =>
            {
                var instance = await instances.GetAsync(instanceId, ct);
                if (instance is null || instance.RiskCatalogVersion >= SqlRiskCatalog.Version)
                {
                    return false;
                }

                var stored = (await accounts.ListByInstanceAsync(instanceId, ct)).ToDictionary(account => account.Id);
                var states = await items.ListStatesAsync(instanceId, ct);
                var updates = new Dictionary<RiskLevel, List<Guid>>();
                var current = new List<SqlRiskElement>();
                foreach (var state in states)
                {
                    var risk = SqlRiskEvaluator.ForState(instance.Engine, state, stored) ?? state.Risk;
                    if (risk != state.Risk)
                    {
                        if (!updates.TryGetValue(risk, out var ids))
                        {
                            updates[risk] = ids = [];
                        }

                        ids.Add(state.Id);
                    }

                    if (state.IsCurrent && stored.TryGetValue(state.AccountId, out var owner) && owner.RemovedAt is null)
                    {
                        current.Add(new SqlRiskElement(state.AccountId, risk, state.RoleAccountId));
                    }
                }

                foreach (var (risk, ids) in updates)
                {
                    await items.SetRiskAsync(ids, risk, ct);
                }

                SqlRiskEvaluator.ApplyAccountRisks(instance.Engine, stored.Values, current);
                instance.MarkRiskCalculated(SqlRiskCatalog.Version);
                return true;
            }, cancellationToken);
        }

        return pending.Count;
    }
}
