using Itmas3.Application.Inventory;
using Itmas3.Domain.Common;
using Itmas3.Domain.Inventory;
using Itmas3.UnitTests.Application.Inventory;

namespace Itmas3.UnitTests.Domain.Inventory;

public sealed class DeviceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RF01_register_creates_a_pending_device_with_a_code_hash()
    {
        var codeHash = DeviceSecrets.HashEnrollmentCode(DeviceSecrets.NewEnrollmentCode())!;

        var device = Device.Register("  Portátil de Ana  ", DeviceType.Workstation, " Ana.Perez@Example.COM ", codeHash, Now);

        Assert.Equal("Portátil de Ana", device.Name);
        Assert.Equal(DeviceType.Workstation, device.Type);
        Assert.Equal("ana.perez@example.com", device.OwnerEmail);
        Assert.Equal(DeviceStatus.Pending, device.Status);
        Assert.Equal(codeHash, device.EnrollmentCodeHash);
        Assert.Equal(Now, device.EnrollmentCodeIssuedAt);
        Assert.Equal(1, device.Revision);
        Assert.Null(device.ApiKeyHash);
        Assert.Null(device.HardwareId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pc\u0007")]
    public void RN01_names_are_required_and_safe(string name) =>
        Assert.Throws<DomainException>(() => TestDevices.Pending(name, Now));

    [Fact]
    public void RN01_names_are_limited_to_100_characters()
    {
        Assert.Equal(100, TestDevices.Pending(new string('a', 100), Now).Name.Length);
        Assert.Throws<DomainException>(() => TestDevices.Pending(new string('a', 101), Now));
    }

    [Theory]
    [InlineData("no-at-sign")]
    [InlineData("a@b")]
    [InlineData("a b@example.com")]
    [InlineData("a@@example.com")]
    public void RN02_owner_emails_must_be_well_formed(string email) =>
        Assert.Throws<DomainException>(() => Device.NormalizeEmail(email));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void RN02_owner_email_is_optional(string? email) => Assert.Null(Device.NormalizeEmail(email));

    [Fact]
    public void RN02b_the_type_must_be_known() =>
        Assert.Throws<DomainException>(() => Device.Register("pc", (DeviceType)7, null, DeviceSecrets.Hash("x"), Now));

    [Fact]
    public void RF07_enroll_links_the_hardware_stores_only_the_hash_and_consumes_the_code()
    {
        var device = TestDevices.Pending("pc", Now);
        var hash = DeviceSecrets.Hash("key");

        var relinked = device.Enroll("hw-0001-abcd", hash, Enrollment("pc-01"), Now);

        Assert.False(relinked);
        Assert.Equal(DeviceStatus.Active, device.Status);
        Assert.Equal("hw-0001-abcd", device.HardwareId);
        Assert.Equal(hash, device.ApiKeyHash);
        Assert.Null(device.EnrollmentCodeHash);
        Assert.Equal("pc-01", device.Hostname);
        Assert.Equal("1", device.TermsVersion);
        Assert.Equal("198.51.100.7", device.EnrolledFromIp);
    }

    [Fact]
    public void RN03_a_consumed_code_cannot_enroll_again()
    {
        var device = TestDevices.Active("hw-0001-abcd", Now);

        Assert.Throws<DomainException>(() => device.Enroll("hw-0001-abcd", DeviceSecrets.Hash("other"), Enrollment("pc-01"), Now));
    }

    [Fact]
    public void RF09_reenrollment_with_a_new_code_replaces_the_api_key_hash()
    {
        var device = TestDevices.Active("hw-0001-abcd", Now, apiKey: "old");
        device.IssueEnrollmentCode(DeviceSecrets.Hash("new-code"), Now.AddDays(1));

        var relinked = device.Enroll("hw-0001-abcd", DeviceSecrets.Hash("new"), Enrollment("pc-01-renamed"), Now.AddDays(1));

        Assert.True(relinked);
        Assert.Equal(DeviceSecrets.Hash("new"), device.ApiKeyHash);
        Assert.Equal("pc-01-renamed", device.Hostname);
    }

    [Fact]
    public void RF04_update_changes_the_user_data_and_the_revision()
    {
        var device = TestDevices.Pending("pc", Now);

        device.Update("srv-01", DeviceType.Server, "Owner@Example.com");

        Assert.Equal("srv-01", device.Name);
        Assert.Equal(DeviceType.Server, device.Type);
        Assert.Equal("owner@example.com", device.OwnerEmail);
        Assert.Equal(2, device.Revision);

        device.Update("srv-01", DeviceType.Server, null);
        Assert.Null(device.OwnerEmail);
    }

    [Fact]
    public void RF05_RN06_delete_revokes_the_code_and_the_api_key_and_is_idempotent()
    {
        var device = TestDevices.Active("hw-0001-abcd", Now);
        device.IssueEnrollmentCode(DeviceSecrets.Hash("pending-code"), Now);

        Assert.True(device.Delete(Now.AddHours(1)));
        Assert.False(device.Delete(Now.AddHours(2)));

        Assert.Equal(DeviceStatus.Deleted, device.Status);
        Assert.Equal(Now.AddHours(1), device.DeletedAt);
        Assert.Null(device.ApiKeyHash);
        Assert.Null(device.EnrollmentCodeHash);
        Assert.Equal("hw-0001-abcd", device.HardwareId);
    }

    [Fact]
    public void RN05_deleted_devices_cannot_change_enroll_or_receive_reports()
    {
        var device = TestDevices.Active("hw-0001-abcd", Now);
        device.Delete(Now);

        Assert.Throws<DomainException>(() => device.Update("x", DeviceType.Server, null));
        Assert.Throws<DomainException>(() => device.IssueEnrollmentCode(DeviceSecrets.Hash("code"), Now));
        Assert.Throws<DomainException>(() => device.Enroll("hw-0001-abcd", DeviceSecrets.Hash("k"), Enrollment("pc"), Now));
        Assert.Throws<DomainException>(() => device.ApplyReport(Report(device, Now), Summary("pc")));
    }

    [Fact]
    public void Pending_devices_do_not_receive_reports()
    {
        var device = TestDevices.Pending("pc", Now);

        Assert.Throws<DomainException>(() => device.ApplyReport(Report(device, Now), Summary("pc")));
    }

    [Fact]
    public void RN05_older_reports_do_not_replace_the_latest_summary()
    {
        var device = TestDevices.Active("hw-0001-abcd", Now);
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
        var device = TestDevices.Active("hw-0001-abcd", Now);
        var other = TestDevices.Active("hw-0002-abcd", Now, apiKey: "key2");

        Assert.Throws<DomainException>(() => device.ApplyReport(Report(other, Now), Summary("x")));
    }

    [Fact]
    public void Report_keeps_both_ips_the_agent_version_and_the_unavailable_count()
    {
        var device = TestDevices.Active("hw-0001-abcd", Now);
        var report = InventoryReport.Receive(
            device.Id, Now, Now, "192.0.2.10", new DeclaredPublicNetwork("203.0.113.5", "CO", "Antioquia", "Medellín"), "1.1.0", 2, "{}");

        device.ApplyReport(report, Summary("pc-01"));

        Assert.Equal("192.0.2.10", device.LastConnectionIp);
        Assert.Equal("203.0.113.5", device.PublicIp);
        Assert.Equal("CO", device.CountryCode);
        Assert.Equal("Medellín", device.City);
        Assert.Equal("1.1.0", report.AgentVersion);
        Assert.Equal(2, report.UnavailableCount);
    }

    private static DeviceEnrollmentData Enrollment(string hostname) => new(hostname, "linux", "1.0.0", "1", Now, "198.51.100.7");

    private static InventoryReport Report(Device device, DateTimeOffset collectedAt) =>
        InventoryReport.Receive(device.Id, collectedAt, collectedAt.AddMinutes(1), "192.0.2.10", null, "1.0.0", 0, "{}");

    private static DeviceSummary Summary(string hostname) => new(hostname, "linux", "Ubuntu", "24.04", "1.0.0", "encrypted", "active", true);
}
