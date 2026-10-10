using Itmas3.Application.Inventory;
using Itmas3.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itmas3.Infrastructure.Persistence.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    /// <summary>Partial indexes: only devices that are not deleted keep their name and hardware id (SPEC-0003 RN-1, RN-4).</summary>
    public const string NotDeletedFilter = "status <> 'deleted'";

    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");
        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id).ValueGeneratedNever();

        // Registry (SPEC-0003)
        builder.Property(device => device.Name).HasMaxLength(Device.MaxNameLength).IsRequired();
        builder.Property(device => device.Type)
            .HasColumnName("device_type")
            .HasConversion(type => DeviceTypes.ToCode(type), code => code == DeviceTypes.Server ? DeviceType.Server : DeviceType.Workstation)
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(device => device.Status)
            .HasConversion(
                status => DeviceStatuses.ToCode(status),
                code => code == DeviceStatuses.Active ? DeviceStatus.Active : code == DeviceStatuses.Deleted ? DeviceStatus.Deleted : DeviceStatus.Pending)
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(device => device.OwnerEmail).HasMaxLength(Device.MaxEmailLength);
        builder.Property(device => device.EnrollmentCodeHash).HasMaxLength(Device.SecretHashLength).IsFixedLength();
        // Revision is the user-facing ETag; xmin protects every write (e.g. a one-time code used twice concurrently).
        builder.Property<uint>("RowVersion").IsRowVersion();

        // Enrollment and summary of the latest inventory (SPEC-0002)
        builder.Property(device => device.HardwareId).HasMaxLength(Device.MaxHardwareIdLength);
        builder.Property(device => device.ApiKeyHash).HasMaxLength(Device.SecretHashLength).IsFixedLength();
        builder.Property(device => device.EnrolledFromIp).HasMaxLength(Device.MaxIpLength);
        builder.Property(device => device.TermsVersion).HasMaxLength(20);
        builder.Property(device => device.LastConnectionIp).HasMaxLength(Device.MaxIpLength);
        builder.Property(device => device.Hostname).HasMaxLength(Device.MaxTextLength);
        builder.Property(device => device.OsFamily).HasMaxLength(20);
        builder.Property(device => device.OsName).HasMaxLength(Device.MaxTextLength);
        builder.Property(device => device.OsVersion).HasMaxLength(Device.MaxTextLength);
        builder.Property(device => device.AgentVersion).HasMaxLength(50);
        builder.Property(device => device.PublicIp).HasMaxLength(Device.MaxIpLength);
        builder.Property(device => device.CountryCode).HasMaxLength(2).IsFixedLength();
        builder.Property(device => device.Region).HasMaxLength(Device.MaxTextLength);
        builder.Property(device => device.City).HasMaxLength(Device.MaxTextLength);
        builder.Property(device => device.SystemVolumeEncryption).HasMaxLength(20);
        builder.Property(device => device.FirewallStatus).HasMaxLength(20);

        builder.HasIndex(device => device.HardwareId).IsUnique().HasFilter(NotDeletedFilter);
        builder.HasIndex(device => device.ApiKeyHash).IsUnique();
        builder.HasIndex(device => device.EnrollmentCodeHash).IsUnique();
        builder.HasIndex(device => device.Hostname);
        builder.HasIndex(device => device.Status);
        // Also created in the migration: unique lower(name) WHERE not deleted, and lower(owner_email).

        // Latest report pointer; SET NULL so purging or deleting a report never breaks the device.
        builder.HasOne<InventoryReport>().WithMany().HasForeignKey(device => device.LastReportId).OnDelete(DeleteBehavior.SetNull);
    }
}
