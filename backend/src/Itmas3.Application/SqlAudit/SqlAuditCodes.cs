using Itmas3.Domain.SqlAudit;

namespace Itmas3.Application.SqlAudit;

/// <summary>
/// Wire names of the SQL audit enumerations (contract <c>sql-audit v1</c> and query API), also used as stored values.
/// <c>Parse*</c> return null for unknown codes so callers decide how to reject them.
/// </summary>
public static class SqlAuditCodes
{
    public static readonly IReadOnlyList<string> AccountTypes =
    [
        "sql-login", "windows-login", "windows-group", "entra-login", "server-role", "database-user", "database-role",
        "application-role", "user-without-login", "pg-role", "mysql-account", "mysql-role", "certificate", "other",
    ];

    /// <summary>The only account type whose name may be empty: a MySQL/MariaDB anonymous account (<c>''@host</c>).</summary>
    public const string AnonymousAccountType = "mysql-account";

    public static readonly IReadOnlyList<string> ObjectTypes =
    [
        "table", "view", "materialized-view", "foreign-table", "procedure", "function", "aggregate", "sequence", "type",
        "domain", "synonym", "schema", "database", "language", "large-object", "foreign-data-wrapper", "foreign-server",
        "tablespace", "parameter", "assembly", "xml-schema-collection", "certificate", "asymmetric-key", "symmetric-key",
        "full-text-catalog", "service-broker", "login", "server-role", "database-role", "application-role", "user",
        "endpoint", "availability-group", "event", "other",
    ];

    public static readonly IReadOnlyList<string> DatabaseStates =
        ["online", "offline", "restoring", "recovering", "suspect", "emergency", "unknown"];

    public static readonly IReadOnlyList<string> AuthenticationModes = ["mixed", "windows", "password", "unknown"];

    public static readonly IReadOnlyList<string> UnavailableReasons =
        ["connection-failed", "certificate-invalid", "authentication-failed", "permission-denied", "offline", "timeout", "error"];

    public static string ToCode(SqlEngine engine) => engine switch
    {
        SqlEngine.PostgreSql => "postgresql",
        SqlEngine.MySql => "mysql",
        SqlEngine.MariaDb => "mariadb",
        _ => "sqlserver",
    };

    public static SqlEngine? ParseEngine(string? code) => code switch
    {
        "sqlserver" => SqlEngine.SqlServer,
        "postgresql" => SqlEngine.PostgreSql,
        "mysql" => SqlEngine.MySql,
        "mariadb" => SqlEngine.MariaDb,
        _ => null,
    };

    public static string ToCode(SqlTlsMode mode) => mode switch
    {
        SqlTlsMode.Pinned => "pinned",
        SqlTlsMode.Unverified => "unverified",
        _ => "verify",
    };

    public static SqlTlsMode? ParseTlsMode(string? code) => code switch
    {
        "verify" => SqlTlsMode.Verify,
        "pinned" => SqlTlsMode.Pinned,
        "unverified" => SqlTlsMode.Unverified,
        _ => null,
    };

    public static string ToCode(RiskLevel risk) => risk switch
    {
        RiskLevel.Critical => "critical",
        RiskLevel.High => "high",
        RiskLevel.Denied => "denied",
        _ => "general",
    };

    public static RiskLevel? ParseRisk(string? code) => code switch
    {
        "critical" => RiskLevel.Critical,
        "high" => RiskLevel.High,
        "general" => RiskLevel.General,
        "denied" => RiskLevel.Denied,
        _ => null,
    };

    public static string ToCode(SqlValidationStatus status) => status switch
    {
        SqlValidationStatus.Valid => "valid",
        SqlValidationStatus.Changed => "changed",
        _ => "pending",
    };

    public static SqlValidationStatus ParseValidationStatus(string? code) => code switch
    {
        "valid" => SqlValidationStatus.Valid,
        "changed" => SqlValidationStatus.Changed,
        _ => SqlValidationStatus.Pending,
    };

    public static string ToCode(SqlAccountLevel level) => level == SqlAccountLevel.Database ? "database" : "server";

    public static SqlAccountLevel? ParseLevel(string? code) => code switch
    {
        "server" => SqlAccountLevel.Server,
        "database" => SqlAccountLevel.Database,
        _ => null,
    };

    public static string ToCode(SqlAccessItemKind kind) => kind == SqlAccessItemKind.RoleMembership ? "role-membership" : "permission";

    public static SqlAccessItemKind? ParseKind(string? code) => code switch
    {
        "role-membership" => SqlAccessItemKind.RoleMembership,
        "permission" => SqlAccessItemKind.Permission,
        _ => null,
    };

    public static string ToCode(SqlSecurableClass securableClass) => securableClass switch
    {
        SqlSecurableClass.Database => "database",
        SqlSecurableClass.Schema => "schema",
        SqlSecurableClass.Object => "object",
        SqlSecurableClass.Column => "column",
        SqlSecurableClass.DefaultPrivileges => "default-privileges",
        _ => "server",
    };

    public static SqlSecurableClass? ParseSecurableClass(string? code) => code switch
    {
        "server" => SqlSecurableClass.Server,
        "database" => SqlSecurableClass.Database,
        "schema" => SqlSecurableClass.Schema,
        "object" => SqlSecurableClass.Object,
        "column" => SqlSecurableClass.Column,
        "default-privileges" => SqlSecurableClass.DefaultPrivileges,
        _ => null,
    };

    public static string ToCode(SqlPermissionState state) => state switch
    {
        SqlPermissionState.GrantWithGrantOption => "grant-with-grant-option",
        SqlPermissionState.Deny => "deny",
        _ => "grant",
    };

    public static SqlPermissionState? ParseState(string? code) => code switch
    {
        "grant" => SqlPermissionState.Grant,
        "grant-with-grant-option" => SqlPermissionState.GrantWithGrantOption,
        "deny" => SqlPermissionState.Deny,
        _ => null,
    };

    public static string ToCode(SqlChangeType changeType) => changeType switch
    {
        SqlChangeType.Removed => "removed",
        SqlChangeType.Changed => "changed",
        _ => "added",
    };

    public static SqlChangeType ParseChangeType(string? code) => code switch
    {
        "removed" => SqlChangeType.Removed,
        "changed" => SqlChangeType.Changed,
        _ => SqlChangeType.Added,
    };

    public static string ToCode(SqlChangeTarget target) => target switch
    {
        SqlChangeTarget.RoleMembership => "role-membership",
        SqlChangeTarget.Permission => "permission",
        _ => "account",
    };

    public static SqlChangeTarget ParseChangeTarget(string? code) => code switch
    {
        "role-membership" => SqlChangeTarget.RoleMembership,
        "permission" => SqlChangeTarget.Permission,
        _ => SqlChangeTarget.Account,
    };
}
