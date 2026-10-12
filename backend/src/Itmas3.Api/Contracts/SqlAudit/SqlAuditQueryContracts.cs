using System.ComponentModel.DataAnnotations;

namespace Itmas3.Api.Contracts.SqlAudit;

/// <summary>Query parameters of the SQL audit screens (SPEC-0005 §12.1 and the contract notes).</summary>
public static class SqlAuditQueryContract
{
    public const string EnginePattern = SqlAuditContract.EnginePattern;
    public const string InstanceSortPattern = "^-?(alias|lastCollectedAt|pending|pendingCritical)$";
    public const string LevelPattern = "^(server|database)$";
    public const string AccountRiskPattern = "^(critical|high|general)$";
    public const string ItemRiskPattern = "^(critical|high|general|denied)$";
    public const string AccountValidationPattern = "^(pending|valid|changed|removed)$";
    public const string ItemValidationPattern = "^(pending|valid|removed)$";
    public const string AccountSortPattern = "^-?(name|risk|pending|firstSeenAt)$";
    public const string ItemSortPattern = "^-?(risk|account|securable|permission|firstSeenAt)$";
    public const string KindPattern = "^(role-membership|permission)$";
    public const string CodePattern = "^[a-z][a-z-]{0,39}$";
}

/// <summary>PUT /api/v1/sql-accounts/{id}/validation: the fingerprint of the account the user saw (RN-6).</summary>
public sealed record ValidateSqlAccountRequest(
    [Required, RegularExpression("^[0-9a-fA-F]{64}$")] string Fingerprint);

/// <summary>Validation of an element after marking it (§8.3).</summary>
public sealed record SqlValidationResponse(string Status, string? By, DateTimeOffset? At);
