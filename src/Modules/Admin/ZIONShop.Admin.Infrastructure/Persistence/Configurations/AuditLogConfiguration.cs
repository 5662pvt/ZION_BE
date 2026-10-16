using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Admin.Domain.Entities;

namespace ZIONShop.Admin.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.ResourceType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.ResourceId).HasMaxLength(100);
        builder.Property(a => a.ActorEmail).HasMaxLength(256);
        builder.Property(a => a.HttpMethod).HasMaxLength(10).IsRequired();
        builder.Property(a => a.Path).HasMaxLength(500).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.CorrelationId).HasMaxLength(64);
        builder.Property(a => a.TraceId).HasMaxLength(64);

        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => a.Action);
        builder.HasIndex(a => a.ActorId);
    }
}
