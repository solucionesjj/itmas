using Itmas3.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itmas3.Infrastructure.Persistence.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");
        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id).ValueGeneratedNever();
        builder.Property(device => device.HardwareId).HasMaxLength(Device.MaxHardwareIdLength).IsRequired();
        builder.Property(device => device.ApiKeyHash).HasMaxLength(Device.ApiKeyHashLength).IsFixedLength().IsRequired();
        builder.Property(device => device.EnrolledFromIp).HasMaxLength(Device.MaxIpLength);
        builder.Property(device => device.TermsVersion).HasMaxLength(20).IsRequired();
        builder.Property(device => device.LastConnectionIp).HasMaxLength(Device.MaxIpLength);
        builder.Property(device => device.Hostname).HasMaxLength(Device.MaxTextLength).IsRequired();
        builder.Property(device => device.OsFamily).HasMaxLength(20).IsRequired();
        builder.Property(device => device.OsName).HasMaxLength(Device.MaxTextLength);
        builder.Property(device => device.OsVersion).HasMaxLength(Device.MaxTextLength);
        builder.Property(device => device.AgentVersion).HasMaxLength(50).IsRequired();
        builder.Property(device => device.PublicIp).HasMaxLength(Device.MaxIpLength);
        builder.Property(device => device.CountryCode).HasMaxLength(2).IsFixedLength();
        builder.Property(device => device.Region).HasMaxLength(Device.MaxTextLength);
        builder.Property(device => device.City).HasMaxLength(Device.MaxTextLength);
        builder.Property(device => device.SystemVolumeEncryption).HasMaxLength(20);
        builder.Property(device => device.FirewallStatus).HasMaxLength(20);

        builder.HasIndex(device => device.HardwareId).IsUnique();
        builder.HasIndex(device => device.ApiKeyHash).IsUnique();
        builder.HasIndex(device => device.Hostname);

        // Latest report pointer; SET NULL so purging or deleting a report never breaks the device.
        builder.HasOne<InventoryReport>().WithMany().HasForeignKey(device => device.LastReportId).OnDelete(DeleteBehavior.SetNull);
    }
}
