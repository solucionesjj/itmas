using System.ComponentModel.DataAnnotations;
using Itmas3.Api.Contracts.Inventory;

namespace Itmas3.UnitTests.Api.Inventory;

public sealed class ContractValidationTests
{
    [Theory]
    [InlineData("nginx", true)]
    [InlineData("Microsoft Visual C++ 2015-2022 Redistributable (x64)", true)]
    [InlineData("Bogotá", true)]
    [InlineData("bad\u0000name", false)]
    [InlineData("bad\nname", false)]
    public void SafeText_rejects_control_characters(string value, bool expected)
    {
        Assert.Equal(expected, new SafeTextAttribute(256).IsValid(value));
    }

    [Fact]
    public void SafeText_enforces_length_and_count_on_collections()
    {
        var attribute = new SafeTextAttribute(5) { MaxCount = 2 };

        Assert.True(attribute.IsValid(new List<string> { "a", "bbbbb" }));
        Assert.False(attribute.IsValid(new List<string> { "a", "b", "c" }));
        Assert.False(attribute.IsValid(new List<string> { "bbbbbb" }));
        Assert.False(attribute.IsValid(new List<string?> { null }));
    }

    [Theory]
    [InlineData("203.0.113.5", true)]
    [InlineData("2001:db8::1", true)]
    [InlineData("not-an-ip", false)]
    [InlineData("<script>", false)]
    public void IpAddressText_accepts_only_ip_addresses(string value, bool expected)
    {
        Assert.Equal(expected, new IpAddressTextAttribute().IsValid(value));
    }

    [Fact]
    public void Null_sections_must_be_declared_unavailable_and_vice_versa()
    {
        Assert.Empty(Validate(Report(firewall: new FirewallInfo { Status = "active", Details = [] }, unavailable: [])));
        Assert.Empty(Validate(Report(firewall: null, unavailable: [new UnavailableSection { Section = "security.firewall", Reason = "timeout" }])));
        Assert.NotEmpty(Validate(Report(firewall: null, unavailable: [])));
        Assert.NotEmpty(Validate(Report(
            firewall: new FirewallInfo { Status = "active", Details = [] },
            unavailable: [new UnavailableSection { Section = "security.firewall", Reason = "timeout" }])));
    }

    private static List<ValidationResult> Validate(InventoryReportRequest request) =>
        request.Validate(new ValidationContext(request)).ToList();

    private static InventoryReportRequest Report(FirewallInfo? firewall, List<UnavailableSection> unavailable) => new()
    {
        SchemaVersion = 1,
        CollectedAt = DateTimeOffset.UtcNow,
        AgentVersion = "1.0.0",
        System = new SystemSection { Hostname = "pc", OsFamily = "linux", OsName = "Debian", OsVersion = "12", OsBuild = null, Architecture = "x64" },
        Hardware = new HardwareSection { MemoryBytes = 1, Cpus = [], Disks = [], Volumes = [] },
        Network = new NetworkSection { Interfaces = [], PublicNetwork = new PublicNetworkInfo { Ip = "203.0.113.5", Country = "CO", Region = null, City = null, Org = null } },
        Software = new SoftwareSection { Applications = [] },
        Accounts = new AccountsSection { Users = [] },
        Security = new SecuritySection { Firewall = firewall },
        Unavailable = unavailable,
    };
}
