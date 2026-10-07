using System.ComponentModel.DataAnnotations;
using Npgsql;

namespace Itmas3.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL connection settings. Bound from the <c>Database</c> configuration section, which in containers
/// is supplied through environment variables (<c>Database__Host</c>, <c>Database__Password</c>, ...).
/// The password MUST NOT be stored in any versioned file.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 5432;

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public SslMode SslMode { get; set; } = SslMode.Prefer;

    [Range(1, 1000)]
    public int MaxPoolSize { get; set; } = 50;

    public string BuildConnectionString() =>
        new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = Name,
            Username = Username,
            Password = Password,
            SslMode = SslMode,
            // Kerberos/GSSAPI is not used; avoids probing for native libraries absent from the image.
            GssEncryptionMode = GssEncryptionMode.Disable,
            MaxPoolSize = MaxPoolSize,
            ApplicationName = "itmas3",
            // Never echo server-side error details (may contain data values) into exceptions and logs.
            IncludeErrorDetail = false,
            Timeout = 15,
            CommandTimeout = 30,
        }.ConnectionString;

    /// <summary>Safe representation for diagnostics: never includes the password.</summary>
    public override string ToString() => $"Host={Host};Port={Port};Database={Name};Username={Username}";
}
