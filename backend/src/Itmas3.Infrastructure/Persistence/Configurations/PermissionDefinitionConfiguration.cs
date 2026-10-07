using Itmas3.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itmas3.Infrastructure.Persistence.Configurations;

internal sealed class PermissionDefinitionConfiguration : IEntityTypeConfiguration<PermissionDefinition>
{
    public void Configure(EntityTypeBuilder<PermissionDefinition> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(permission => permission.Id);
        builder.Property(permission => permission.Id).HasColumnName("name").HasMaxLength(150).ValueGeneratedNever();
        builder.Property(permission => permission.DisplayName).HasMaxLength(PermissionDefinition.MaxDisplayNameLength).IsRequired();
        builder.Property(permission => permission.Description).HasMaxLength(PermissionDefinition.MaxDescriptionLength).IsRequired();
        builder.Property(permission => permission.Category).HasMaxLength(PermissionDefinition.MaxCategoryLength).IsRequired();
        builder.Ignore(permission => permission.Name);
        builder.Ignore(permission => permission.IsAssignable);
    }
}
