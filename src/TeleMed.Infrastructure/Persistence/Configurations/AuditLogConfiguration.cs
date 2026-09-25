using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.Property(a => a.Id).UseSerialColumn();
        builder.Property(a => a.Changes).HasColumnType("jsonb");
        builder.HasIndex(a => new { a.EntityType, a.EntityId, a.CreatedAt });
        builder.HasIndex(a => new { a.ActorId, a.CreatedAt });
        builder.HasIndex(a => a.CreatedAt).HasMethod("brin");
    }
}
