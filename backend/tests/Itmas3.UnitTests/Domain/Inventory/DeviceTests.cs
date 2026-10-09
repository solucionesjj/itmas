using Itmas3.Application.Inventory;
using Itmas3.Domain.Common;
using Itmas3.Domain.Inventory;

namespace Itmas3.UnitTests.Domain.Inventory;

public sealed class DeviceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Enroll_stores_only_the_hash_and_enrollment_data()
    {
        var hash = DeviceSecrets.Hash("key");
        var device = Device.Enroll("hw-0001-abcd", hash, Enrollment("pc-01"), Now);

        Assert.Equal(hash, device.ApiKeyHash);
        Assert.Equal("pc-01", device.Hostname);
        Assert.Equal("1", device.TermsVersion);
        Assert.Equal("198.51.100.7", device.EnrolledFromIp);
        Assert.Null(device.LastReportId);
    }

    [Fact]
    public void RN04_relink_replaces_the_api_key_hash()
    {
        var device = Device.Enroll("hw-0001-abcd", DeviceSecrets.Hash("old"), Enrollment("pc-01"), Now);

        device.Relink(DeviceSecrets.Hash("new"), Enrollment("pc-01-renamed"), Now.AddDays(1));

        Assert.Equal(DeviceSecrets.Hash("new"), device.ApiKeyHash);
        Assert.Equal("pc-01-renamed", device.Hostname);
    }

    [Fact]
    public void RN05_older_reports_do_not_replace_the_latest_summary()
    {
        var device = Device.Enroll("hw-0001-abcd", DeviceSecrets.Hash("key"), Enrollment("pc-01"), Now);
        var latest = Report(device, Now.AddHours(2));
        var older = Report(device, Now.AddHours(1));

        Assert.True(device.ApplyReport(latest, Summary("pc-latest")));
        Assert.False(device.ApplyReport(older, Summary("pc-older")));

        Assert.Equal(latest.Id, device.LastReportId);
        Assert.Equal("pc-latest", device.Hostname);
        Assert.Equal(older.ReceivedAt, device.LastSeenAt);
    }

    [Fact]
    public void Reports_of_another_device_are_rejected()
    {
        var device = Device.Enroll("hw-0001-abcd", DeviceSecrets.Hash("key"), Enrollment("pc-01"), Now);
        var other = Device.Enroll("hw-0002-abcd", DeviceSecrets.Hash("key2"), Enrollment("pc-02"), Now);

        Assert.Throws<DomainException>(() => device.ApplyReport(Report(other, Now), Summary("x")));
    }

    [Fact]
    public void Report_keeps_both_ips()
    {
        var device = Device.Enroll("hw-0001-abcd", DeviceSecrets.Hash("key"), Enrollment("pc-01"), Now);
        var report = InventoryReport.Receive(device.Id, Now, Now, "192.0.2.10", new DeclaredPublicNetwork("203.0.113.5", "CO", "Antioquia", "Medellín"), "{}");

        device.ApplyReport(report, Summary("pc-01"));

        Assert.Equal("192.0.2.10", device.LastConnectionIp);
        Assert.Equal("203.0.113.5", device.PublicIp);
        Assert.Equal("CO", device.CountryCode);
        Assert.Equal("Medellín", device.City);
    }

    private static DeviceEnrollmentData Enrollment(string hostname) => new(hostname, "linux", "1.0.0", "1", Now, "198.51.100.7");

    private static InventoryReport Report(Device device, DateTimeOffset collectedAt) =>
        InventoryReport.Receive(device.Id, collectedAt, collectedAt.AddMinutes(1), "192.0.2.10", null, "{}");

    private static DeviceSummary Summary(string hostname) => new(hostname, "linux", "Ubuntu", "24.04", "1.0.0", "encrypted", "active", true);
}
