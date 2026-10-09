using System.Net;
using Itmas3.Agent.Collectors;
using Itmas3.Agent.Collectors.Common;
using Itmas3.Agent.Collectors.Model;

namespace Itmas3.Agent.Tests;

public sealed class NetworkTests
{
    [Fact]
    public void Local_ips_exclude_loopback_and_link_local()
    {
        var description = NetworkInterfaceSource.Describe("eth0",
        [
            IPAddress.Parse("10.0.0.15"), IPAddress.Parse("127.0.0.1"), IPAddress.Parse("169.254.10.1"),
            IPAddress.Parse("2001:db8::15"), IPAddress.Parse("fe80::1%2"), IPAddress.IPv6Loopback,
        ]);

        Assert.NotNull(description);
        Assert.Equal(["10.0.0.15"], description.Ipv4);
        Assert.Equal(["2001:db8::15"], description.Ipv6);
        Assert.Null(NetworkInterfaceSource.Describe("lo", [IPAddress.Loopback]));
    }

    [Fact]
    public void CA12_ipinfo_response_is_reduced_to_five_validated_fields() =>
        Assert.Equal(
            new PublicNetworkInfo("172.96.143.36", "US", "Florida", "Miami", "AS23470 ReliableSite.Net LLC"),
            PublicNetworkSource.Parse(Fixtures.Read("ipinfo/ipinfo.json")));

    [Theory]
    [InlineData("ipinfo/bogon.json")]
    [InlineData(null)]
    public void Unusable_ipinfo_answers_are_rejected(string? fixture)
    {
        var json = fixture is null ? """{"ip":"<script>","country":"US"}""" : Fixtures.Read(fixture);

        Assert.Throws<CollectorException>(() => PublicNetworkSource.Parse(json));
        Assert.Throws<CollectorException>(() => PublicNetworkSource.Parse("not json"));
    }

    [Fact]
    public async Task Ipinfo_is_called_anonymously_over_https()
    {
        var handler = StubHandler.Returning(HttpStatusCode.OK, Fixtures.Read("ipinfo/ipinfo.json"));
        using var client = new HttpClient(handler);

        var info = await new PublicNetworkSource(client).GetAsync(Fixtures.Ct);

        Assert.Equal("172.96.143.36", info.Ip);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://ipinfo.io/json"), request.RequestUri);
        Assert.Null(request.Headers.Authorization);
        Assert.Equal(string.Empty, handler.Bodies[0]);
    }

    [Fact]
    public async Task Ipinfo_errors_become_unavailable_reasons()
    {
        using var rateLimited = new HttpClient(StubHandler.Returning(HttpStatusCode.TooManyRequests));
        using var unreachable = new HttpClient(new StubHandler((_, _) => throw new HttpRequestException("dns")));

        Assert.Equal(UnavailableReasons.Error, (await Assert.ThrowsAsync<CollectorException>(() => new PublicNetworkSource(rateLimited).GetAsync(Fixtures.Ct))).Reason);
        Assert.Equal(UnavailableReasons.Error, (await Assert.ThrowsAsync<CollectorException>(() => new PublicNetworkSource(unreachable).GetAsync(Fixtures.Ct))).Reason);
    }
}
