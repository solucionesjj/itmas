using Itmas3.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itmas3.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.DisplayName).HasMaxLength(User.MaxDisplayNameLength).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(User.MaxEmailLength);
        builder.Property(user => user.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.HasIndex(user => user.IsRoot);

        builder.OwnsMany(user => user.Identities, identities =>
        {
            identities.ToTable("user_external_identities");
            identities.WithOwner().HasForeignKey("UserId");
            identities.Property<long>("Id").UseIdentityByDefaultColumn();
            identities.HasKey("Id");
            identities.Property(identity => identity.Provider).HasMaxLength(50).IsRequired();
            identities.Property(identity => identity.Issuer).HasMaxLength(200).IsRequired();
            identities.Property(identity => identity.Subject).HasMaxLength(200).IsRequired();
            identities.HasIndex(identity => new { identity.Provider, identity.Issuer, identity.Subject }).IsUnique();
        });
        builder.Navigation(user => user.Identities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(user => user.PolicyAssignments, assignments =>
        {
            assignments.ToTable("policy_assignments");
            assignments.WithOwner().HasForeignKey("UserId");
            assignments.HasKey("UserId", nameof(PolicyAssignment.PolicyId));
            assignments.HasIndex(assignment => assignment.PolicyId);
            assignments.HasOne<Policy>().WithMany().HasForeignKey(assignment => assignment.PolicyId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(user => user.PolicyAssignments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
