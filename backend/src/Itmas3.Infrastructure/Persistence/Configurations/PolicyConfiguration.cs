using Itmas3.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itmas3.Infrastructure.Persistence.Configurations;

internal sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("policies");
        builder.HasKey(policy => policy.Id);
        builder.Property(policy => policy.Id).ValueGeneratedNever();
        builder.Property(policy => policy.Name).HasMaxLength(Policy.MaxNameLength).IsRequired();
        builder.Property(policy => policy.Description).HasMaxLength(Policy.MaxDescriptionLength).IsRequired();
        builder.Property(policy => policy.Revision).IsConcurrencyToken();
        builder.Ignore(policy => policy.PermissionNames);
        // Case-insensitive unique name: unique index on lower(name), created in the migration.

        builder.OwnsMany(policy => policy.Statements, statements =>
        {
            statements.ToTable("policy_statements");
            statements.WithOwner().HasForeignKey("PolicyId");
            statements.HasKey("PolicyId", nameof(PolicyStatement.PermissionName));
            statements.Property(statement => statement.PermissionName).HasMaxLength(150);
            statements.Property(statement => statement.Effect).HasConversion<string>().HasMaxLength(20);
            statements.HasIndex(statement => statement.PermissionName);
            statements.HasOne<PermissionDefinition>().WithMany().HasForeignKey(statement => statement.PermissionName).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(policy => policy.Statements).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
