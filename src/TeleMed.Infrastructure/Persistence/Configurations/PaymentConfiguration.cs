using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", t =>
        {
            t.HasCheckConstraint("ck_payments_amounts", "gross_cents > 0 AND discount_cents >= 0 AND amount_cents > 0 AND amount_cents = gross_cents - discount_cents");
            t.HasCheckConstraint("ck_payments_captured", "captured_cents IS NULL OR (captured_cents > 0 AND captured_cents <= amount_cents)");
            t.HasCheckConstraint("ck_payments_split",
                "commission_cents >= 0 AND provider_fee_cents >= 0 AND payout_cents >= 0 " +
                "AND commission_cents + provider_fee_cents + payout_cents = COALESCE(captured_cents, amount_cents)");
            t.HasCheckConstraint("ck_payments_refunds",
                "refunded_commission_cents >= 0 AND refunded_provider_fee_cents >= 0 AND refunded_payout_cents >= 0 " +
                "AND refunded_commission_cents + refunded_provider_fee_cents + refunded_payout_cents = refunded_cents " +
                "AND refunded_cents <= COALESCE(captured_cents, 0)");
        });

        builder.Property(p => p.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(p => p.PromoCode).HasMaxLength(50);
        builder.Property(p => p.ProviderPaymentId).HasMaxLength(100);
        builder.Property(p => p.AuthorizationToken).HasMaxLength(500);
        builder.Property(p => p.FailureReason).HasMaxLength(500);
        builder.Property(p => p.Version).IsRowVersion();

        builder.HasOne<Appointment>().WithMany().HasForeignKey(p => p.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.PatientId)
            .HasConstraintName("fk_payments_users_patient_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(p => p.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payout>().WithMany().HasForeignKey(p => p.PayoutId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.AppointmentId).IsUnique().HasDatabaseName("ux_payments_appointment_id");
        builder.HasIndex(p => new { p.PatientId, p.CreatedAt });
        builder.HasIndex(p => p.DoctorId);
        builder.HasIndex(p => p.PayoutId);
        builder.HasIndex(p => p.SucceededAt);
        builder.HasIndex(p => p.Status).HasFilter("status = 'authorized'");
    }
}
