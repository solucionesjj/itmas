using Itmas3.Agent.Collectors.Common;
using Itmas3.Agent.Collectors.Linux;
using Itmas3.Agent.Collectors.Model;

namespace Itmas3.Agent.Tests;

/// <summary>Firewall aggregation of the Linux source with scripted command outputs.</summary>
public sealed class LinuxSourceTests
{
    [Fact]
    public async Task CA11_firewall_is_active_when_any_tool_filters_input()
    {
        var processes = new FixtureProcessRunner()
            .Returns("/usr/sbin/ufw", ["status"], Fixtures.Read("linux/ufw-inactive.txt"))
            .Returns("/usr/sbin/iptables", ["-S", "INPUT"], Fixtures.Read("linux/iptables-input-rules.txt"));

        var firewall = await Source(processes).GetFirewallAsync(Fixtures.Ct);

        Assert.Equal(FirewallStatus.Active, firewall.Status);
        Assert.Equal([new FirewallDetail("ufw", false), new FirewallDetail("iptables", true)], firewall.Details);
    }

    [Fact]
    public async Task Firewall_is_inactive_when_tools_exist_but_none_filters()
    {
        var processes = new FixtureProcessRunner()
            .Returns("/usr/sbin/ufw", ["status"], Fixtures.Read("linux/ufw-inactive.txt"))
            .Returns("/usr/sbin/nft", ["-j", "list", "ruleset"], Fixtures.Read("linux/nft-ruleset-forward-only.json"));

        Assert.Equal(FirewallStatus.Inactive, (await Source(processes).GetFirewallAsync(Fixtures.Ct)).Status);
    }

    [Fact]
    public async Task Firewall_without_any_tool_is_unknown()
    {
        Assert.Equal(FirewallStatus.Unknown, (await Source(new FixtureProcessRunner()).GetFirewallAsync(Fixtures.Ct)).Status);
    }

    [Fact]
    public async Task Without_root_the_firewall_is_permission_denied()
    {
        var processes = new FixtureProcessRunner()
            .Returns("/usr/sbin/ufw", ["status"], string.Empty, 1, "ERROR: You need to be root to run this script");

        var exception = await Assert.ThrowsAsync<Itmas3.Agent.Collectors.CollectorException>(() => Source(processes).GetFirewallAsync(Fixtures.Ct));
        Assert.Equal(UnavailableReasons.PermissionDenied, exception.Reason);
    }

    [Fact]
    public async Task Applications_come_from_apt_mark_showmanual()
    {
        var processes = new FixtureProcessRunner().Returns("/usr/bin/apt-mark", ["showmanual"], "nginx\nopenssh-server\n\n");

        Assert.Equal(["nginx", "openssh-server"], await Source(processes).GetApplicationsAsync(Fixtures.Ct));
    }

    private static LinuxInventorySource Source(FixtureProcessRunner processes) =>
        new(processes, new SystemFiles(), TimeProvider.System);
}
