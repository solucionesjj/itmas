using Itmas3.Application.Inventory;

namespace Itmas3.UnitTests.Application.Inventory;

public sealed class DeviceSecretsTests
{
    [Fact]
    public void Api_keys_are_random_256_bit_base64url()
    {
        var keys = Enumerable.Range(0, 100).Select(_ => DeviceSecrets.NewApiKey()).ToList();

        Assert.Equal(100, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key =>
        {
            Assert.Equal(43, key.Length);
            Assert.Matches("^[A-Za-z0-9_-]+$", key);
        });
    }

    [Fact]
    public void Hash_is_sha256_hex_and_never_the_secret()
    {
        var hash = DeviceSecrets.Hash("secret-value");

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]+$", hash);
        Assert.Equal(hash, DeviceSecrets.Hash("secret-value"));
        Assert.NotEqual(hash, DeviceSecrets.Hash("secret-valuf"));
    }

    [Fact]
    public void Enrollment_codes_are_random_grouped_crockford_base32()
    {
        var codes = Enumerable.Range(0, 100).Select(_ => DeviceSecrets.NewEnrollmentCode()).ToList();

        Assert.Equal(100, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.Matches("^[0-9A-HJKMNP-TV-Z]{5}(-[0-9A-HJKMNP-TV-Z]{5}){3}$", code));
    }

    [Theory]
    [InlineData("ABCDE-FGH1J-KMNPQ-RSTVW", "ABCDEFGH1JKMNPQRSTVW")]
    [InlineData("abcde fghij kmnpq rstvw", "ABCDEFGH1JKMNPQRSTVW")]
    [InlineData("ABCDE-FGHLJ-KMNPQ-RSTV0", "ABCDEFGH1JKMNPQRSTV0")]
    [InlineData("ABCDEFGHIJKMNPQRSTVO", "ABCDEFGH1JKMNPQRSTV0")]
    public void Enrollment_codes_are_normalized_as_typed_by_people(string typed, string expected) =>
        Assert.Equal(expected, DeviceSecrets.NormalizeEnrollmentCode(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABCDE-FGHIJ-KMNPQ")]
    [InlineData("ABCDE-FGHIJ-KMNPQ-RSTVW-X")]
    [InlineData("ABCDE-FGHIJ-KMNPQ-RSTVU")]
    [InlineData("ABCDE_FGHIJ_KMNPQ_RSTVW")]
    [InlineData("a3f1c9e2b7d4a3f1c9e2b7d4a3f1c9e2b7d4a3f1c9e2b7d4")]
    public void Malformed_enrollment_codes_are_rejected_without_hashing(string? code)
    {
        Assert.Null(DeviceSecrets.NormalizeEnrollmentCode(code));
        Assert.Null(DeviceSecrets.HashEnrollmentCode(code));
    }

    [Fact]
    public void Every_spelling_of_a_code_has_the_same_hash()
    {
        var code = DeviceSecrets.NewEnrollmentCode();

        Assert.Equal(DeviceSecrets.HashEnrollmentCode(code), DeviceSecrets.HashEnrollmentCode(code.Replace("-", " ", StringComparison.Ordinal).ToLowerInvariant()));
    }
}
