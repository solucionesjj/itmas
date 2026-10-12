using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Itmas3.Agent.Collectors.SqlAudit.Model;

namespace Itmas3.Agent.Collectors.SqlAudit;

/// <summary>
/// TLS policy of the engine connections (SPEC-0005 RF-3, ADR-0015). Every connection is encrypted; the mode only decides
/// how the server certificate is authenticated:
/// <list type="bullet">
/// <item><c>verify</c>: OS trust store and host name (no policy errors at all).</item>
/// <item><c>pinned</c>: only the certificate whose SHA-256 was pinned, whatever its chain or name.</item>
/// <item><c>unverified</c>: any certificate; the channel is still encrypted.</item>
/// </list>
/// The callback is used by Npgsql and MySqlConnector; SQL Server uses the equivalent SqlClient options
/// (<c>Encrypt=Mandatory</c>, <c>ServerCertificate</c>, <c>TrustServerCertificate</c>).
/// </summary>
public static class SqlTls
{
    /// <summary>Upper-case hexadecimal SHA-256 (64 characters) of the DER encoding of a certificate.</summary>
    public static string Thumbprint(X509Certificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        return Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData()));
    }

    /// <summary>Accepts "AB:CD:…", "ab cd …" or plain hexadecimal; null when it is not a SHA-256 fingerprint.</summary>
    public static string? NormalizeThumbprint(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var hex = new string(value.Where(character => character is not (':' or ' ' or '-')).ToArray()).Trim().ToUpperInvariant();
        return hex.Length == 64 && hex.All(char.IsAsciiHexDigit) ? hex : null;
    }

    /// <summary>Loads a certificate file (PEM or DER). Throws <see cref="CryptographicException"/> when it is not one.</summary>
    public static X509Certificate2 LoadCertificate(string path) => X509CertificateLoader.LoadCertificateFromFile(path);

    public static RemoteCertificateValidationCallback ValidationCallback(string tlsMode, string? pinnedSha256)
    {
        switch (tlsMode)
        {
            case SqlTlsModes.Verify:
                return static (_, certificate, _, errors) => certificate is not null && errors == SslPolicyErrors.None;
            case SqlTlsModes.Pinned:
                var pinned = Convert.FromHexString(NormalizeThumbprint(pinnedSha256) ?? throw new ArgumentException("A pinned certificate requires its SHA-256.", nameof(pinnedSha256)));
                return (_, certificate, _, _) =>
                    certificate is not null && CryptographicOperations.FixedTimeEquals(SHA256.HashData(certificate.GetRawCertData()), pinned);
            case SqlTlsModes.Unverified:
                // ADR-0015: encrypted, certificate deliberately not validated (chosen expressly by the administrator).
                return static (_, certificate, _, _) => certificate is not null;
            default:
                throw new ArgumentOutOfRangeException(nameof(tlsMode), tlsMode, "Unknown TLS mode.");
        }
    }
}
