using System.Text.RegularExpressions;
using Itmas3.Agent.Collectors.SqlAudit.Model;

namespace Itmas3.Agent.Collectors.SqlAudit;

/// <summary>
/// Non-secret definition of one engine connection configured on the device (SPEC-0005 RF-2). The password travels
/// separately and is never part of this record. Connection strings are always composed from these fields with each
/// driver's builder: free-form connection strings do not exist (ADR-0014 §3).
/// </summary>
/// <param name="Port">Null only for a SQL Server named instance resolved by SQL Browser.</param>
/// <param name="InstanceName">SQL Server named instance; null otherwise.</param>
/// <param name="Database">Initial database (PostgreSQL); null for the other engines.</param>
/// <param name="PinnedSha256">Upper-case hexadecimal SHA-256 of the pinned certificate (mode pinned).</param>
/// <param name="PinnedCertificatePath">DER copy of the pinned certificate kept in the protected store (SQL Server).</param>
public sealed record SqlConnectionSettings(
    string Alias,
    string Engine,
    string Host,
    int? Port,
    string? InstanceName,
    string? Database,
    string User,
    string TlsMode,
    string? PinnedSha256 = null,
    string? PinnedCertificatePath = null)
{
    public const string ApplicationName = "ITMas Agent";
    public const int MaxConnections = 50;
    public const string DefaultPostgreSqlDatabase = "postgres";

    /// <summary>30 s per query and 5 min per connection (RNF-1, ADR-0014 §6).</summary>
    public static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan ConnectionBudget = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Destination as shown to people: host, port and instance (never the account).</summary>
    public string Destination =>
        InstanceName is null ? $"{Host}:{Port}" : Port is null ? $@"{Host}\{InstanceName}" : $@"{Host}\{InstanceName}:{Port}";

    public SqlConnectionInfo ToContract() => new(Alias, Engine, Host, Port, InstanceName, TlsMode);

    public static int DefaultPort(string engine) => engine switch
    {
        SqlEngines.SqlServer => 1433,
        SqlEngines.PostgreSql => 5432,
        _ => 3306,
    };
}

/// <summary>Validation of the values an administrator types (they end up in connection strings and the contract).</summary>
public static partial class SqlConnectionRules
{
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    public static partial Regex AliasPattern();

    /// <summary>SQL Server instance names: up to 16 letters, digits, '_', '$', '#' or '&amp;'.</summary>
    [GeneratedRegex("^[A-Za-z0-9_$#&]{1,16}$", RegexOptions.CultureInvariant)]
    public static partial Regex InstanceNamePattern();

    public static bool IsValidAlias(string? value) => value is not null && AliasPattern().IsMatch(value);

    public static bool IsValidEngine(string? value) => value is not null && SqlEngines.All.Contains(value, StringComparer.Ordinal);

    public static bool IsValidTlsMode(string? value) => value is not null && SqlTlsModes.All.Contains(value, StringComparer.Ordinal);

    /// <summary>DNS name or IP address only: no ports, instances, spaces, commas or connection-string syntax.</summary>
    public static bool IsValidHost(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 253
        && Uri.CheckHostName(value) is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or ':' or '_');

    public static bool IsValidPort(int? value) => value is >= 1 and <= 65535;

    public static bool IsValidInstanceName(string? value) => value is not null && InstanceNamePattern().IsMatch(value);

    /// <summary>Engine account and database names: printable text without control characters.</summary>
    public static bool IsValidName(string? value, int maxLength = 128) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength && !value.Any(char.IsControl);

    /// <summary>Returns a description of the first problem, or null when the settings are usable.</summary>
    public static string? Validate(SqlConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsValidAlias(settings.Alias))
        {
            return "El alias debe empezar por una letra o un número y tener como máximo 64 caracteres (letras, números, '.', '_' o '-').";
        }

        if (!IsValidEngine(settings.Engine))
        {
            return "El motor debe ser sqlserver, postgresql, mysql o mariadb.";
        }

        if (!IsValidHost(settings.Host))
        {
            return "El host debe ser un nombre DNS o una dirección IP, sin puerto ni instancia.";
        }

        if (settings.InstanceName is not null && (settings.Engine != SqlEngines.SqlServer || !IsValidInstanceName(settings.InstanceName)))
        {
            return "La instancia con nombre solo se admite en SQL Server (hasta 16 letras, números, '_', '$', '#' o '&').";
        }

        if (settings.Port is not null ? !IsValidPort(settings.Port) : settings.InstanceName is null)
        {
            return "El puerto debe estar entre 1 y 65535.";
        }

        if (settings.Database is not null && (settings.Engine != SqlEngines.PostgreSql || !IsValidName(settings.Database, 63)))
        {
            return "La base de datos inicial solo se indica en PostgreSQL (nombre de hasta 63 caracteres).";
        }

        if (!IsValidName(settings.User))
        {
            return "Indique el usuario del motor dedicado al agente.";
        }

        if (!IsValidTlsMode(settings.TlsMode))
        {
            return "El modo TLS debe ser verify, pinned o unverified.";
        }

        if (settings.TlsMode == SqlTlsModes.Pinned && settings.PinnedSha256 is null)
        {
            return "El modo pinned requiere el certificado del servidor (archivo o huella SHA-256).";
        }

        return null;
    }
}
