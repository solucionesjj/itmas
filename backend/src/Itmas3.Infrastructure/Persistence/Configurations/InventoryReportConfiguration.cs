using Itmas3.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itmas3.Infrastructure.Persistence.Configurations;

internal sealed class InventoryReportConfiguration : IEntityTypeConfiguration<InventoryReport>
{
    public void Configure(EntityTypeBuilder<InventoryReport> builder)
    {
        builder.ToTable("inventory_reports");
        builder.HasKey(report => report.Id);
        builder.Property(report => report.Id).ValueGeneratedNever();
        builder.Property(report => report.ConnectionIp).HasMaxLength(Device.MaxIpLength);
        builder.Property(report => report.PublicIp).HasMaxLength(Device.MaxIpLength);
        builder.Property(report => report.CountryCode).HasMaxLength(2).IsFixedLength();
        builder.Property(report => report.Region).HasMaxLength(Device.MaxTextLength);
        builder.Property(report => report.City).HasMaxLength(Device.MaxTextLength);
        builder.Property(report => report.Payload).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(report => new { report.DeviceId, report.CollectedAt }).IsDescending(false, true);
        builder.HasIndex(report => report.CollectedAt);
        builder.HasOne<Device>().WithMany().HasForeignKey(report => report.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}
