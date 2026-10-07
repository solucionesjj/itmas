using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itmas3.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping of the technical Data Protection key table. Every persisted type gets its own
/// <see cref="IEntityTypeConfiguration{TEntity}"/> in this folder (constitution §5).
/// </summary>
internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder)
    {
        builder.ToTable("data_protection_keys");
        builder.HasKey(key => key.Id);
    }
}
