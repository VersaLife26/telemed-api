using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public const string AppointmentIndex = "ux_prescriptions_appointment_id";

    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("prescriptions", t =>
            t.HasCheckConstraint("ck_prescriptions_cancelled", "(status = 'cancelled') = (cancelled_at IS NOT NULL)"));
        builder.Property(p => p.DoctorName).HasMaxLength(200);
        builder.Property(p => p.DoctorSlmc).HasMaxLength(20);
        builder.Property(p => p.DoctorQualifications).HasMaxLength(2000);
        builder.Property(p => p.VerificationHmac).HasMaxLength(64).IsFixedLength();
        builder.Property(p => p.CancellationReason).HasMaxLength(1000);
        builder.Property(p => p.Version).IsRowVersion();

        builder.HasOne<Appointment>().WithOne().HasForeignKey<Prescription>(p => p.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(p => p.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.PatientId)
            .HasConstraintName("fk_prescriptions_users_patient_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Items).WithOne().HasForeignKey(i => i.PrescriptionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.AppointmentId).IsUnique().HasDatabaseName(AppointmentIndex);
        builder.HasIndex(p => new { p.PatientId, p.IssuedAt });
        builder.HasIndex(p => new { p.DoctorId, p.IssuedAt });
    }
}

internal sealed class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
    {
        builder.ToTable("prescription_items", t =>
        {
            t.HasCheckConstraint("ck_prescription_items_duration_days", "duration_days > 0");
            t.HasCheckConstraint("ck_prescription_items_quantity", "quantity > 0");
        });
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.DrugName).HasMaxLength(200);
        builder.Property(i => i.Strength).HasMaxLength(100);
        builder.Property(i => i.Form).HasMaxLength(100);
        builder.Property(i => i.Dosage).HasMaxLength(200);
        builder.Property(i => i.Frequency).HasMaxLength(200);
        builder.Property(i => i.Instructions).HasMaxLength(1000);

        builder.HasOne<Drug>().WithMany().HasForeignKey(i => i.DrugId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.PrescriptionId, i.SortOrder }).IsUnique();
        builder.HasIndex(i => i.DrugId);
    }
}
