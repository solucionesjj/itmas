using System.Security.Cryptography;
using System.Text;

namespace Itmas3.Application.Inventory;

/// <summary>Generation, normalization and hashing of device credentials (ADR-0009, ADR-0012).</summary>
public static class DeviceSecrets
{
    /// <summary>Characters of a normalized enrollment code (Crockford Base32: no I, L, O, U).</summary>
    public const int EnrollmentCodeLength = 20;

    private const int ApiKeyBytes = 32;
    private const int EnrollmentCodeGroup = 5;
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>New device API key: 256 random bits, base64url (no padding).</summary>
    public static string NewApiKey() => Base64Url(RandomNumberGenerator.GetBytes(ApiKeyBytes));

    /// <summary>
    /// New one-time enrollment code (ADR-0012): 20 Crockford Base32 characters (100 random bits), displayed in groups
    /// of five (<c>XXXXX-XXXXX-XXXXX-XXXXX</c>).
    /// </summary>
    public static string NewEnrollmentCode()
    {
        var code = new StringBuilder(EnrollmentCodeLength + (EnrollmentCodeLength / EnrollmentCodeGroup) - 1);
        for (var i = 0; i < EnrollmentCodeLength; i++)
        {
            if (i > 0 && i % EnrollmentCodeGroup == 0)
            {
                code.Append('-');
            }

            code.Append(CrockfordAlphabet[RandomNumberGenerator.GetInt32(CrockfordAlphabet.Length)]);
        }

        return code.ToString();
    }

    /// <summary>
    /// Canonical form of an enrollment code as typed by a person: without separators, uppercase, with the Crockford
    /// substitutions (I, L → 1; O → 0). Null when it cannot be a valid code, so it is rejected without a lookup.
    /// </summary>
    public static string? NormalizeEnrollmentCode(string? code)
    {
        if (code is null || code.Length > EnrollmentCodeLength * 2)
        {
            return null;
        }

        var normalized = new StringBuilder(EnrollmentCodeLength);
        foreach (var character in code)
        {
            if (character is '-' or ' ')
            {
                continue;
            }

            var upper = char.ToUpperInvariant(character) switch
            {
                'I' or 'L' => '1',
                'O' => '0',
                var other => other,
            };

            if (!CrockfordAlphabet.Contains(upper, StringComparison.Ordinal) || normalized.Length == EnrollmentCodeLength)
            {
                return null;
            }

            normalized.Append(upper);
        }

        return normalized.Length == EnrollmentCodeLength ? normalized.ToString() : null;
    }

    /// <summary>Hash of an enrollment code in any accepted spelling; null when the code is malformed.</summary>
    public static string? HashEnrollmentCode(string? code) =>
        NormalizeEnrollmentCode(code) is { } normalized ? Hash(normalized) : null;

    /// <summary>SHA-256 of a secret, lowercase hex. Enough for high-entropy keys; the key itself is never stored.</summary>
    public static string Hash(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
