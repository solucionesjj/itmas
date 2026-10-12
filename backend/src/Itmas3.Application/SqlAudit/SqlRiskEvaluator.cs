using Itmas3.Domain.SqlAudit;

namespace Itmas3.Application.SqlAudit;

/// <summary>Applies <see cref="SqlRiskCatalog"/> to stored elements and accounts (RF-14, RF-15).</summary>
internal static class SqlRiskEvaluator
{
    public static RiskLevel ForMembership(SqlEngine engine, string roleName) => SqlRiskCatalog.ForMembership(engine, roleName);

    public static RiskLevel ForPermission(SqlEngine engine, SqlSecurableClass securableClass, string permission, SqlPermissionState state) =>
        SqlRiskCatalog.ForPermission(engine, securableClass, permission, state);

    /// <summary>Risk of a stored element; null when it cannot be evaluated (a membership whose role is unknown).</summary>
    public static RiskLevel? ForState(SqlEngine engine, Abstractions.SqlAccessItemState state, IReadOnlyDictionary<Guid, SqlAccount> accounts)
    {
        if (state.Kind == SqlAccessItemKind.RoleMembership)
        {
            return state.RoleAccountId is { } roleId && accounts.TryGetValue(roleId, out var role)
                ? ForMembership(engine, role.Name)
                : null;
        }

        return state.SecurableClass is { } securableClass && state.Permission is { } permission && state.State is { } permissionState
            ? ForPermission(engine, securableClass, permission, permissionState)
            : null;
    }

    /// <summary>Recalculates the own and inherited risk of the current accounts from the current elements.</summary>
    public static void ApplyAccountRisks(SqlEngine engine, IEnumerable<SqlAccount> accounts, IEnumerable<SqlRiskElement> currentElements)
    {
        var current = accounts.Where(account => account.RemovedAt is null).ToDictionary(account => account.Id);
        var baseline = current.ToDictionary(pair => pair.Key, pair => SqlRiskCatalog.ForAccount(engine, pair.Value.Name, pair.Value.AccountType));
        foreach (var (accountId, risk) in SqlRiskPropagation.Calculate(baseline, currentElements))
        {
            current[accountId].SetRisk(risk.Own, risk.Inherited);
        }
    }
}
