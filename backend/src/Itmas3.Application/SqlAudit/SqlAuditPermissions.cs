using Itmas3.Application.Authorization;
using Itmas3.Domain.SqlAudit;

namespace Itmas3.Application.SqlAudit;

/// <summary>Permissions of the SQL access audit (SPEC-0005 §7).</summary>
public static class SqlAuditPermissions
{
    public const string Category = "Auditoría SQL";

    public const string Read = "sql-audit.read";
    public const string AccessesValidate = "sql-audit.accesses.validate";
}

internal sealed class SqlAuditPermissionDefinitions : IPermissionDefinitionProvider
{
    private const string Category = SqlAuditPermissions.Category;

    public IEnumerable<PermissionDeclaration> GetPermissions() =>
    [
        new(SqlAuditPermissions.Read, "Consultar auditoría SQL", "Ver las instancias de motores SQL auditadas, sus cuentas, roles, permisos, nivel de riesgo, registro de cambios e historial de validaciones.", Category),
        new(SqlAuditPermissions.AccessesValidate, "Validar accesos SQL", "Marcar y desmarcar como válidos cuentas, pertenencias a roles y permisos de los motores SQL.", Category),
    ];
}

/// <summary>Audit actions of the SQL access audit (SPEC-0005 §12.3), written to the shared audit log.</summary>
public static class SqlAuditAuditActions
{
    public const string AccountValidated = "sql-audit.account.validated";
    public const string AccountUnvalidated = "sql-audit.account.unvalidated";
    public const string ItemValidated = "sql-audit.item.validated";
    public const string ItemUnvalidated = "sql-audit.item.unvalidated";

    public const string AccountResourceType = "sql-account";
    public const string ItemResourceType = "sql-access-item";
}

/// <summary>
/// Audited view of a validation: instance, account, element and validated fingerprint (§12.3). Uses ids and the
/// element description; it never contains secrets.
/// </summary>
internal static class SqlValidationAuditView
{
    public static object OfAccount(SqlAccount account) => new
    {
        InstanceId = account.InstanceId,
        AccountId = account.Id,
        Account = account.Name,
        account.Host,
        Level = SqlAuditCodes.ToCode(account.Level),
        Database = account.DatabaseName,
        Status = SqlAuditCodes.ToCode(account.ValidationStatus),
        Fingerprint = account.ValidatedFingerprint ?? account.Fingerprint,
    };

    public static object OfItem(SqlAccessItem item, SqlAccount account, string? roleName) => new
    {
        InstanceId = item.InstanceId,
        AccountId = account.Id,
        Account = account.Name,
        ItemId = item.Id,
        Kind = SqlAuditCodes.ToCode(item.Kind),
        Role = roleName,
        Securable = item.Securable is { } securable
            ? new
            {
                Class = SqlAuditCodes.ToCode(securable.Class),
                securable.Database,
                securable.Schema,
                securable.Name,
                securable.ObjectType,
                securable.Column,
            }
            : null,
        item.Permission,
        State = item.State is { } state ? SqlAuditCodes.ToCode(state) : null,
        Status = SqlAuditCodes.ToCode(item.ValidationStatus),
    };
}

/// <summary>Deployment settings of the SQL access audit, bound by the composition root.</summary>
public sealed class SqlAuditSettings
{
    /// <summary>Days the change log, the snapshot receipts and the elements removed from the engine are kept (P-14).</summary>
    public int ChangeLogRetentionDays { get; init; } = 365;
}
