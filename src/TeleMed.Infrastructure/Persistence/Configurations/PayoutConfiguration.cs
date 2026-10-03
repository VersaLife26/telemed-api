using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class PayoutBatchConfiguration : IEntityTypeConfiguration<PayoutBatch>
{
    public void Configure(EntityTypeBuilder<PayoutBatch> builder)
    {
        builder.ToTable("payout_batches", t => t.HasCheckConstraint("ck_payout_batches_period", "period_start <= period_end"));
        builder.Property(b => b.Version).IsRowVersion();
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(b => b.CreatedByAdminId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => b.PeriodEnd).IsUnique().HasDatabaseName("ux_payout_batches_period_end");
    }
}

internal sealed class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.ToTable("payouts", t =>
        {
            t.HasCheckConstraint("ck_payouts_amounts", "amount_cents >= 0 AND payment_count > 0");
            t.HasCheckConstraint("ck_payouts_paid", "status <> 'paid' OR (transfer_reference IS NOT NULL AND paid_at IS NOT NULL)");
        });

        builder.Property(p => p.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(p => p.TransferReference).HasMaxLength(200);
        builder.Property(p => p.FailureReason).HasMaxLength(1000);
        builder.Property(p => p.Version).IsRowVersion();

        builder.HasOne<PayoutBatch>().WithMany().HasForeignKey(p => p.BatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(p => p.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(p => p.MarkedByAdminId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.DoctorId, p.Period, p.Currency }).IsUnique().HasDatabaseName("ux_payouts_doctor_id_period");
        builder.HasIndex(p => p.BatchId);
    }
}

internal sealed class PayoutAdjustmentConfiguration : IEntityTypeConfiguration<PayoutAdjustment>
{
    public void Configure(EntityTypeBuilder<PayoutAdjustment> builder)
    {
        builder.ToTable("payout_adjustments", t => t.HasCheckConstraint("ck_payout_adjustments_amount", "amount_cents < 0"));

        builder.Property(a => a.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(a => a.Version).IsRowVersion();

        builder.HasOne<Doctor>().WithMany().HasForeignKey(a => a.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Refund>().WithMany().HasForeignKey(a => a.RefundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payment>().WithMany().HasForeignKey(a => a.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payout>().WithMany().HasForeignKey(a => a.AppliedPayoutId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.RefundId).IsUnique().HasDatabaseName("ux_payout_adjustments_refund_id");
        builder.HasIndex(a => a.PaymentId);
        builder.HasIndex(a => a.AppliedPayoutId);
        builder.HasIndex(a => a.DoctorId).HasFilter("applied_payout_id IS NULL");
    }
}
