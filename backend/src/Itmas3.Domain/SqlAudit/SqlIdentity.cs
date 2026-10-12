using System.Security.Cryptography;
using System.Text;

namespace Itmas3.Domain.SqlAudit;

/// <summary>
/// Identities of accounts and elements (RN-2, RN-3) as SHA-256 (hex) of a canonical key, and the fingerprint of the
/// attributes of an account (RN-4). Snapshots are compared by these hashes (SPEC-0005 §10.5).
/// </summary>
public static class SqlIdentity
{
    public const int HashLength = 64;

    // Unit separator between parts and a marker for null, so ("a", null) and ("a", "") never collide.
    private const char Separator = '\u001F';
    private const string NullMarker = "\u0000";

    public static string Account(SqlAccountLevel level, string? database, string name, string? host, string type) =>
        Hash("account", Level(level), database, name, host, type);

    public static string Membership(string memberIdentity, string roleIdentity) =>
        Hash("membership", memberIdentity, roleIdentity);

    public static string Permission(string granteeIdentity, SqlSecurable securable, string permission, SqlPermissionState state)
    {
        ArgumentNullException.ThrowIfNull(securable);
        return Hash(
            "permission",
            granteeIdentity,
            securable.Class.ToString(),
            securable.Database,
            securable.Schema,
            securable.Name,
            securable.ObjectType,
            securable.Column,
            permission,
            state.ToString());
    }

    /// <summary>
    /// Fingerprint of the attributes an account validation refers to (RN-4). The engine modification date is left out:
    /// it changes for reasons that are not access changes (e.g. a password rotation). The creation date is included, so
    /// an account dropped and created again with the same name is detected (P-9).
    /// </summary>
    public static string Fingerprint(string type, SqlAccountAttributes attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        var password = attributes.Password;
        return Hash(
            "fingerprint",
            type,
            Flag(attributes.Enabled),
            Flag(attributes.Locked),
            Date(attributes.CreatedAt),
            attributes.DefaultDatabase,
            attributes.DefaultSchema,
            attributes.Login,
            Flag(attributes.IsOrphaned),
            password is null ? null : "password",
            Flag(password?.PolicyEnforced),
            Flag(password?.ExpirationEnforced),
            Flag(password?.Expired),
            Date(password?.ValidUntil));
    }

    private static string Level(SqlAccountLevel level) => level == SqlAccountLevel.Database ? "database" : "server";

    private static string? Flag(bool? value) => value switch
    {
        true => "1",
        false => "0",
        null => null,
    };

    private static string? Date(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static string Hash(params string?[] parts)
    {
        var canonical = string.Join(Separator, parts.Select(part => part ?? NullMarker));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
