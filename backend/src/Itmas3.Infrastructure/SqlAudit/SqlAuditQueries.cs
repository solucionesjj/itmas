using System.Text.Json;
using Itmas3.Application.Common;
using Itmas3.Application.Inventory;
using Itmas3.Application.SqlAudit;
using Itmas3.Application.SqlAudit.Queries;
using Itmas3.Domain.SqlAudit;
using Itmas3.Infrastructure.Authorization;
using Itmas3.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itmas3.Infrastructure.SqlAudit;

/// <summary>
/// Read-only projections of the SQL access audit (no tracking, parameterised SQL only). Pending counters are computed
/// with correlated counts over the partial indexes of the current elements (RF-18, RF-20).
/// </summary>
internal sealed class SqlAuditQueries(AppDbContext db) : ISqlAuditQueries
{
    private const int RoleSummaryLimit = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PagedResult<SqlInstanceListItem>> SearchInstancesAsync(SqlInstanceSearch search, CancellationToken cancellationToken)
    {
        var instances = db.SqlInstances.AsNoTracking();
        if (SqlAuditCodes.ParseEngine(search.Engine) is { } engine)
        {
            instances = instances.Where(instance => instance.Engine == engine);
        }

        if (LikePattern.Contains(search.Text) is { } pattern)
        {
            instances = instances.Where(instance => EF.Functions.ILike(instance.Alias, pattern, LikePattern.Escape)
                || EF.Functions.ILike(instance.Host, pattern, LikePattern.Escape)
                || (instance.InstanceName != null && EF.Functions.ILike(instance.InstanceName, pattern, LikePattern.Escape))
                || db.Devices.Any(device => device.Id == instance.DeviceId && EF.Functions.ILike(device.Name, pattern, LikePattern.Escape)));
        }

        var rows = InstanceRows(instances);
        var total = await rows.CountAsync(cancellationToken);
        var ordered = (search.Sort, search.Descending) switch
        {
            (SqlInstanceSort.LastCollectedAt, false) => rows.OrderBy(row => row.Instance.LastCollectedAt),
            (SqlInstanceSort.LastCollectedAt, true) => rows.OrderByDescending(row => row.Instance.LastCollectedAt != null).ThenByDescending(row => row.Instance.LastCollectedAt),
            (SqlInstanceSort.Pending, false) => rows.OrderBy(row => row.PendingAccounts + row.PendingItems),
            (SqlInstanceSort.Pending, true) => rows.OrderByDescending(row => row.PendingAccounts + row.PendingItems),
            (SqlInstanceSort.PendingCritical, false) => rows.OrderBy(row => row.PendingCriticalAccounts + row.PendingCriticalItems),
            (SqlInstanceSort.PendingCritical, true) => rows.OrderByDescending(row => row.PendingCriticalAccounts + row.PendingCriticalItems),
            (_, true) => rows.OrderByDescending(row => row.Instance.Alias),
            _ => rows.OrderBy(row => row.Instance.Alias),
        };

        var page = await ordered.ThenBy(row => row.Instance.Alias).ThenBy(row => row.Instance.Id)
            .Skip(search.Page.Skip).Take(search.Page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SqlInstanceListItem>(page.Select(ToListItem).ToList(), search.Page.Page, search.Page.PageSize, total);
    }

    public async Task<SqlInstanceDetail?> GetInstanceAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await InstanceRows(db.SqlInstances.AsNoTracking().Where(instance => instance.Id == id)).SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var accountTypes = await db.SqlAccounts.AsNoTracking()
            .Where(account => account.InstanceId == id && account.RemovedAt == null)
            .Select(account => account.AccountType).Distinct().OrderBy(type => type)
            .ToListAsync(cancellationToken);
        var objectTypes = await db.SqlAccessItems.AsNoTracking()
            .Where(item => item.InstanceId == id && item.RemovedAt == null && item.ObjectType != null)
            .Select(item => item.ObjectType!).Distinct().OrderBy(type => type)
            .ToListAsync(cancellationToken);

        var summary = ToListItem(row);
        var instance = row.Instance;
        return new SqlInstanceDetail(
            summary.Id,
            summary.Device,
            summary.Alias,
            summary.Engine,
            summary.Host,
            summary.Port,
            summary.InstanceName,
            summary.Version,
            summary.Tls,
            summary.LastCollectedAt,
            summary.AccountCount,
            summary.PendingCount,
            summary.PendingCriticalCount,
            summary.HasUnavailable,
            instance.Edition,
            instance.AuthenticationMode,
            instance.LastReceivedAt,
            instance.Databases.Select(database => new SqlDatabaseView(database.Name, database.State, database.Owner)).ToList(),
            instance.Unavailable.Select(part => new SqlUnavailableView(part.Section, part.Reason)).ToList(),
            instance.OmittedBuiltinPermissions,
            accountTypes,
            objectTypes);
    }

    public async Task<PagedResult<SqlAccountListItem>?> SearchAccountsAsync(Guid instanceId, SqlAccountSearch search, CancellationToken cancellationToken)
    {
        var unavailable = await UnavailableAsync(instanceId, cancellationToken);
        if (unavailable is null)
        {
            return null;
        }

        var accounts = db.SqlAccounts.AsNoTracking().Where(account => account.InstanceId == instanceId);
        accounts = search.Validation switch
        {
            SqlAccountValidationFilter.Pending => accounts.Where(account => account.RemovedAt == null
                && (account.ValidationStatus != SqlValidationStatus.Valid || db.SqlAccessItems.Any(item => item.AccountId == account.Id
                    && item.RemovedAt == null && item.ValidationStatus != SqlValidationStatus.Valid))),
            SqlAccountValidationFilter.Valid => accounts.Where(account => account.RemovedAt == null
                && account.ValidationStatus == SqlValidationStatus.Valid
                && !db.SqlAccessItems.Any(item => item.AccountId == account.Id && item.RemovedAt == null && item.ValidationStatus != SqlValidationStatus.Valid)),
            SqlAccountValidationFilter.Changed => accounts.Where(account => account.RemovedAt == null && account.ValidationStatus == SqlValidationStatus.Changed),
            SqlAccountValidationFilter.Removed => accounts.Where(account => account.RemovedAt != null),
            _ => accounts.Where(account => account.RemovedAt == null),
        };

        if (SqlAuditCodes.ParseLevel(search.Level) is { } level)
        {
            accounts = accounts.Where(account => account.Level == level);
        }

        if (!string.IsNullOrEmpty(search.Database))
        {
            accounts = accounts.Where(account => account.DatabaseName == search.Database);
        }

        if (!string.IsNullOrEmpty(search.Type))
        {
            accounts = accounts.Where(account => account.AccountType == search.Type);
        }

        if (SqlAuditCodes.ParseRisk(search.Risk) is { } risk)
        {
            accounts = accounts.Where(account => (account.InheritedRisk > account.OwnRisk ? account.InheritedRisk : account.OwnRisk) == risk);
        }

        if (LikePattern.Contains(search.Text) is { } pattern)
        {
            accounts = accounts.Where(account => EF.Functions.ILike(account.Name, pattern, LikePattern.Escape));
        }

        var rows = AccountRows(accounts);
        var total = await rows.CountAsync(cancellationToken);
        var ordered = (search.Sort, search.Descending) switch
        {
            (SqlAccountSort.Risk, false) => rows.OrderBy(row => row.EffectiveRisk),
            (SqlAccountSort.Risk, true) => rows.OrderByDescending(row => row.EffectiveRisk),
            (SqlAccountSort.Pending, false) => rows.OrderBy(row => row.PendingItems + (row.Account.ValidationStatus != SqlValidationStatus.Valid ? 1 : 0)),
            (SqlAccountSort.Pending, true) => rows.OrderByDescending(row => row.PendingItems + (row.Account.ValidationStatus != SqlValidationStatus.Valid ? 1 : 0)),
            (SqlAccountSort.FirstSeenAt, false) => rows.OrderBy(row => row.Account.FirstSeenAt),
            (SqlAccountSort.FirstSeenAt, true) => rows.OrderByDescending(row => row.Account.FirstSeenAt),
            (_, true) => rows.OrderByDescending(row => row.Account.Name),
            _ => rows.OrderBy(row => row.Account.Name),
        };

        var page = await ordered.ThenBy(row => row.Account.Name).ThenBy(row => row.Account.Id)
            .Skip(search.Page.Skip).Take(search.Page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SqlAccountListItem>(
            page.Select(row => ToAccountListItem(row, unavailable)).ToList(), search.Page.Page, search.Page.PageSize, total);
    }

    public async Task<PagedResult<SqlAccessItemView>?> SearchPermissionsAsync(Guid instanceId, SqlItemSearch search, CancellationToken cancellationToken)
    {
        var unavailable = await UnavailableAsync(instanceId, cancellationToken);
        if (unavailable is null)
        {
            return null;
        }

        var items = db.SqlAccessItems.AsNoTracking()
            .Where(item => item.InstanceId == instanceId && item.Kind == SqlAccessItemKind.Permission);
        return await SearchItemsAsync(items, search, unavailable, includeAccount: true, membershipsFirst: false, cancellationToken);
    }

    public async Task<SqlAccountDetail?> GetAccountAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await AccountRows(db.SqlAccounts.AsNoTracking().Where(account => account.Id == id)).SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var instance = await (
                from candidate in db.SqlInstances.AsNoTracking()
                join device in db.Devices.AsNoTracking() on candidate.DeviceId equals device.Id
                where candidate.Id == row.Account.InstanceId
                select new { Instance = candidate, DeviceName = device.Name, DeviceStatus = device.Status })
            .SingleAsync(cancellationToken);

        var currentItems = db.SqlAccessItems.AsNoTracking().Where(item => item.AccountId == id && item.RemovedAt == null);
        var databases = await currentItems.Where(item => item.DatabaseName != null)
            .Select(item => item.DatabaseName!).Distinct().OrderBy(name => name).ToListAsync(cancellationToken);
        var objectTypes = await currentItems.Where(item => item.ObjectType != null)
            .Select(item => item.ObjectType!).Distinct().OrderBy(type => type).ToListAsync(cancellationToken);

        var summary = ToAccountListItem(row, instance.Instance.Unavailable);
        var account = row.Account;
        var instanceRef = new SqlInstanceRef(
            instance.Instance.Id,
            instance.Instance.Alias,
            SqlAuditCodes.ToCode(instance.Instance.Engine),
            instance.Instance.Host,
            instance.Instance.Port,
            instance.Instance.InstanceName,
            SqlAuditCodes.ToCode(instance.Instance.TlsMode),
            new SqlDeviceRef(instance.Instance.DeviceId, instance.DeviceName, DeviceStatuses.ToCode(instance.DeviceStatus)));

        return new SqlAccountDetail(
            summary.Id,
            summary.Level,
            summary.Database,
            summary.Name,
            summary.Host,
            summary.Type,
            summary.Enabled,
            summary.Locked,
            summary.Risk,
            summary.RiskInherited,
            summary.Roles,
            summary.PendingCount,
            summary.Validation,
            summary.Fingerprint,
            summary.FirstSeenAt,
            summary.RemovedAt,
            summary.Stale,
            instanceRef,
            SqlAuditCodes.ToCode(account.OwnRisk),
            account.InheritedRisk > RiskLevel.General ? SqlAuditCodes.ToCode(account.InheritedRisk) : null,
            account.EngineCreatedAt,
            account.EngineModifiedAt,
            account.DefaultDatabase,
            account.DefaultSchema,
            account.LoginName,
            account.IsOrphaned,
            account.HasPassword
                ? new SqlPasswordView(account.PasswordPolicyEnforced, account.PasswordExpirationEnforced, account.PasswordExpired, account.PasswordValidUntil)
                : null,
            account.LastSeenAt,
            databases,
            objectTypes);
    }

    public async Task<PagedResult<SqlAccessItemView>?> SearchAccountItemsAsync(Guid accountId, SqlItemSearch search, CancellationToken cancellationToken)
    {
        var instanceId = await db.SqlAccounts.AsNoTracking()
            .Where(account => account.Id == accountId).Select(account => (Guid?)account.InstanceId)
            .SingleOrDefaultAsync(cancellationToken);
        if (instanceId is null)
        {
            return null;
        }

        var unavailable = (await UnavailableAsync(instanceId.Value, cancellationToken))!;
        var items = db.SqlAccessItems.AsNoTracking().Where(item => item.AccountId == accountId);
        if (SqlAuditCodes.ParseKind(search.Kind) is { } kind)
        {
            items = items.Where(item => item.Kind == kind);
        }

        return await SearchItemsAsync(items, search, unavailable, includeAccount: false, membershipsFirst: true, cancellationToken);
    }

    public async Task<PagedResult<SqlAccountHistoryEntry>?> GetAccountHistoryAsync(Guid accountId, PageRequest page, CancellationToken cancellationToken)
    {
        if (!await db.SqlAccounts.AnyAsync(account => account.Id == accountId, cancellationToken))
        {
            return null;
        }

        var accountKey = accountId.ToString();
        var changes = db.SqlAccessChanges.AsNoTracking().Where(change => change.AccountId == accountId);
        var itemKeys = db.SqlAccessItems.Where(item => item.AccountId == accountId).Select(item => item.Id.ToString());
        var validations = db.AuditEvents.AsNoTracking()
            .Where(auditEvent => auditEvent.Result == "success"
                && ((auditEvent.ResourceType == SqlAuditAuditActions.AccountResourceType && auditEvent.ResourceId == accountKey)
                    || (auditEvent.ResourceType == SqlAuditAuditActions.ItemResourceType && itemKeys.Contains(auditEvent.ResourceId!))));

        var total = await changes.CountAsync(cancellationToken) + await validations.CountAsync(cancellationToken);
        var window = page.Skip + page.PageSize;
        var changeRows = await changes.OrderByDescending(change => change.DetectedAt).ThenByDescending(change => change.Id)
            .Take(window).ToListAsync(cancellationToken);
        var validationRows = await validations.OrderByDescending(auditEvent => auditEvent.OccurredAt).ThenByDescending(auditEvent => auditEvent.Id)
            .Take(window).ToListAsync(cancellationToken);

        var entries = changeRows.Select(change => new SqlAccountHistoryEntry(
                change.DetectedAt,
                SqlAuditCodes.ToCode(change.ChangeType),
                SqlAuditCodes.ToCode(change.Target),
                change.Details.Item is { } item ? ToHistoryItem(item) : null,
                change.Details.Changes.Select(field => new SqlFieldChangeView(field.Field, field.Before, field.After)).ToList(),
                null))
            .Concat(validationRows.Select(ToValidationEntry))
            .OrderByDescending(entry => entry.At)
            .Skip(page.Skip).Take(page.PageSize)
            .ToList();

        return new PagedResult<SqlAccountHistoryEntry>(entries, page.Page, page.PageSize, total);
    }

    private async Task<PagedResult<SqlAccessItemView>> SearchItemsAsync(
        IQueryable<SqlAccessItem> items,
        SqlItemSearch search,
        IReadOnlyList<SqlUnavailablePart> unavailable,
        bool includeAccount,
        bool membershipsFirst,
        CancellationToken cancellationToken)
    {
        items = search.Validation switch
        {
            SqlItemValidationFilter.Pending => items.Where(item => item.RemovedAt == null && item.ValidationStatus != SqlValidationStatus.Valid),
            SqlItemValidationFilter.Valid => items.Where(item => item.RemovedAt == null && item.ValidationStatus == SqlValidationStatus.Valid),
            SqlItemValidationFilter.Removed => items.Where(item => item.RemovedAt != null),
            _ => items.Where(item => item.RemovedAt == null),
        };

        if (SqlAuditCodes.ParseRisk(search.Risk) is { } risk)
        {
            items = items.Where(item => item.Risk == risk);
        }

        if (!string.IsNullOrEmpty(search.Database))
        {
            items = items.Where(item => item.DatabaseName == search.Database);
        }

        if (!string.IsNullOrEmpty(search.ObjectType))
        {
            items = items.Where(item => item.ObjectType == search.ObjectType);
        }

        if (LikePattern.Contains(search.Account) is { } accountPattern)
        {
            items = items.Where(item => db.SqlAccounts.Any(account => account.Id == item.AccountId && EF.Functions.ILike(account.Name, accountPattern, LikePattern.Escape)));
        }

        if (LikePattern.Contains(search.Text) is { } pattern)
        {
            items = items.Where(item => (item.Permission != null && EF.Functions.ILike(item.Permission, pattern, LikePattern.Escape))
                || (item.DatabaseName != null && EF.Functions.ILike(item.DatabaseName, pattern, LikePattern.Escape))
                || (item.SchemaName != null && EF.Functions.ILike(item.SchemaName, pattern, LikePattern.Escape))
                || (item.ObjectName != null && EF.Functions.ILike(item.ObjectName, pattern, LikePattern.Escape))
                || (item.ColumnName != null && EF.Functions.ILike(item.ColumnName, pattern, LikePattern.Escape)));
        }

        var rows =
            from item in items
            join account in db.SqlAccounts on item.AccountId equals account.Id
            join role in db.SqlAccounts on item.RoleAccountId equals role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new ItemRow
            {
                Item = item,
                AccountName = account.Name,
                AccountLevel = account.Level,
                AccountDatabase = account.DatabaseName,
                AccountHost = account.Host,
                AccountType = account.AccountType,
                RoleName = role == null ? null : role.Name,
            };

        var total = await rows.CountAsync(cancellationToken);
        IOrderedQueryable<ItemRow> ordered = membershipsFirst
            ? rows.OrderBy(row => row.Item.Kind == SqlAccessItemKind.Permission)
            : rows.OrderBy(_ => 0);
        ordered = (search.Sort, search.Descending) switch
        {
            (SqlItemSort.Risk, false) => ordered.ThenBy(row => row.Item.Risk),
            (SqlItemSort.Account, false) => ordered.ThenBy(row => row.AccountName),
            (SqlItemSort.Account, true) => ordered.ThenByDescending(row => row.AccountName),
            (SqlItemSort.Securable, true) => ordered.ThenByDescending(row => row.Item.DatabaseName).ThenByDescending(row => row.Item.SchemaName).ThenByDescending(row => row.Item.ObjectName),
            (SqlItemSort.Securable, false) => ordered.ThenBy(row => row.Item.DatabaseName).ThenBy(row => row.Item.SchemaName).ThenBy(row => row.Item.ObjectName),
            (SqlItemSort.Permission, false) => ordered.ThenBy(row => row.Item.Permission),
            (SqlItemSort.Permission, true) => ordered.ThenByDescending(row => row.Item.Permission),
            (SqlItemSort.FirstSeenAt, false) => ordered.ThenBy(row => row.Item.FirstSeenAt),
            (SqlItemSort.FirstSeenAt, true) => ordered.ThenByDescending(row => row.Item.FirstSeenAt),
            _ => ordered.ThenByDescending(row => row.Item.Risk),
        };

        var page = await ordered
            .ThenBy(row => row.RoleName)
            .ThenBy(row => row.AccountName)
            .ThenBy(row => row.Item.DatabaseName)
            .ThenBy(row => row.Item.SchemaName)
            .ThenBy(row => row.Item.ObjectName)
            .ThenBy(row => row.Item.ColumnName)
            .ThenBy(row => row.Item.Permission)
            .ThenBy(row => row.Item.Id)
            .Skip(search.Page.Skip).Take(search.Page.PageSize)
            .ToListAsync(cancellationToken);

        var views = page.Select(row => ToItemView(row, unavailable, includeAccount)).ToList();
        return new PagedResult<SqlAccessItemView>(views, search.Page.Page, search.Page.PageSize, total);
    }

    private IQueryable<InstanceRow> InstanceRows(IQueryable<SqlInstance> instances) =>
        from instance in instances
        join device in db.Devices on instance.DeviceId equals device.Id
        select new InstanceRow
        {
            Instance = instance,
            DeviceName = device.Name,
            DeviceStatus = device.Status,
            AccountCount = db.SqlAccounts.Count(account => account.InstanceId == instance.Id && account.RemovedAt == null),
            PendingAccounts = db.SqlAccounts.Count(account => account.InstanceId == instance.Id && account.RemovedAt == null
                && account.ValidationStatus != SqlValidationStatus.Valid),
            PendingItems = db.SqlAccessItems.Count(item => item.InstanceId == instance.Id && item.RemovedAt == null
                && item.ValidationStatus != SqlValidationStatus.Valid),
            PendingCriticalAccounts = db.SqlAccounts.Count(account => account.InstanceId == instance.Id && account.RemovedAt == null
                && account.ValidationStatus != SqlValidationStatus.Valid
                && (account.OwnRisk == RiskLevel.Critical || account.InheritedRisk == RiskLevel.Critical)),
            PendingCriticalItems = db.SqlAccessItems.Count(item => item.InstanceId == instance.Id && item.RemovedAt == null
                && item.ValidationStatus != SqlValidationStatus.Valid && item.Risk == RiskLevel.Critical),
        };

    private IQueryable<AccountRow> AccountRows(IQueryable<SqlAccount> accounts) =>
        accounts.Select(account => new AccountRow
        {
            Account = account,
            EffectiveRisk = account.InheritedRisk > account.OwnRisk ? account.InheritedRisk : account.OwnRisk,
            PendingItems = db.SqlAccessItems.Count(item => item.AccountId == account.Id && item.RemovedAt == null
                && item.ValidationStatus != SqlValidationStatus.Valid),
            Roles = db.SqlAccessItems
                .Where(item => item.AccountId == account.Id && item.RemovedAt == null && item.Kind == SqlAccessItemKind.RoleMembership)
                .Join(db.SqlAccounts, item => item.RoleAccountId, role => role.Id, (_, role) => role.Name)
                .OrderBy(name => name)
                .Take(RoleSummaryLimit)
                .ToList(),
        });

    private async Task<IReadOnlyList<SqlUnavailablePart>?> UnavailableAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var instance = await db.SqlInstances.AsNoTracking()
            .Where(candidate => candidate.Id == instanceId)
            .Select(candidate => new { candidate.Unavailable })
            .SingleOrDefaultAsync(cancellationToken);
        return instance?.Unavailable;
    }

    private static SqlInstanceListItem ToListItem(InstanceRow row)
    {
        var instance = row.Instance;
        return new SqlInstanceListItem(
            instance.Id,
            new SqlDeviceRef(instance.DeviceId, row.DeviceName, DeviceStatuses.ToCode(row.DeviceStatus)),
            instance.Alias,
            SqlAuditCodes.ToCode(instance.Engine),
            instance.Host,
            instance.Port,
            instance.InstanceName,
            instance.Version,
            SqlAuditCodes.ToCode(instance.TlsMode),
            instance.LastCollectedAt,
            row.AccountCount,
            row.PendingAccounts + row.PendingItems,
            row.PendingCriticalAccounts + row.PendingCriticalItems,
            instance.Unavailable.Count > 0);
    }

    private static SqlAccountListItem ToAccountListItem(AccountRow row, IReadOnlyList<SqlUnavailablePart> unavailable)
    {
        var account = row.Account;
        var current = account.RemovedAt is null;
        return new SqlAccountListItem(
            account.Id,
            SqlAuditCodes.ToCode(account.Level),
            account.DatabaseName,
            account.Name,
            account.Host,
            account.AccountType,
            account.Enabled,
            account.Locked,
            SqlAuditCodes.ToCode(account.EffectiveRisk),
            account.InheritedRisk > account.OwnRisk,
            current ? row.Roles : [],
            current ? row.PendingItems + (account.ValidationStatus != SqlValidationStatus.Valid ? 1 : 0) : 0,
            Validation(account.ValidationStatus, account.RemovedAt, account.ValidatedBy, account.ValidatedAt),
            account.Fingerprint,
            account.FirstSeenAt,
            account.RemovedAt,
            current && !SqlScopes.WasRead(unavailable, account.DatabaseName));
    }

    private static SqlAccessItemView ToItemView(ItemRow row, IReadOnlyList<SqlUnavailablePart> unavailable, bool includeAccount)
    {
        var item = row.Item;
        return new SqlAccessItemView(
            item.Id,
            SqlAuditCodes.ToCode(item.Kind),
            includeAccount
                ? new SqlAccountRefView(item.AccountId, row.AccountName, SqlAuditCodes.ToCode(row.AccountLevel), row.AccountDatabase, row.AccountHost, row.AccountType)
                : null,
            item.Securable is { } securable ? ToSecurableView(securable) : null,
            item.RoleAccountId is { } roleId && row.RoleName is not null ? new SqlRoleRefView(roleId, row.RoleName) : null,
            item.Permission,
            item.State is { } state ? SqlAuditCodes.ToCode(state) : null,
            item.Grantor,
            SqlAuditCodes.ToCode(item.Risk),
            Validation(item.ValidationStatus, item.RemovedAt, item.ValidatedBy, item.ValidatedAt),
            item.FirstSeenAt,
            item.RemovedAt,
            item.RemovedAt is null && !SqlScopes.WasRead(unavailable, item.DatabaseName));
    }

    private static SqlValidationView Validation(SqlValidationStatus status, DateTimeOffset? removedAt, string? by, DateTimeOffset? at) =>
        new(removedAt is null ? SqlAuditCodes.ToCode(status) : "removed", by, at);

    private static SqlSecurableView ToSecurableView(SqlSecurable securable) => new(
        SqlAuditCodes.ToCode(securable.Class), securable.Database, securable.Schema, securable.Name, securable.ObjectType, securable.Column);

    private static SqlHistoryItemView ToHistoryItem(SqlItemDescription item) => new(
        SqlAuditCodes.ToCode(item.Kind),
        item.Securable is { } securable ? ToSecurableView(securable) : null,
        item.Role,
        item.Permission,
        item.State is { } state ? SqlAuditCodes.ToCode(state) : null);

    /// <summary>A validation event of the shared audit log (written by the validation use cases).</summary>
    private static SqlAccountHistoryEntry ToValidationEntry(Persistence.Records.AuditEventRecord auditEvent)
    {
        var validated = auditEvent.Action is SqlAuditAuditActions.AccountValidated or SqlAuditAuditActions.ItemValidated;
        SqlHistoryItemView? item = null;
        var target = "account";
        if (auditEvent.ResourceType == SqlAuditAuditActions.ItemResourceType && auditEvent.After is { } after)
        {
            var view = JsonSerializer.Deserialize<AuditedItem>(after, JsonOptions);
            if (view is not null)
            {
                target = view.Kind ?? "permission";
                item = new SqlHistoryItemView(
                    target,
                    view.Securable is { } securable
                        ? new SqlSecurableView(securable.Class ?? "server", securable.Database, securable.Schema, securable.Name, securable.ObjectType, securable.Column)
                        : null,
                    view.Role,
                    view.Permission,
                    view.State);
            }
        }

        return new SqlAccountHistoryEntry(auditEvent.OccurredAt, validated ? "validated" : "unvalidated", target, item, [], auditEvent.ActorDisplayName);
    }

    private sealed class InstanceRow
    {
        public required SqlInstance Instance { get; init; }

        public required string DeviceName { get; init; }

        public required Domain.Inventory.DeviceStatus DeviceStatus { get; init; }

        public int AccountCount { get; init; }

        public int PendingAccounts { get; init; }

        public int PendingItems { get; init; }

        public int PendingCriticalAccounts { get; init; }

        public int PendingCriticalItems { get; init; }
    }

    private sealed class AccountRow
    {
        public required SqlAccount Account { get; init; }

        public RiskLevel EffectiveRisk { get; init; }

        public int PendingItems { get; init; }

        public required List<string> Roles { get; init; }
    }

    private sealed class ItemRow
    {
        public required SqlAccessItem Item { get; init; }

        public required string AccountName { get; init; }

        public SqlAccountLevel AccountLevel { get; init; }

        public string? AccountDatabase { get; init; }

        public string? AccountHost { get; init; }

        public required string AccountType { get; init; }

        public string? RoleName { get; init; }
    }

    private sealed record AuditedItem(string? Kind, string? Role, AuditedSecurable? Securable, string? Permission, string? State);

    private sealed record AuditedSecurable(string? Class, string? Database, string? Schema, string? Name, string? ObjectType, string? Column);
}
