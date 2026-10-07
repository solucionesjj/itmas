using Itmas3.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itmas3.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventRecord>
{
    public void Configure(EntityTypeBuilder<AuditEventRecord> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(auditEvent => auditEvent.Id);
        builder.Property(auditEvent => auditEvent.Id).ValueGeneratedNever();
        builder.Property(auditEvent => auditEvent.ActorDisplayName).HasMaxLength(200);
        builder.Property(auditEvent => auditEvent.Action).HasMaxLength(100);
        builder.Property(auditEvent => auditEvent.ResourceType).HasMaxLength(50);
        builder.Property(auditEvent => auditEvent.ResourceId).HasMaxLength(200);
        builder.Property(auditEvent => auditEvent.Result).HasMaxLength(20);
        builder.Property(auditEvent => auditEvent.Before).HasColumnType("jsonb");
        builder.Property(auditEvent => auditEvent.After).HasColumnType("jsonb");
        builder.Property(auditEvent => auditEvent.TraceId).HasMaxLength(64);
        builder.HasIndex(auditEvent => auditEvent.OccurredAt).IsDescending();
        builder.HasIndex(auditEvent => new { auditEvent.ResourceType, auditEvent.ResourceId });
        builder.HasIndex(auditEvent => auditEvent.ActorUserId);
    }
}
