using System.Text.RegularExpressions;
using Itmas3.Domain.Common;

namespace Itmas3.Domain.Inventory;

/// <summary>Lifecycle of a registered device (SPEC-0003 RF-6).</summary>
public enum DeviceStatus
{
    /// <summary>Registered; no agent has enrolled with its code yet.</summary>
    Pending,

    /// <summary>An agent enrolled with its code and may send inventories.</summary>
    Active,

    /// <summary>Deleted by a user: neither its code nor its API key work any more (RN-5, RN-6).</summary>
    Deleted,
}

/// <summary>Kind of device (SPEC-0003 RN-2b).</summary>
public enum DeviceType
{
    Workstation,
    Server,
}

/// <summary>
/// A device of the inventory (SPEC-0002, SPEC-0003). It is registered by a user, who receives a one-time enrollment
/// code; the agent exchanges that code for the device API key (ADR-0009, ADR-0012). The device identity is the API key
/// (SPEC-0002 RN-1); the hardware id only links re-installations of the agent on the same machine.
/// Summary columns mirror the latest inventory so devices can be filtered without reading the payloads.
/// </summary>
public sealed partial class Device : Entity<Guid>
{
    public const int MaxNameLength = 100;
    public const int MaxEmailLength = 254;
    public const int MaxHardwareIdLength = 128;
    public const int MaxTextLength = 256;
    public const int SecretHashLength = 64;
    public const int MaxIpLength = 45;

    private Device(Guid id, string name, DeviceType type, DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        Type = type;
        CreatedAt = createdAt;
        Status = DeviceStatus.Pending;
    }

    public string Name { get; private set; }

    public DeviceType Type { get; private set; }

    /// <summary>E-mail of the collaborator who owns the device (trimmed, lowercase). Not necessarily an ITMas user.</summary>
    public string? OwnerEmail { get; private set; }

    public DeviceStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Incremented on every change made by a user; used for optimistic concurrency (ETag).</summary>
    public int Revision { get; private set; }

    /// <summary>SHA-256 (hex) of the current one-time enrollment code, or null when none is valid (RN-3).</summary>
    public string? EnrollmentCodeHash { get; private set; }

    public DateTimeOffset? EnrollmentCodeIssuedAt { get; private set; }

    public string? HardwareId { get; private set; }

    /// <summary>SHA-256 (hex) of the device API key. The key itself is never stored. Null until enrolled or once deleted.</summary>
    public string? ApiKeyHash { get; private set; }

    public DateTimeOffset? EnrolledAt { get; private set; }

    public string? EnrolledFromIp { get; private set; }

    public string? TermsVersion { get; private set; }

    public DateTimeOffset? TermsAcceptedAt { get; private set; }

    public DateTimeOffset? LastSeenAt { get; private set; }

    public string? LastConnectionIp { get; private set; }

    public Guid? LastReportId { get; private set; }

    public DateTimeOffset? LastCollectedAt { get; private set; }

    public string? Hostname { get; private set; }

    public string? OsFamily { get; private set; }

    public string? OsName { get; private set; }

    public string? OsVersion { get; private set; }

    public string? AgentVersion { get; private set; }

    public string? PublicIp { get; private set; }

    public string? CountryCode { get; private set; }

    public string? Region { get; private set; }

    public string? City { get; private set; }

    public string? SystemVolumeEncryption { get; private set; }

    public string? FirewallStatus { get; private set; }

    public bool? HasLocalAdmins { get; private set; }

    /// <summary>Registers a device with its first enrollment code (SPEC-0003 RF-1). Uniqueness of the name is checked by the use case.</summary>
    public static Device Register(string name, DeviceType type, string? ownerEmail, string enrollmentCodeHash, DateTimeOffset now)
    {
        var device = new Device(Guid.CreateVersion7(now), ValidName(name), ValidType(type), now)
        {
            OwnerEmail = NormalizeEmail(ownerEmail),
            Revision = 1,
        };
        device.IssueEnrollmentCode(enrollmentCodeHash, now);
        return device;
    }

    /// <summary>Changes the data maintained by users (RF-4). Deleted devices cannot change (RN-5).</summary>
    public void Update(string name, DeviceType type, string? ownerEmail)
    {
        EnsureNotDeleted();
        Name = ValidName(name);
        Type = ValidType(type);
        OwnerEmail = NormalizeEmail(ownerEmail);
        Revision++;
    }

    /// <summary>Replaces the enrollment code; the previous one stops working (RF-3, RN-3).</summary>
    public void IssueEnrollmentCode(string enrollmentCodeHash, DateTimeOffset now)
    {
        EnsureNotDeleted();
        EnrollmentCodeHash = ValidHash(enrollmentCodeHash, nameof(enrollmentCodeHash));
        EnrollmentCodeIssuedAt = now;
    }

    /// <summary>
    /// Enrolls the agent that presented the current code (RF-7, RF-9): links the hardware, stores the new API key hash
    /// (the previous key stops working) and consumes the code. Returns true when the device was already active (re-link).
    /// </summary>
    public bool Enroll(string hardwareId, string apiKeyHash, DeviceEnrollmentData enrollment, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        EnsureNotDeleted();
        if (EnrollmentCodeHash is null)
        {
            throw new DomainException("El equipo no tiene un código de enrolamiento vigente.");
        }

        var relinked = Status == DeviceStatus.Active;
        HardwareId = Required(hardwareId, MaxHardwareIdLength, "El identificador de hardware");
        ApiKeyHash = ValidHash(apiKeyHash, nameof(apiKeyHash));
        EnrollmentCodeHash = null;
        Status = DeviceStatus.Active;
        EnrolledAt = now;
        EnrolledFromIp = enrollment.SourceIp;
        TermsVersion = Required(enrollment.TermsVersion, 20, "La versión de los términos");
        TermsAcceptedAt = enrollment.TermsAcceptedAt;
        Hostname = Required(enrollment.Hostname, MaxTextLength, "El nombre del equipo");
        OsFamily = Required(enrollment.OsFamily, MaxTextLength, "La familia del sistema operativo");
        AgentVersion = Required(enrollment.AgentVersion, MaxTextLength, "La versión del agente");
        return relinked;
    }

    /// <summary>
    /// Logical deletion (RF-5): the code and the API key stop working in the same transaction (RN-6) and the hardware id
    /// may be registered again (RN-4). The history is kept. Deleting twice is a no-op.
    /// </summary>
    public bool Delete(DateTimeOffset now)
    {
        if (Status == DeviceStatus.Deleted)
        {
            return false;
        }

        Status = DeviceStatus.Deleted;
        DeletedAt = now;
        EnrollmentCodeHash = null;
        ApiKeyHash = null;
        Revision++;
        return true;
    }

    /// <summary>
    /// Records a received inventory. The summary is replaced only when the report is newer than the current one (RN-5);
    /// older reports are kept in the history only.
    /// </summary>
    public bool ApplyReport(InventoryReport report, DeviceSummary summary)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(summary);
        if (report.DeviceId != Id)
        {
            throw new DomainException("El inventario no pertenece a este dispositivo.");
        }

        if (Status != DeviceStatus.Active)
        {
            throw new DomainException("El equipo no está activo.");
        }

        LastSeenAt = report.ReceivedAt;
        LastConnectionIp = report.ConnectionIp;

        if (LastCollectedAt is { } current && report.CollectedAt <= current)
        {
            return false;
        }

        LastReportId = report.Id;
        LastCollectedAt = report.CollectedAt;
        Hostname = Required(summary.Hostname, MaxTextLength, "El nombre del equipo");
        OsFamily = Required(summary.OsFamily, MaxTextLength, "La familia del sistema operativo");
        OsName = summary.OsName;
        OsVersion = summary.OsVersion;
        AgentVersion = Required(summary.AgentVersion, MaxTextLength, "La versión del agente");
        PublicIp = report.PublicIp;
        CountryCode = report.CountryCode;
        Region = report.Region;
        City = report.City;
        SystemVolumeEncryption = summary.SystemVolumeEncryption;
        FirewallStatus = summary.FirewallStatus;
        HasLocalAdmins = summary.HasLocalAdmins;
        return true;
    }

    /// <summary>Normalized device name (RN-1): trimmed, 1–100 characters, no control characters.</summary>
    public static string ValidName(string? name)
    {
        var trimmed = Required(name, MaxNameLength, "El nombre del equipo");
        return trimmed.Any(char.IsControl) ? throw new DomainException("El nombre del equipo contiene caracteres no permitidos.") : trimmed;
    }

    /// <summary>Normalized owner e-mail (RN-2): null when empty, otherwise trimmed, lowercase and well formed.</summary>
    public static string? NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > MaxEmailLength || !EmailPattern().IsMatch(trimmed))
        {
            throw new DomainException("El correo del propietario no es válido.");
        }

        return trimmed.ToLowerInvariant();
    }

    private void EnsureNotDeleted()
    {
        if (Status == DeviceStatus.Deleted)
        {
            throw new DomainException("El equipo está eliminado y no se puede modificar.");
        }
    }

    private static DeviceType ValidType(DeviceType type) =>
        Enum.IsDefined(type) ? type : throw new DomainException("El tipo de equipo no es válido.");

    private static string ValidHash(string hash, string parameterName) =>
        hash is { Length: SecretHashLength }
            ? hash
            : throw new ArgumentException("The hash must be a SHA-256 hex string.", parameterName);

    private static string Required(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException($"{field} es obligatorio.");
        }

        return trimmed.Length <= maxLength ? trimmed : throw new DomainException($"{field} supera la longitud máxima.");
    }

    // Pragmatic address check: one '@', no spaces or control characters, a dot in the domain.
    [GeneratedRegex(@"^[^@\s\p{Cc}]+@[^@\s\p{Cc}]+\.[^@\s\p{Cc}]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex EmailPattern();
}

/// <summary>Data declared by the agent (and observed by the server) when a device enrolls.</summary>
public sealed record DeviceEnrollmentData(
    string Hostname,
    string OsFamily,
    string AgentVersion,
    string TermsVersion,
    DateTimeOffset TermsAcceptedAt,
    string? SourceIp);

/// <summary>Values derived from an inventory that are copied to the device for filtering.</summary>
public sealed record DeviceSummary(
    string Hostname,
    string OsFamily,
    string? OsName,
    string? OsVersion,
    string AgentVersion,
    string? SystemVolumeEncryption,
    string? FirewallStatus,
    bool? HasLocalAdmins);
