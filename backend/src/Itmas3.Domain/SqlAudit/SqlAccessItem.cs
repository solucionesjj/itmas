using Itmas3.Domain.Common;

namespace Itmas3.Domain.SqlAudit;

/// <summary>
/// An element validated on its own (RF-16): the membership of an account in a role, or a permission (including
/// ownership and PostgreSQL role attributes). Identified by <see cref="IdentityHash"/> (RN-3): changing any identifying
/// value produces another element, so an element never "changes" (RF-17); only its grantor and risk are refreshed.
/// <see cref="DatabaseName"/> is the database the element belongs to (null: server level), which is also its section.
/// </summary>
public sealed class SqlAccessItem : Entity<Guid>
{
    public const int MaxTextLength = 256;
    public const int MaxPermissionLength = 128;

    private SqlAccessItem(Guid id, Guid instanceId, Guid accountId, string identityHash, SqlAccessItemKind kind)
        : base(id)
    {
        InstanceId = instanceId;
        AccountId = accountId;
        IdentityHash = identityHash;
        Kind = kind;
    }

    public Guid InstanceId { get; private set; }

    /// <summary>The member of the role or the grantee of the permission.</summary>
    public Guid AccountId { get; private set; }

    public string IdentityHash { get; private set; }

    public SqlAccessItemKind Kind { get; private set; }

    public Guid? RoleAccountId { get; private set; }

    public SqlSecurableClass? SecurableClass { get; private set; }

    public string? DatabaseName { get; private set; }

    public string? SchemaName { get; private set; }

    public string? ObjectName { get; private set; }

    public string? ObjectType { get; private set; }

    public string? ColumnName { get; private set; }

    public string? Permission { get; private set; }

    public SqlPermissionState? State { get; private set; }

    public string? Grantor { get; private set; }

    public RiskLevel Risk { get; private set; }

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    /// <summary><see cref="SqlValidationStatus.Pending"/> or <see cref="SqlValidationStatus.Valid"/>.</summary>
    public SqlValidationStatus ValidationStatus { get; private set; }

    public Guid? ValidatedByUserId { get; private set; }

    public string? ValidatedBy { get; private set; }

    public DateTimeOffset? ValidatedAt { get; private set; }

    public SqlSecurable? Securable => SecurableClass is { } securableClass
        ? new SqlSecurable(securableClass, DatabaseName, SchemaName, ObjectName, ObjectType, ColumnName)
        : null;

    public static SqlAccessItem Membership(
        Guid instanceId, SqlAccount member, SqlAccount role, RiskLevel risk, DateTimeOffset seenAt)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(role);
        if (member.InstanceId != instanceId || role.InstanceId != instanceId)
        {
            throw new DomainException("La pertenencia a un rol debe ser de cuentas de la misma instancia.");
        }

        return new SqlAccessItem(
            Guid.CreateVersion7(seenAt), instanceId, member.Id, SqlIdentity.Membership(member.IdentityHash, role.IdentityHash), SqlAccessItemKind.RoleMembership)
        {
            RoleAccountId = role.Id,
            DatabaseName = role.DatabaseName,
            Risk = risk,
            FirstSeenAt = seenAt,
            LastSeenAt = seenAt,
            ValidationStatus = SqlValidationStatus.Pending,
        };
    }

    public static SqlAccessItem Grant(
        Guid instanceId,
        SqlAccount grantee,
        SqlSecurable securable,
        string permission,
        SqlPermissionState state,
        string? grantor,
        RiskLevel risk,
        DateTimeOffset seenAt)
    {
        ArgumentNullException.ThrowIfNull(grantee);
        ArgumentNullException.ThrowIfNull(securable);
        if (grantee.InstanceId != instanceId)
        {
            throw new DomainException("El titular del permiso debe ser una cuenta de la misma instancia.");
        }

        if (string.IsNullOrEmpty(permission))
        {
            throw new DomainException("El permiso es obligatorio.");
        }

        return new SqlAccessItem(
            Guid.CreateVersion7(seenAt), instanceId, grantee.Id, SqlIdentity.Permission(grantee.IdentityHash, securable, permission, state), SqlAccessItemKind.Permission)
        {
            SecurableClass = securable.Class,
            DatabaseName = securable.Database,
            SchemaName = securable.Schema,
            ObjectName = securable.Name,
            ObjectType = securable.ObjectType,
            ColumnName = securable.Column,
            Permission = permission,
            State = state,
            Grantor = grantor,
            Risk = risk,
            FirstSeenAt = seenAt,
            LastSeenAt = seenAt,
            ValidationStatus = SqlValidationStatus.Pending,
        };
    }

    /// <summary>Marks the element as valid (RF-16). Removed elements cannot be marked (RN-5). Idempotent.</summary>
    public bool Validate(SqlValidationActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (RemovedAt is not null)
        {
            throw new DomainException("El elemento ya no figura en el motor y no se puede marcar como válido.");
        }

        if (ValidationStatus == SqlValidationStatus.Valid)
        {
            return false;
        }

        ValidationStatus = SqlValidationStatus.Valid;
        ValidatedByUserId = actor.UserId;
        ValidatedBy = actor.DisplayName.Length <= SqlAccount.MaxActorLength ? actor.DisplayName : actor.DisplayName[..SqlAccount.MaxActorLength];
        ValidatedAt = now;
        return true;
    }

    /// <summary>Back to pending (RF-16). Idempotent.</summary>
    public bool Unvalidate()
    {
        if (ValidationStatus == SqlValidationStatus.Pending)
        {
            return false;
        }

        ValidationStatus = SqlValidationStatus.Pending;
        ValidatedByUserId = null;
        ValidatedBy = null;
        ValidatedAt = null;
        return true;
    }

    public SqlItemDescription Describe(string? roleName) =>
        new(Kind, Securable, Kind == SqlAccessItemKind.RoleMembership ? roleName : null, Permission, State);
}
