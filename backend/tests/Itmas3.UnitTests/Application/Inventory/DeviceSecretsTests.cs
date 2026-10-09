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
    public void Fixed_time_comparison_matches_only_equal_values()
    {
        Assert.True(DeviceSecrets.FixedTimeEquals("abc", "abc"));
        Assert.False(DeviceSecrets.FixedTimeEquals("abc", "abd"));
        Assert.False(DeviceSecrets.FixedTimeEquals("abc", "abcd"));
    }
}
