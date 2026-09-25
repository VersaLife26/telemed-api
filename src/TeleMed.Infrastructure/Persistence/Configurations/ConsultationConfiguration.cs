using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class ConsultationConfiguration : IEntityTypeConfiguration<Consultation>
{
    public void Configure(EntityTypeBuilder<Consultation> builder)
    {
        builder.ToTable("consultations", t => t.HasCheckConstraint("ck_consultations_duration_seconds", "duration_seconds >= 0"));
        builder.Property(c => c.EndReason).HasMaxLength(200);
        builder.Property(c => c.Version).IsRowVersion();

        builder.HasOne<Appointment>().WithOne().HasForeignKey<Consultation>(c => c.AppointmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.AppointmentId).IsUnique().HasDatabaseName("ux_consultations_appointment_id");
        builder.HasIndex(c => c.Status).HasFilter("status IN ('waiting', 'active')");
    }
}
