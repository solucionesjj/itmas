using Itmas3.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itmas3.Infrastructure.Persistence.Configurations;

internal sealed class AuthorizationStateConfiguration : IEntityTypeConfiguration<AuthorizationState>
{
    public void Configure(EntityTypeBuilder<AuthorizationState> builder)
    {
        builder.ToTable("authorization_state");
        builder.HasKey(state => state.Id);
        builder.Property(state => state.Id).ValueGeneratedNever();
        builder.HasData(new AuthorizationState { Id = AuthorizationState.SingletonId, Version = 1 });
    }
}
