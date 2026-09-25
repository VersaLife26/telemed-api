using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public const string DoctorOverlapConstraint = "ex_appointments_doctor_overlap";
    public const string PatientOverlapConstraint = "ex_appointments_patient_overlap";

    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments", t =>
        {
            t.HasCheckConstraint("ck_appointments_range", "end_at > start_at");
            t.HasCheckConstraint("ck_appointments_fee_cents", "fee_cents >= 0");
            t.HasCheckConstraint("ck_appointments_refund_percent", "refund_percent BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_appointments_visit_patient_weight_kg", "visit_patient_weight_kg > 0");
        });

        builder.Property(a => a.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(a => a.Intake).HasJsonbConversion();
        builder.Property(a => a.VisitPatientName).HasMaxLength(200);
        builder.Property(a => a.VisitPatientWeightKg).HasPrecision(4, 1);
        builder.Property(a => a.VisitPatientAllergies).HasMaxLength(1000);
        builder.Property(a => a.CancellationReason).HasMaxLength(1000);
        builder.Property(a => a.Version).IsRowVersion();

        builder.HasOne<User>().WithMany().HasForeignKey(a => a.PatientId)
            .HasConstraintName("fk_appointments_users_patient_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(a => a.DoctorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.DoctorId, a.StartAt });
        builder.HasIndex(a => new { a.PatientId, a.StartAt });
        builder.HasIndex(a => a.PaymentDueAt).HasFilter("status = 'pending_payment'");
    }
}
