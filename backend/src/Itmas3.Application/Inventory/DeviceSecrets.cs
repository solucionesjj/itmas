using System.Security.Cryptography;
using System.Text;

namespace Itmas3.Application.Inventory;

/// <summary>Generation, hashing and comparison of device credentials (ADR-0009).</summary>
public static class DeviceSecrets
{
    private const int ApiKeyBytes = 32;

    /// <summary>New device API key: 256 random bits, base64url (no padding).</summary>
    public static string NewApiKey() => Base64Url(RandomNumberGenerator.GetBytes(ApiKeyBytes));

    /// <summary>SHA-256 of a secret, lowercase hex. Enough for high-entropy keys; the key itself is never stored.</summary>
    public static string Hash(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    /// <summary>Constant-time comparison (over fixed-length hashes, so the length of the secret does not leak).</summary>
    public static bool FixedTimeEquals(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
