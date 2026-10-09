using Itmas3.Agent.Collectors;
using Itmas3.Agent.Collectors.Linux;
using Itmas3.Agent.Collectors.Model;

namespace Itmas3.Agent.Tests;

public sealed class LinuxParsersTests
{
    [Theory]
    [InlineData("ubuntu-24.04", "Ubuntu", "24.04")]
    [InlineData("ubuntu-22.04", "Ubuntu", "22.04")]
    [InlineData("debian-12", "Debian GNU/Linux", "12")]
    public void Os_release_of_supported_distributions(string fixture, string name, string version)
    {
        var release = LinuxParsers.ParseOsRelease(Fixtures.Read($"linux/os-release-{fixture}.txt"));

        Assert.Equal(name, release["NAME"]);
        Assert.Equal(version, release["VERSION_ID"]);
    }

    [Fact]
    public void Memory_comes_from_MemTotal_in_bytes() =>
        Assert.Equal(16303836L * 1024, LinuxParsers.ParseMemTotal(Fixtures.Read("linux/meminfo.txt")));

    [Fact]
    public void Lscpu_x86_reports_one_entry_per_socket_with_cores_and_threads()
    {
        var cpus = LinuxParsers.ParseLscpu(Fixtures.Read("linux/lscpu-x86-2-sockets.json"));

        Assert.Equal(2, cpus.Count);
        Assert.All(cpus, cpu => Assert.Equal(new CpuInfo("Intel(R) Xeon(R) Gold 6338 CPU @ 2.00GHz", 8, 16), cpu));
    }

    [Theory]
    [InlineData("ubuntu-24.04-arm64-vm")]
    [InlineData("ubuntu-22.04-arm64-vm")]
    [InlineData("debian-12-arm64-vm")]
    public void Lscpu_arm_virtual_machines_without_model_name_use_the_vendor(string fixture)
    {
        var cpu = Assert.Single(LinuxParsers.ParseLscpu(Fixtures.Read($"linux/lscpu-{fixture}.json")));

        Assert.Equal(new CpuInfo("Apple", 8, 8), cpu);
    }

    [Fact]
    public void CA10_lsblk_detects_luks_under_lvm_and_skips_removable_virtual_and_swap()
    {
        var (disks, volumes) = LinuxParsers.ParseLsblk(Fixtures.Read("linux/lsblk-luks-lvm.json"), mount => mount == "/" ? (900, 400) : null);

        Assert.Equal(["nvme0n1", "sda"], disks.Select(disk => disk.Id));
        Assert.Equal(new DiskInfo("nvme0n1", "Samsung SSD 980 PRO 1TB", 1000204886016, MediaTypes.Ssd, BusTypes.Nvme), disks[0]);
        Assert.Equal(new DiskInfo("sda", "ST4000NM000A-2HZ100", 4000787030016, MediaTypes.Hdd, BusTypes.Sata), disks[1]);

        Assert.Equal(["/", "/boot", "/srv/backups"], volumes.Select(volume => volume.MountPoint));
        var root = volumes[0];
        Assert.True(root.IsSystem);
        Assert.Equal((900L, 400L), (root.SizeBytes, root.FreeBytes));
        Assert.Equal(new EncryptionInfo(EncryptionStatus.Encrypted, EncryptionMethods.Luks), root.Encryption);
        Assert.Equal("nvme0n1", root.DiskId);
        Assert.Equal(EncryptionStatus.NotEncrypted, volumes[1].Encryption.Status);
        Assert.Equal("sda", volumes[2].DiskId);
    }

    [Fact]
    public void Lsblk_of_a_virtual_machine_reports_virtio_disks_as_virtual()
    {
        var (disks, _) = LinuxParsers.ParseLsblk(Fixtures.Read("linux/lsblk-ubuntu-24.04-arm64-vm.json"), _ => null);

        Assert.Equal(["vda", "vdb"], disks.Select(disk => disk.Id));
        Assert.All(disks, disk => Assert.Equal((BusTypes.Virtual, MediaTypes.Unknown), (disk.BusType, disk.MediaType)));
    }

    [Fact]
    public void CA09_users_have_groups_admin_flag_and_enabled_state()
    {
        var users = LinuxParsers.ParseUsers(
                Fixtures.Read("linux/passwd.txt"),
                Fixtures.Read("linux/group.txt"),
                LinuxParsers.ParseUidMin(Fixtures.Read("linux/login.defs.txt")),
                Fixtures.Read("linux/shadow.txt"),
                new DateOnly(2026, 10, 8))
            .ToDictionary(user => user.Name);

        Assert.Equal(["backup-op", "deploy", "operator2", "root", "svc-app"], users.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new[] { "adm", "deploy", "docker", "sudo" }, users["deploy"].Groups);
        Assert.True(users["deploy"].IsAdmin);
        Assert.True(users["operator2"].IsAdmin);
        Assert.True(users["root"].IsAdmin);
        Assert.False(users["backup-op"].IsAdmin);
        Assert.False(users["backup-op"].Enabled);
        Assert.False(users["svc-app"].Enabled);
        Assert.True(users["deploy"].Enabled);
    }

    [Fact]
    public void Uid_min_defaults_to_1000() => Assert.Equal(1000, LinuxParsers.ParseUidMin(null));

    [Fact]
    public void CA11_firewall_tools_are_interpreted()
    {
        Assert.True(LinuxParsers.ParseUfwStatus(Fixtures.Read("linux/ufw-active.txt")));
        Assert.False(LinuxParsers.ParseUfwStatus(Fixtures.Read("linux/ufw-inactive.txt")));
        Assert.Null(LinuxParsers.ParseUfwStatus("ERROR: You need to be root to run this script"));
        Assert.True(LinuxParsers.ParseNftHasInputRules(Fixtures.Read("linux/nft-ruleset-input.json")));
        Assert.False(LinuxParsers.ParseNftHasInputRules(Fixtures.Read("linux/nft-ruleset-forward-only.json")));
        Assert.True(LinuxParsers.ParseIptablesInputActive(Fixtures.Read("linux/iptables-input-rules.txt")));
        Assert.False(LinuxParsers.ParseIptablesInputActive(Fixtures.Read("linux/iptables-input-empty.txt")));
    }

    [Fact]
    public void Lscpu_without_cpus_is_an_error() =>
        Assert.Throws<CollectorException>(() => LinuxParsers.ParseLscpu("{\"lscpu\": []}"));
}
