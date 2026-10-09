using Itmas3.Agent.Collectors;
using Itmas3.Agent.Collectors.Model;

namespace Itmas3.Agent.Tests;

public sealed class InventoryCollectorTests
{
    [Fact]
    public async Task CA07_complete_inventory_has_every_section()
    {
        var report = await ContractTests.Collector(new FakePlatform()).CollectAsync("1.2.3", Fixtures.Ct);

        Assert.Equal("1.2.3", report.AgentVersion);
        Assert.NotNull(report.Hardware.MemoryBytes);
        Assert.NotNull(report.Hardware.Cpus);
        Assert.NotNull(report.Network.PublicNetwork);
        Assert.NotNull(report.Security.Firewall);
        Assert.Empty(report.Unavailable);
    }

    [Fact]
    public async Task CA08_applications_are_unique_sorted_names_without_control_characters()
    {
        var report = await ContractTests.Collector(new FakePlatform()).CollectAsync("1.0.0", Fixtures.Ct);

        Assert.Equal(["BadName", "nginx", "openssh-server"], report.Software.Applications);
    }

    [Theory]
    [InlineData("permission-denied")]
    [InlineData("timeout")]
    [InlineData("not-supported")]
    [InlineData("error")]
    public async Task RF09_a_failing_section_is_reported_unavailable_with_its_reason(string reason)
    {
        Exception failure = reason switch
        {
            "permission-denied" => new UnauthorizedAccessException(),
            "timeout" => new CollectorException(UnavailableReasons.Timeout, "timeout"),
            "not-supported" => new PlatformNotSupportedException(),
            _ => new InvalidOperationException("boom"),
        };

        var report = await ContractTests.Collector(new FakePlatform { UsersFailure = failure }).CollectAsync("1.0.0", Fixtures.Ct);

        Assert.Null(report.Accounts.Users);
        Assert.Equal(new UnavailableSection(Sections.Users, reason), Assert.Single(report.Unavailable));
        Assert.NotNull(report.Security.Firewall);
    }

    [Fact]
    public async Task CA12_ipinfo_failure_does_not_block_the_report()
    {
        var report = await ContractTests.Collector(new FakePlatform(), new CollectorException(UnavailableReasons.Timeout, "slow"))
            .CollectAsync("1.0.0", Fixtures.Ct);

        Assert.Null(report.Network.PublicNetwork);
        Assert.Equal(new UnavailableSection(Sections.PublicNetwork, UnavailableReasons.Timeout), Assert.Single(report.Unavailable));
    }

    [Fact]
    public async Task Cancellation_of_the_whole_collection_is_not_hidden()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var platform = new FakePlatform { UsersFailure = new OperationCanceledException(cancelled.Token) };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ContractTests.Collector(platform).CollectAsync("1.0.0", cancelled.Token));
    }
}
