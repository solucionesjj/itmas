using Itmas3.Application.Common;

namespace Itmas3.Application.SqlAudit.Queries;

/// <summary>
/// Read side of the SQL access audit (SPEC-0005 RF-20..RF-23): read-only projections with server-side paging, sorting
/// and filters. Values use the wire codes of <see cref="SqlAuditCodes"/>.
/// </summary>
public interface ISqlAuditQueries
{
    Task<PagedResult<SqlInstanceListItem>> SearchInstancesAsync(SqlInstanceSearch search, CancellationToken cancellationToken);

    Task<SqlInstanceDetail?> GetInstanceAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Accounts of an instance; null when the instance does not exist.</summary>
    Task<PagedResult<SqlAccountListItem>?> SearchAccountsAsync(Guid instanceId, SqlAccountSearch search, CancellationToken cancellationToken);

    /// <summary>Permissions of an instance; null when the instance does not exist.</summary>
    Task<PagedResult<SqlAccessItemView>?> SearchPermissionsAsync(Guid instanceId, SqlItemSearch search, CancellationToken cancellationToken);

    Task<SqlAccountDetail?> GetAccountAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Roles and permissions of an account; null when the account does not exist.</summary>
    Task<PagedResult<SqlAccessItemView>?> SearchAccountItemsAsync(Guid accountId, SqlItemSearch search, CancellationToken cancellationToken);

    /// <summary>Change log and validation history of an account, newest first; null when the account does not exist.</summary>
    Task<PagedResult<SqlAccountHistoryEntry>?> GetAccountHistoryAsync(Guid accountId, PageRequest page, CancellationToken cancellationToken);
}

public enum SqlInstanceSort
{
    Alias,
    LastCollectedAt,
    Pending,
    PendingCritical,
}

public sealed record SqlInstanceSearch(string? Text, string? Engine, SqlInstanceSort Sort, bool Descending, PageRequest Page);

public enum SqlAccountSort
{
    Name,
    Risk,
    Pending,
    FirstSeenAt,
}

/// <summary>Validation filter of accounts: pending elements, fully validated, changed since validated, removed.</summary>
public enum SqlAccountValidationFilter
{
    Current,
    Pending,
    Valid,
    Changed,
    Removed,
}

public sealed record SqlAccountSearch(
    string? Level, string? Database, string? Type, string? Risk, SqlAccountValidationFilter Validation, string? Text, SqlAccountSort Sort, bool Descending, PageRequest Page);

public enum SqlItemSort
{
    Risk,
    Account,
    Securable,
    Permission,
    FirstSeenAt,
}

public enum SqlItemValidationFilter
{
    Current,
    Pending,
    Valid,
    Removed,
}

public sealed record SqlItemSearch(
    string? Kind,
    string? Risk,
    SqlItemValidationFilter Validation,
    string? Database,
    string? Account,
    string? ObjectType,
    string? Text,
    SqlItemSort Sort,
    bool Descending,
    PageRequest Page);

public sealed record SqlDeviceRef(Guid Id, string Name, string Status);

public sealed record SqlValidationView(string Status, string? By, DateTimeOffset? At);

public sealed record SqlUnavailableView(string Section, string Reason);

public sealed record SqlDatabaseView(string Name, string State, string? Owner);

public sealed record SqlInstanceListItem(
    Guid Id,
    SqlDeviceRef Device,
    string Alias,
    string Engine,
    string Host,
    int? Port,
    string? InstanceName,
    string? Version,
    string Tls,
    DateTimeOffset? LastCollectedAt,
    int AccountCount,
    int PendingCount,
    int PendingCriticalCount,
    bool HasUnavailable);

public sealed record SqlInstanceDetail(
    Guid Id,
    SqlDeviceRef Device,
    string Alias,
    string Engine,
    string Host,
    int? Port,
    string? InstanceName,
    string? Version,
    string Tls,
    DateTimeOffset? LastCollectedAt,
    int AccountCount,
    int PendingCount,
    int PendingCriticalCount,
    bool HasUnavailable,
    string? Edition,
    string? AuthenticationMode,
    DateTimeOffset? LastReceivedAt,
    IReadOnlyList<SqlDatabaseView> Databases,
    IReadOnlyList<SqlUnavailableView> Unavailable,
    int OmittedBuiltinPermissions,
    IReadOnlyList<string> AccountTypes,
    IReadOnlyList<string> ObjectTypes);

public sealed record SqlAccountListItem(
    Guid Id,
    string Level,
    string? Database,
    string Name,
    string? Host,
    string Type,
    bool Enabled,
    bool Locked,
    string Risk,
    bool RiskInherited,
    IReadOnlyList<string> Roles,
    int PendingCount,
    SqlValidationView Validation,
    string Fingerprint,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset? RemovedAt,
    bool Stale);

public sealed record SqlInstanceRef(Guid Id, string Alias, string Engine, string Host, int? Port, string? InstanceName, string Tls, SqlDeviceRef Device);

public sealed record SqlPasswordView(bool? PolicyEnforced, bool? ExpirationEnforced, bool? Expired, DateTimeOffset? ValidUntil);

public sealed record SqlAccountDetail(
    Guid Id,
    string Level,
    string? Database,
    string Name,
    string? Host,
    string Type,
    bool Enabled,
    bool Locked,
    string Risk,
    bool RiskInherited,
    IReadOnlyList<string> Roles,
    int PendingCount,
    SqlValidationView Validation,
    string Fingerprint,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset? RemovedAt,
    bool Stale,
    SqlInstanceRef Instance,
    string OwnRisk,
    string? InheritedRisk,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ModifiedAt,
    string? DefaultDatabase,
    string? DefaultSchema,
    string? Login,
    bool IsOrphaned,
    SqlPasswordView? Password,
    DateTimeOffset LastSeenAt,
    IReadOnlyList<string> Databases,
    IReadOnlyList<string> ObjectTypes);

public sealed record SqlAccountRefView(Guid Id, string Name, string Level, string? Database, string? Host, string Type);

public sealed record SqlSecurableView(string Class, string? Database, string? Schema, string? Name, string? ObjectType, string? Column);

public sealed record SqlRoleRefView(Guid AccountId, string Name);

public sealed record SqlAccessItemView(
    Guid Id,
    string Kind,
    SqlAccountRefView? Account,
    SqlSecurableView? Securable,
    SqlRoleRefView? Role,
    string? Permission,
    string? State,
    string? Grantor,
    string Risk,
    SqlValidationView Validation,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset? RemovedAt,
    bool Stale);

public sealed record SqlHistoryItemView(string Kind, SqlSecurableView? Securable, string? Role, string? Permission, string? State);

public sealed record SqlFieldChangeView(string Field, string? Before, string? After);

public sealed record SqlAccountHistoryEntry(
    DateTimeOffset At,
    string Type,
    string Target,
    SqlHistoryItemView? Item,
    IReadOnlyList<SqlFieldChangeView> Changes,
    string? By);
