using Itmas3.Domain.Common;

namespace Itmas3.Domain.Inventory;

/// <summary>
/// Device enrolled through the inventory agent (SPEC-0002). Its identity is the API key issued at enrollment (RN-1);
/// the hardware id only links re-installations of the agent on the same machine (RN-4).
/// Summary columns mirror the latest inventory so devices can be filtered without reading the payloads.
/// </summary>
public sealed class Device : Entity<Guid>
{
    public const int MaxHardwareIdLength = 128;
    public const int MaxTextLength = 256;
    public const int ApiKeyHashLength = 64;
    public const int MaxIpLength = 45;

    private Device(Guid id, string hardwareId, string apiKeyHash, DateTimeOffset enrolledAt)
        : base(id)
    {
        HardwareId = hardwareId;
        ApiKeyHash = apiKeyHash;
        EnrolledAt = enrolledAt;
        Hostname = string.Empty;
        OsFamily = string.Empty;
        AgentVersion = string.Empty;
        TermsVersion = string.Empty;
    }

    public string HardwareId { get; private set; }

    /// <summary>SHA-256 (hex) of the device API key. The key itself is never stored.</summary>
    public string ApiKeyHash { get; private set; }

    public DateTimeOffset EnrolledAt { get; private set; }

    public string? EnrolledFromIp { get; private set; }

    public string TermsVersion { get; private set; }

    public DateTimeOffset TermsAcceptedAt { get; private set; }

    public DateTimeOffset? LastSeenAt { get; private set; }

    public string? LastConnectionIp { get; private set; }

    public Guid? LastReportId { get; private set; }

    public DateTimeOffset? LastCollectedAt { get; private set; }

    public string Hostname { get; private set; }

    public string OsFamily { get; private set; }

    public string? OsName { get; private set; }

    public string? OsVersion { get; private set; }

    public string AgentVersion { get; private set; }

    public string? PublicIp { get; private set; }

    public string? CountryCode { get; private set; }

    public string? Region { get; private set; }

    public string? City { get; private set; }

    public string? SystemVolumeEncryption { get; private set; }

    public string? FirewallStatus { get; private set; }

    public bool? HasLocalAdmins { get; private set; }

    public static Device Enroll(string hardwareId, string apiKeyHash, DeviceEnrollmentData enrollment, DateTimeOffset now)
    {
        var device = new Device(Guid.CreateVersion7(now), Required(hardwareId, MaxHardwareIdLength, "El identificador de hardware"), apiKeyHash, now);
        device.ApplyEnrollment(apiKeyHash, enrollment, now);
        return device;
    }

    /// <summary>Re-enrollment of the same machine (RN-4): the new key replaces the previous one, which stops working.</summary>
    public void Relink(string apiKeyHash, DeviceEnrollmentData enrollment, DateTimeOffset now) =>
        ApplyEnrollment(apiKeyHash, enrollment, now);

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

    private void ApplyEnrollment(string apiKeyHash, DeviceEnrollmentData enrollment, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        if (apiKeyHash.Length != ApiKeyHashLength)
        {
            throw new ArgumentException("The API key hash must be a SHA-256 hex string.", nameof(apiKeyHash));
        }

        ApiKeyHash = apiKeyHash;
        EnrolledAt = now;
        EnrolledFromIp = enrollment.SourceIp;
        TermsVersion = Required(enrollment.TermsVersion, 20, "La versión de los términos");
        TermsAcceptedAt = enrollment.TermsAcceptedAt;
        Hostname = Required(enrollment.Hostname, MaxTextLength, "El nombre del equipo");
        OsFamily = Required(enrollment.OsFamily, MaxTextLength, "La familia del sistema operativo");
        AgentVersion = Required(enrollment.AgentVersion, MaxTextLength, "La versión del agente");
    }

    private static string Required(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException($"{field} es obligatorio.");
        }

        return trimmed.Length <= maxLength ? trimmed : throw new DomainException($"{field} supera la longitud máxima.");
    }
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
