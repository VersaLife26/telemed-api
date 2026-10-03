using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class MedicalReportConfiguration : IEntityTypeConfiguration<MedicalReport>
{
    public const string AppointmentIndex = "ux_medical_reports_appointment_id";

    public void Configure(EntityTypeBuilder<MedicalReport> builder)
    {
        builder.ToTable("medical_reports", t =>
            t.HasCheckConstraint("ck_medical_reports_cancelled", "(status = 'cancelled') = (cancelled_at IS NOT NULL)"));
        builder.Property(r => r.DoctorName).HasMaxLength(200);
        builder.Property(r => r.DoctorSlmc).HasMaxLength(20);
        builder.Property(r => r.DoctorQualifications).HasMaxLength(2000);
        builder.Property(r => r.VerificationHmac).HasMaxLength(64).IsFixedLength();
        builder.Property(r => r.CancellationReason).HasMaxLength(1000);
        builder.Property(r => r.Addressee).HasMaxLength(200);
        builder.Property(r => r.ClinicalImpression).HasMaxLength(500);
        builder.Property(r => r.Findings).HasMaxLength(4000);
        builder.Property(r => r.Advice).HasMaxLength(2000);
        builder.Property(r => r.FitnessNotes).HasMaxLength(1000);
        builder.Property(r => r.Version).IsRowVersion();

        builder.HasOne<Appointment>().WithOne().HasForeignKey<MedicalReport>(r => r.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(r => r.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.PatientId)
            .HasConstraintName("fk_medical_reports_users_patient_id").OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.AppointmentId).IsUnique().HasDatabaseName(AppointmentIndex);
        builder.HasIndex(r => new { r.PatientId, r.IssuedAt });
        builder.HasIndex(r => new { r.DoctorId, r.IssuedAt });
    }
}
