using Itmas3.Agent.Collectors.MacOs;
using Itmas3.Agent.Collectors.Model;

namespace Itmas3.Agent.Tests;

public sealed class MacOsParsersTests
{
    [Fact]
    public void Sw_vers_and_platform_uuid()
    {
        var versions = MacOsParsers.ParseKeyValues(Fixtures.Read("macos/sw_vers.txt"));

        Assert.Equal("macOS", versions["ProductName"]);
        Assert.False(string.IsNullOrEmpty(versions["ProductVersion"]));
        Assert.Equal("00000000-1111-2222-3333-444444444444", MacOsParsers.ParsePlatformUuid(Fixtures.Read("macos/ioreg.txt")));
    }

    [Fact]
    public void Sysctl_gives_memory_and_cpu()
    {
        var (memory, cpus) = MacOsParsers.ParseSysctl(Fixtures.Read("macos/sysctl.txt"));

        Assert.Equal(17179869184, memory);
        Assert.Equal(new CpuInfo("Apple M1 Pro", 8, 8), Assert.Single(cpus));
    }

    [Fact]
    public void Internal_apple_ssd_is_an_nvme_ssd()
    {
        Assert.Equal(["disk0"], MacOsParsers.ParseWholeDisks(Fixtures.Read("macos/diskutil-list-physical.plist")));
        var disk = MacOsParsers.ParseDisk(Fixtures.Read("macos/diskutil-info-disk0.plist"));

        Assert.NotNull(disk);
        Assert.Equal(("disk0", MediaTypes.Ssd, BusTypes.Nvme), (disk.Id, disk.MediaType, disk.BusType));
        Assert.True(disk.SizeBytes > 0);
    }

    [Fact]
    public void Only_the_system_volume_and_user_volumes_are_reported()
    {
        var identifiers = MacOsParsers.ParseMountedVolumeIdentifiers(Fixtures.Read("macos/diskutil-list.plist"));

        // /System/Volumes/* and the Recovery helper volume mounted under /Volumes are skipped.
        Assert.Equal(["disk3s3s1"], identifiers);
    }

    [Fact]
    public void CA10_FileVault_system_volume_is_encrypted_on_its_physical_disk()
    {
        var volume = MacOsParsers.ParseVolume(Fixtures.Read("macos/diskutil-info-root.plist"), "FileVault is On.");

        Assert.NotNull(volume);
        Assert.True(volume.IsSystem);
        Assert.Equal("/", volume.MountPoint);
        Assert.Equal("disk0", volume.DiskId);
        Assert.Equal(new EncryptionInfo(EncryptionStatus.Encrypted, EncryptionMethods.FileVault), volume.Encryption);
        Assert.True(volume.FreeBytes > 0 && volume.FreeBytes < volume.SizeBytes);
    }

    [Fact]
    public void Without_FileVault_the_volume_is_not_encrypted_even_if_hardware_encrypted()
    {
        var plist = Fixtures.Read("macos/diskutil-info-root.plist")
            .Replace("<key>FileVault</key>\n\t<true/>", "<key>FileVault</key>\n\t<false/>", StringComparison.Ordinal);

        Assert.Equal(EncryptionStatus.NotEncrypted, MacOsParsers.ParseVolume(plist, "FileVault is Off.")!.Encryption.Status);
        Assert.Equal(EncryptionStatus.Encrypting, MacOsParsers.ParseVolume(plist, "Encryption in progress: Percent completed = 12")!.Encryption.Status);
    }

    [Fact]
    public void CA09_users_are_real_accounts_and_disabled_flag_is_read()
    {
        Assert.Equal(["jdoe", "localadmin"], MacOsParsers.ParseUserList(Fixtures.Read("macos/dscl-users.txt")));
        Assert.False(MacOsParsers.ParseEnabled(Fixtures.Read("macos/dscl-disabled.txt")));
        Assert.True(MacOsParsers.ParseEnabled("AuthenticationAuthority: ;ShadowHash;HASHLIST:<SALTED-SHA512-PBKDF2> ;SecureToken;"));
    }

    [Fact]
    public void CA11_application_firewall_state()
    {
        Assert.True(MacOsParsers.ParseApplicationFirewall(Fixtures.Read("macos/socketfilterfw-enabled.txt")));
        Assert.False(MacOsParsers.ParseApplicationFirewall(Fixtures.Read("macos/socketfilterfw-disabled.txt")));
        Assert.True(MacOsParsers.ParsePfStatus("Status: Enabled for 0 days 00:10:00"));
    }

    [Theory]
    [InlineData("jdoe", true)]
    [InlineData("first.last", true)]
    [InlineData("bad name", false)]
    [InlineData("../etc", false)]
    [InlineData("-rf", true)]
    public void Only_safe_user_names_are_passed_as_arguments(string name, bool safe) =>
        Assert.Equal(safe, MacOsParsers.IsSafeUserName(name));

    [Fact]
    public void Plist_parser_ignores_external_entities()
    {
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE plist [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
            <plist version="1.0"><dict><key>A</key><string>&xxe;</string></dict></plist>
            """;

        Assert.ThrowsAny<Exception>(() => Plist.ParseDictionary(xml).String("A")!.Contains("root", StringComparison.Ordinal));
    }
}
