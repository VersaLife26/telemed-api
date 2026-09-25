using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class RescheduleRequestConfiguration : IEntityTypeConfiguration<RescheduleRequest>
{
    public const string PendingPerAppointmentIndex = "ux_reschedule_requests_appointment_pending";

    public void Configure(EntityTypeBuilder<RescheduleRequest> builder)
    {
        builder.ToTable("reschedule_requests", t =>
        {
            t.HasCheckConstraint("ck_reschedule_requests_original_range", "original_end_at > original_start_at");
            t.HasCheckConstraint("ck_reschedule_requests_proposed_range", "proposed_end_at > proposed_start_at");
        });

        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.Version).IsRowVersion();

        builder.HasOne<Appointment>().WithMany().HasForeignKey(r => r.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(r => r.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.PatientId)
            .HasConstraintName("fk_reschedule_requests_users_patient_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.RequestedByUserId)
            .HasConstraintName("fk_reschedule_requests_users_requested_by_user_id").OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.AppointmentId, "all").HasDatabaseName("ix_reschedule_requests_appointment_id");
        builder.HasIndex(r => r.AppointmentId, "pending").IsUnique().HasFilter("status = 'pending'").HasDatabaseName(PendingPerAppointmentIndex);
        builder.HasIndex(r => new { r.DoctorId, r.ProposedStartAt }).HasFilter("status = 'pending'");
        builder.HasIndex(r => r.OriginalStartAt).HasFilter("status = 'pending'");
        builder.HasIndex(r => new { r.Status, r.CreatedAt });
    }
}
