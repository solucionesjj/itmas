namespace Itmas3.Domain.SqlAudit;

/// <summary>Database engines audited by the agent (SPEC-0005 §3).</summary>
public enum SqlEngine
{
    SqlServer,
    PostgreSql,
    MySql,
    MariaDb,
}

/// <summary>Protection of the connection between the agent and the engine (RF-3, ADR-0015). Always encrypted.</summary>
public enum SqlTlsMode
{
    /// <summary>Certificate validated against the system trust store and the host name.</summary>
    Verify,

    /// <summary>Only the pinned certificate is accepted.</summary>
    Pinned,

    /// <summary>Encrypted, but the certificate is not validated: chosen expressly by the device administrator.</summary>
    Unverified,
}

/// <summary>
/// Risk level of an element (RF-14). Ordered so that the maximum is the highest risk; <see cref="Denied"/> is a
/// <c>DENY</c>, which carries no risk colour and never raises the risk of an account.
/// </summary>
public enum RiskLevel
{
    Denied = 0,
    General = 1,
    High = 2,
    Critical = 3,
}

/// <summary>
/// Validation state stored on an element (RF-17). "Removed from the engine" is not stored here: it is derived from
/// <c>RemovedAt</c>, because a removed element keeps the validation it had.
/// </summary>
public enum SqlValidationStatus
{
    Pending,
    Valid,

    /// <summary>Only accounts: validated, but an attribute changed afterwards (RN-4, P-9).</summary>
    Changed,
}

public enum SqlAccountLevel
{
    Server,
    Database,
}

public enum SqlAccessItemKind
{
    RoleMembership,
    Permission,
}

public enum SqlSecurableClass
{
    Server,
    Database,
    Schema,
    Object,
    Column,
    DefaultPrivileges,
}

public enum SqlPermissionState
{
    Grant,
    GrantWithGrantOption,
    Deny,
}

public enum SqlChangeType
{
    Added,
    Removed,
    Changed,
}

public enum SqlChangeTarget
{
    Account,
    RoleMembership,
    Permission,
}

/// <summary>A database of an instance as reported in the latest snapshot.</summary>
public sealed record SqlDatabaseInfo(string Name, string State, string? Owner);

/// <summary>
/// A part of the instance that could not be read in the latest snapshot (RF-7): <c>instance</c>, <c>server</c> or
/// <c>database:&lt;name&gt;</c>, with the reason reported by the agent.
/// </summary>
public sealed record SqlUnavailablePart(string Section, string Reason)
{
    public const string InstanceSection = "instance";
    public const string ServerSection = "server";
    public const string DatabaseSectionPrefix = "database:";

    public static string DatabaseSection(string database) => DatabaseSectionPrefix + database;
}

/// <summary>Identity of an instance within the device that reports it (RN-1, P-23).</summary>
public sealed record SqlInstanceKey(SqlEngine Engine, string Host, int? Port, string? InstanceName)
{
    /// <summary>Normalises the host (trimmed, lower case, without the trailing dot of a FQDN) and the instance name.</summary>
    public static SqlInstanceKey Create(SqlEngine engine, string host, int? port, string? instanceName)
    {
        var normalizedHost = host?.Trim().TrimEnd('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedHost))
        {
            throw new Common.DomainException("El host de la instancia es obligatorio.");
        }

        if (port is < 1 or > 65535)
        {
            throw new Common.DomainException("El puerto de la instancia no es válido.");
        }

        var normalizedInstance = string.IsNullOrWhiteSpace(instanceName) ? null : instanceName.Trim().ToUpperInvariant();
        return new SqlInstanceKey(engine, normalizedHost, port, normalizedInstance);
    }
}

/// <summary>Attributes of an account that its validation refers to (RN-4), plus the dates reported by the engine.</summary>
public sealed record SqlAccountAttributes(
    bool Enabled,
    bool Locked,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ModifiedAt,
    string? DefaultDatabase,
    string? DefaultSchema,
    string? Login,
    bool IsOrphaned,
    SqlPasswordAttributes? Password);

public sealed record SqlPasswordAttributes(bool? PolicyEnforced, bool? ExpirationEnforced, bool? Expired, DateTimeOffset? ValidUntil);

/// <summary>Object a permission applies to (RN-3).</summary>
public sealed record SqlSecurable(
    SqlSecurableClass Class, string? Database, string? Schema, string? Name, string? ObjectType, string? Column);

/// <summary>A field of an account (or the grantor of an element) that changed between two snapshots.</summary>
public sealed record SqlFieldChange(string Field, string? Before, string? After);

/// <summary>Readable description of an element, kept in the change log so it survives the purge of the element.</summary>
public sealed record SqlItemDescription(
    SqlAccessItemKind Kind, SqlSecurable? Securable, string? Role, string? Permission, SqlPermissionState? State);

/// <summary>Details stored with a change log entry.</summary>
public sealed record SqlChangeDetails(SqlItemDescription? Item, IReadOnlyList<SqlFieldChange> Changes)
{
    public static SqlChangeDetails None { get; } = new(null, []);
}

/// <summary>Who validated or unvalidated an element.</summary>
public sealed record SqlValidationActor(Guid? UserId, string DisplayName);
