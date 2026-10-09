using Itmas3.Agent.Collectors.Model;
using Itmas3.Agent.Collectors.Windows;

namespace Itmas3.Agent.Tests;

public sealed class WindowsParsersTests
{
    [Theory]
    [InlineData("Windows 10 Pro", 22631, "Client", "Windows 11 Pro")]
    [InlineData("Windows 10 Enterprise", 19045, "Client", "Windows 10 Enterprise")]
    [InlineData("Windows Server 2022 Standard", 20348, "Server", "Windows Server 2022 Standard")]
    [InlineData("Windows Server 2025 Datacenter", 26100, "Server", "Windows Server 2025 Datacenter")]
    public void Windows_11_is_recognised_by_build(string productName, int build, string installationType, string expected) =>
        Assert.Equal(expected, WindowsParsers.OsName(productName, build, installationType));

    [Theory]
    [InlineData("7-Zip 23.01 (x64)", null, null, null, true)]
    [InlineData("", null, null, null, false)]
    [InlineData("Microsoft Visual C++ Runtime", 1, null, null, false)]
    [InlineData("Security Update for Office", null, "Office16", null, false)]
    [InlineData("Update for Windows", null, null, "Update", false)]
    public void CA08_installed_apps_exclude_system_components_and_updates(string? name, int? systemComponent, string? parent, string? releaseType, bool listed) =>
        Assert.Equal(listed, WindowsParsers.IsListedApplication(new UninstallEntry(name, systemComponent, parent, releaseType)));

    [Theory]
    [InlineData(17, BusTypes.Nvme)]
    [InlineData(11, BusTypes.Sata)]
    [InlineData(10, BusTypes.Sas)]
    [InlineData(15, BusTypes.Virtual)]
    [InlineData(7, null)]
    [InlineData(12, null)]
    [InlineData(0, BusTypes.Unknown)]
    public void Bus_types_map_and_removable_buses_are_excluded(int busType, string? expected) =>
        Assert.Equal(expected, WindowsParsers.BusType(busType));

    [Theory]
    [InlineData(1, 1, EncryptionStatus.Encrypted)]
    [InlineData(0, 1, EncryptionStatus.Suspended)]
    [InlineData(0, 2, EncryptionStatus.Encrypting)]
    [InlineData(0, 0, EncryptionStatus.NotEncrypted)]
    [InlineData(null, null, EncryptionStatus.Unknown)]
    public void CA10_bitlocker_states(int? protection, int? conversion, string expected) =>
        Assert.Equal(expected, WindowsParsers.BitLocker(protection, conversion).Status);

    [Fact]
    public void CA11_firewall_requires_all_three_profiles_or_an_active_third_party_product()
    {
        FirewallDetail[] allOn = [new("dominio", true), new("privado", true), new("público", true)];
        FirewallDetail[] publicOff = [new("dominio", true), new("privado", true), new("público", false)];

        Assert.Equal(FirewallStatus.Active, WindowsParsers.FirewallStatus(allOn, []));
        Assert.Equal(FirewallStatus.Inactive, WindowsParsers.FirewallStatus(publicOff, []));
        Assert.Equal(FirewallStatus.Active, WindowsParsers.FirewallStatus(publicOff, [new FirewallDetail("ESET", true)]));
        Assert.Equal(FirewallStatus.Unknown, WindowsParsers.FirewallStatus([], []));
    }

    [Theory]
    [InlineData(0x41000u, true)]
    [InlineData(0x61100u, true)]
    [InlineData(0x40000u, false)]
    [InlineData(0x60100u, false)]
    public void Third_party_firewall_state_is_decoded(uint productState, bool enabled) =>
        Assert.Equal(enabled, WindowsParsers.ThirdPartyFirewallEnabled(productState));

    [Fact]
    public void CA09_admins_include_members_through_nested_local_groups()
    {
        LocalPrincipal[] principals =
        [
            new(@"PC\Admin", "Admin", false, true, "S-1-5-21-1-500", []),
            new(@"PC\ana", "ana", false, false, "S-1-5-21-1-1001", []),
            new(@"PC\luis", "luis", false, false, "S-1-5-21-1-1002", []),
            new(@"PC\Administradores", "Administradores", true, false, WindowsParsers.AdministratorsSid, [@"PC\Admin", @"PC\Soporte", @"CORP\Domain Admins"]),
            new(@"PC\Soporte", "Soporte", true, false, "S-1-5-21-1-2000", [@"PC\ana"]),
            new(@"PC\Usuarios", "Usuarios", true, false, "S-1-5-32-545", [@"PC\ana", @"PC\luis"]),
        ];

        var users = WindowsParsers.ResolveUsers(principals).ToDictionary(user => user.Name);

        Assert.True(users["Admin"].IsAdmin);
        Assert.False(users["Admin"].Enabled);
        Assert.True(users["ana"].IsAdmin);
        Assert.Equal(["Administradores", "Soporte", "Usuarios"], users["ana"].Groups);
        Assert.False(users["luis"].IsAdmin);
    }

    [Fact]
    public void Wql_literals_are_escaped() =>
        Assert.Equal(@"O\'Brien\\Team", WindowsParsers.WqlLiteral(@"O'Brien\Team"));

    [Theory]
    [InlineData("4C4C4544-0042-3510-8048-B4C04F4B4E32", true)]
    [InlineData("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF", false)]
    [InlineData("00000000-0000-0000-0000-000000000000", false)]
    [InlineData(null, false)]
    public void Placeholder_smbios_uuids_are_ignored(string? uuid, bool usable) =>
        Assert.Equal(usable, WindowsParsers.IsUsableUuid(uuid));
}
