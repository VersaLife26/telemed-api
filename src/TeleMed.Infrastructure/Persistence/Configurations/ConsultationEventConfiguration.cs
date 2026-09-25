using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class ConsultationEventConfiguration : IEntityTypeConfiguration<ConsultationEvent>
{
    public void Configure(EntityTypeBuilder<ConsultationEvent> builder)
    {
        builder.ToTable("consultation_events");
        builder.Property(e => e.Id).UseSerialColumn();
        builder.Property(e => e.Data).HasColumnType("jsonb");

        builder.HasOne<Consultation>().WithMany().HasForeignKey(e => e.ConsultationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ConsultationId, e.Kind, e.Id });
    }
}
