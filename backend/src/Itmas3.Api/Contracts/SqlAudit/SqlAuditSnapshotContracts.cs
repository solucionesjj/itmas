using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Itmas3.Api.Contracts.Inventory;
using Itmas3.Application.SqlAudit;
using Itmas3.Domain.SqlAudit;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Itmas3.Api.Contracts.SqlAudit;

/// <summary>Shared rules of the snapshot contract <c>sql-audit v1</c> (SPEC-0005 §12.2).</summary>
public static partial class SqlAuditContract
{
    public const int SchemaVersion = 1;
    public const int TextLength = 256;
    public const int MaxBodyBytes = 16 * 1024 * 1024;
    public const int MaxDatabases = 1_000;
    public const int MaxAccounts = 20_000;
    public const int MaxMemberships = 100_000;
    public const int MaxPermissions = 200_000;
    public const int MaxUnavailable = MaxDatabases + 2;

    public const string AliasPattern = "^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$";
    public const string EnginePattern = "^(sqlserver|postgresql|mysql|mariadb)$";
    public const string TlsPattern = "^(verify|pinned|unverified)$";

    /// <summary>Validation errors are reported for the first offending elements only, to keep responses small.</summary>
    public const int MaxReportedErrors = 20;

    [GeneratedRegex("^[A-Za-z0-9_-]{1,16}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    public static partial Regex RefPattern();

    [GeneratedRegex("^[A-Z][A-Z0-9_ ]{0,127}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    public static partial Regex PermissionPattern();

    public static bool IsSafeText(string? text, int maxLength = TextLength) =>
        text is not null && text.Length > 0 && text.Length <= maxLength && !text.Any(char.IsControl);
}

/// <summary>
/// POST /api/v1/sql-audit-snapshots. Strict: unknown properties are rejected (constitution §3.6). The large collections
/// are validated by <see cref="Validate"/> in a single pass instead of the per-node model validation (RNF-2).
/// </summary>
public sealed class SqlAuditSnapshotRequest : IValidatableObject
{
    [Range(SqlAuditContract.SchemaVersion, SqlAuditContract.SchemaVersion)]
    public required int SchemaVersion { get; init; }

    [Required]
    public required Guid? SubmissionId { get; init; }

    [Required]
    public required DateTimeOffset? CollectedAt { get; init; }

    [Required, StringLength(50), RegularExpression(InventoryContract.VersionPattern)]
    public required string AgentVersion { get; init; }

    [Required]
    public required SqlConnectionSection Connection { get; init; }

    public required SqlInstanceSection? Instance { get; init; }

    [Required, MaxLength(SqlAuditContract.MaxDatabases)]
    public required List<SqlDatabaseEntry> Databases { get; init; }

    [ValidateNever]
    public required List<SqlAccountEntry> Accounts { get; init; }

    [ValidateNever]
    public required List<SqlMembershipEntry> RoleMemberships { get; init; }

    [ValidateNever]
    public required List<SqlPermissionEntry> Permissions { get; init; }

    [Required]
    public required SqlOmittedSection Omitted { get; init; }

    [Required, MaxLength(SqlAuditContract.MaxUnavailable)]
    public required List<SqlUnavailableEntry> Unavailable { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        new SqlAuditSnapshotValidator(this).Validate();
}

public sealed class SqlConnectionSection
{
    [Required, RegularExpression(SqlAuditContract.AliasPattern)]
    public required string Alias { get; init; }

    [Required, RegularExpression(SqlAuditContract.EnginePattern)]
    public required string Engine { get; init; }

    [Required, SafeText(SqlAuditContract.TextLength)]
    public required string Host { get; init; }

    [Range(1, 65535)]
    public required int? Port { get; init; }

    [SafeText(SqlInstance.MaxInstanceNameLength)]
    public required string? InstanceName { get; init; }

    [Required, RegularExpression(SqlAuditContract.TlsPattern)]
    public required string Tls { get; init; }
}

public sealed class SqlInstanceSection
{
    [Required, SafeText(SqlAuditContract.TextLength)]
    public required string Version { get; init; }

    [SafeText(SqlAuditContract.TextLength)]
    public required string? Edition { get; init; }

    [Required, RegularExpression("^(mixed|windows|password|unknown)$")]
    public required string AuthenticationMode { get; init; }
}

public sealed class SqlDatabaseEntry
{
    [Required, SafeText(SqlAuditContract.TextLength)]
    public required string Name { get; init; }

    [Required, RegularExpression("^(online|offline|restoring|recovering|suspect|emergency|unknown)$")]
    public required string State { get; init; }

    public required string? OwnerRef { get; init; }
}

public sealed class SqlAccountEntry
{
    public required string Ref { get; init; }

    public required string Level { get; init; }

    public required string? Database { get; init; }

    public required string Name { get; init; }

    public required string? Host { get; init; }

    public required string Type { get; init; }

    public required bool Enabled { get; init; }

    public required bool Locked { get; init; }

    public required DateTimeOffset? CreatedAt { get; init; }

    public required DateTimeOffset? ModifiedAt { get; init; }

    public required string? DefaultDatabase { get; init; }

    public required string? DefaultSchema { get; init; }

    public required string? LoginRef { get; init; }

    public required bool IsOrphaned { get; init; }

    public required SqlPasswordEntry? Password { get; init; }
}

public sealed class SqlPasswordEntry
{
    public required bool? PolicyEnforced { get; init; }

    public required bool? ExpirationEnforced { get; init; }

    public required bool? Expired { get; init; }

    public required DateTimeOffset? ValidUntil { get; init; }
}

public sealed class SqlMembershipEntry
{
    public required string MemberRef { get; init; }

    public required string RoleRef { get; init; }
}

public sealed class SqlPermissionEntry
{
    public required string GranteeRef { get; init; }

    public required SqlSecurableEntry Securable { get; init; }

    public required string Permission { get; init; }

    public required string State { get; init; }

    public required string? GrantorRef { get; init; }
}

public sealed class SqlSecurableEntry
{
    public required string Class { get; init; }

    public required string? Database { get; init; }

    public required string? Schema { get; init; }

    public required string? Name { get; init; }

    public required string? ObjectType { get; init; }

    public required string? Column { get; init; }
}

public sealed class SqlOmittedSection
{
    [Range(0, int.MaxValue)]
    public required int BuiltinPublicPermissions { get; init; }
}

public sealed class SqlUnavailableEntry
{
    [Required, SafeText(SqlAuditContract.TextLength + 9)]
    public required string Section { get; init; }

    [Required, RegularExpression("^(connection-failed|certificate-invalid|authentication-failed|permission-denied|offline|timeout|error)$")]
    public required string Reason { get; init; }
}

public sealed record SqlAuditSnapshotAccepted(Guid SubmissionId);
