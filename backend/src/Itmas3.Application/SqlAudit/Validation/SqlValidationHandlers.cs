using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Application.SqlAudit.Abstractions;
using Itmas3.Domain.SqlAudit;

namespace Itmas3.Application.SqlAudit.Validation;

/// <summary>Validation of an element as returned to the UI (RF-16, §8.3).</summary>
public sealed record SqlValidationResult(SqlValidationStatus Status, string? By, DateTimeOffset? At);

/// <summary>
/// Use case: mark an account as valid with the fingerprint the user saw (RF-16, RN-4..RN-6). Removed accounts cannot be
/// marked (422); a fingerprint that is no longer the current one is a conflict (409). Idempotent and audited.
/// </summary>
public sealed class ValidateSqlAccountHandler(
    ISqlAccountRepository accounts, ISqlAuditUnitOfWork unitOfWork, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public Task<SqlValidationResult> HandleAsync(Guid accountId, string fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        return unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var account = await accounts.GetAsync(accountId, ct) ?? throw new NotFoundException("SQL account not found.");
            if (account.RemovedAt is null && !string.Equals(account.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException("Esta cuenta cambió desde que cargaste la página. Revisa los cambios y vuelve a validarla.");
            }

            if (account.Validate(SqlValidationActors.Of(currentUser), timeProvider.GetUtcNow()))
            {
                audit.Record(
                    SqlAuditAuditActions.AccountValidated, SqlAuditAuditActions.AccountResourceType, account.Id.ToString(), null, SqlValidationAuditView.OfAccount(account));
            }

            return new SqlValidationResult(account.ValidationStatus, account.ValidatedBy, account.ValidatedAt);
        }, cancellationToken);
    }
}

/// <summary>Use case: return an account to pending (RF-16). Idempotent; audited only when something changed.</summary>
public sealed class UnvalidateSqlAccountHandler(ISqlAccountRepository accounts, ISqlAuditUnitOfWork unitOfWork)
{
    public Task<bool> HandleAsync(Guid accountId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var account = await accounts.GetAsync(accountId, ct) ?? throw new NotFoundException("SQL account not found.");
            var before = SqlValidationAuditView.OfAccount(account);
            if (!account.Unvalidate())
            {
                return false;
            }

            audit.Record(
                SqlAuditAuditActions.AccountUnvalidated, SqlAuditAuditActions.AccountResourceType, account.Id.ToString(), before, SqlValidationAuditView.OfAccount(account));
            return true;
        }, cancellationToken);
}

/// <summary>Use case: mark a role membership or a permission as valid (RF-16, RN-5, RN-6). Idempotent and audited.</summary>
public sealed class ValidateSqlAccessItemHandler(
    ISqlAccessItemStore items, ISqlAccountRepository accounts, ISqlAuditUnitOfWork unitOfWork, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public Task<SqlValidationResult> HandleAsync(Guid itemId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var item = await items.GetAsync(itemId, ct) ?? throw new NotFoundException("SQL access item not found.");
            if (item.Validate(SqlValidationActors.Of(currentUser), timeProvider.GetUtcNow()))
            {
                var (account, roleName) = await OwnerAsync(accounts, item, ct);
                audit.Record(
                    SqlAuditAuditActions.ItemValidated, SqlAuditAuditActions.ItemResourceType, item.Id.ToString(), null, SqlValidationAuditView.OfItem(item, account, roleName));
            }

            return new SqlValidationResult(item.ValidationStatus, item.ValidatedBy, item.ValidatedAt);
        }, cancellationToken);

    internal static async Task<(SqlAccount Account, string? RoleName)> OwnerAsync(ISqlAccountRepository accounts, SqlAccessItem item, CancellationToken ct)
    {
        var account = await accounts.GetAsync(item.AccountId, ct) ?? throw new NotFoundException("SQL account not found.");
        var role = item.RoleAccountId is { } roleId ? await accounts.GetAsync(roleId, ct) : null;
        return (account, role?.Name);
    }
}

/// <summary>Use case: return a role membership or a permission to pending (RF-16). Idempotent.</summary>
public sealed class UnvalidateSqlAccessItemHandler(ISqlAccessItemStore items, ISqlAccountRepository accounts, ISqlAuditUnitOfWork unitOfWork)
{
    public Task<bool> HandleAsync(Guid itemId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var item = await items.GetAsync(itemId, ct) ?? throw new NotFoundException("SQL access item not found.");
            var (account, roleName) = await ValidateSqlAccessItemHandler.OwnerAsync(accounts, item, ct);
            var before = SqlValidationAuditView.OfItem(item, account, roleName);
            if (!item.Unvalidate())
            {
                return false;
            }

            audit.Record(
                SqlAuditAuditActions.ItemUnvalidated, SqlAuditAuditActions.ItemResourceType, item.Id.ToString(), before, SqlValidationAuditView.OfItem(item, account, roleName));
            return true;
        }, cancellationToken);
}

internal static class SqlValidationActors
{
    public static SqlValidationActor Of(ICurrentUser user) =>
        new(user.ApplicationUserId, user.DisplayName ?? user.Username ?? "Desconocido");
}
